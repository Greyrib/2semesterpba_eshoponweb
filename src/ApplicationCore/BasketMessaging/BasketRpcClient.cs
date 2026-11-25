using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

namespace Microsoft.eShopWeb.ApplicationCore.BasketMessaging;

/// <summary>
/// Sender side of the Basket microservice's request/reply query queue.
/// Used by BasketService when FeatureToggleOptions.UseBasketMicroservice is on.
/// Reuses the same MessagingOptions ("Messaging:Host" etc.) as the generic Messaging
/// library - same RabbitMQ broker, already correctly pointed at "rabbitmq" in Docker.
///
/// Connects lazily on first RequestAsync, same as Messaging.RabbitMqConnection - not eagerly
/// at app startup. An eager InitializeAsync() call in Program.cs crashed the FunctionalTests
/// host (it boots the whole app but has no RabbitMQ available); lazy connect means nothing
/// is attempted unless the toggle is actually on and a basket query is actually sent.
/// </summary>
public class BasketRpcClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly MessagingOptions _opt;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IEnvironment? _environment;
    private IConnection? _connection;
    private IRequester? _requester;

    public BasketRpcClient(IOptions<MessagingOptions> opt) => _opt = opt.Value;

    private async Task<IRequester> GetRequesterAsync()
    {
        if (_requester != null) return _requester;
        await _lock.WaitAsync();
        try
        {
            if (_requester != null) return _requester;

            var settings = ConnectionSettingsBuilder.Create()
                .Host(_opt.Host).Port(_opt.Port)
                .User(_opt.User).Password(_opt.Password)
                .ContainerId(BasketMessagingConstants.ContainerId)
                .Build();

            _environment = AmqpEnvironment.Create(settings);
            _connection = await BasketBrokerRetry.ConnectAsync(_environment);

            await _connection.Management()
                .Queue(BasketMessagingConstants.QueriesQueue)
                .Type(QueueType.QUORUM)
                .DeclareAsync();

            _requester = await _connection.RequesterBuilder()
                .RequestAddress()
                .Queue(BasketMessagingConstants.QueriesQueue)
                .Requester()
                .BuildAsync();
            return _requester;
        }
        finally { _lock.Release(); }
    }

    public async Task<TResponse?> RequestAsync<TRequest, TResponse>(string messageType, TRequest payload, TimeSpan? timeout = null)
    {
        var requester = await GetRequesterAsync();

        var request = new AmqpMessage(JsonSerializer.SerializeToUtf8Bytes(payload)).Subject(messageType);

        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        IMessage reply;
        try
        {
            reply = await requester.PublishAsync(request, cts.Token);
        }
        catch
        {
            throw new TimeoutException($"No reply for '{messageType}'. Is the Basket microservice running?");
        }

        var body = (byte[])reply.Body();

        if (reply.Subject() == "error")
            throw new Exception($"Basket microservice error: {JsonSerializer.Deserialize<string>(body, Json)}");

        return JsonSerializer.Deserialize<TResponse>(body, Json);
    }

    public async ValueTask DisposeAsync()
    {
        if (_requester is not null) await _requester.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
        if (_environment is not null) await _environment.CloseAsync();
    }
}
