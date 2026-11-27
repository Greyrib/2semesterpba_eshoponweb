using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

namespace Microsoft.eShopWeb.ApplicationCore.BasketMessaging;

/// <summary>
/// Sender side of the Basket microservice's fire-and-forget command queues.
/// Used by BasketService when FeatureToggleOptions.UseBasketMicroservice is on.
/// Reuses the same MessagingOptions ("Messaging:Host" etc.) as the generic Messaging
/// library - same RabbitMQ broker, already correctly pointed at "rabbitmq" in Docker.
///
/// Connects lazily on first SendAsync, same as Messaging.RabbitMqConnection - not eagerly
/// at app startup. An eager InitializeAsync() call in Program.cs crashed the FunctionalTests
/// host (it boots the whole app but has no RabbitMQ available); lazy connect means nothing
/// is attempted unless the toggle is actually on and a basket command is actually sent.
/// </summary>
public class BasketQueue : IAsyncDisposable
{
    private readonly MessagingOptions _opt;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IEnvironment? _environment;
    private IConnection? _connection;
    private IPublisher? _publisher;

    public BasketQueue(IOptions<MessagingOptions> opt) => _opt = opt.Value;

    private async Task<IPublisher> GetPublisherAsync()
    {
        if (_publisher != null) return _publisher;
        await _lock.WaitAsync();
        try
        {
            if (_publisher != null) return _publisher;

            var settings = ConnectionSettingsBuilder.Create()
                .Host(_opt.Host).Port(_opt.Port)
                .User(_opt.User).Password(_opt.Password)
                .ContainerId(BasketMessagingConstants.ContainerId)
                .Build();

            _environment = AmqpEnvironment.Create(settings);
            _connection = await BasketBrokerRetry.ConnectAsync(_environment);

            var management = _connection.Management();

            await management.Exchange(BasketMessagingConstants.CommandsExchange)
                .Type(ExchangeType.DIRECT)
                .DeclareAsync();

            await DeclareAndBindAsync(management, BasketMessagingConstants.DeleteBasketQueue, BasketMessagingConstants.DeleteBasketRoutingKey);
            await DeclareAndBindAsync(management, BasketMessagingConstants.TransferBasketQueue, BasketMessagingConstants.TransferBasketRoutingKey);

            // No fixed Queue()/Exchange() here - each message below carries its own
            // destination via ToAddress(), so one publisher can send to any routing key.
            _publisher = await _connection.PublisherBuilder().BuildAsync();
            return _publisher;
        }
        finally { _lock.Release(); }
    }

    private static async Task DeclareAndBindAsync(IManagement management, string queue, string routingKey)
    {
        await management.Queue(queue).Type(QueueType.QUORUM).DeclareAsync();
        await management.Binding()
            .SourceExchange(BasketMessagingConstants.CommandsExchange)
            .DestinationQueue(queue)
            .Key(routingKey)
            .BindAsync();
    }

    public async Task SendAsync<T>(string routingKey, T payload)
    {
        var publisher = await GetPublisherAsync();

        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        var message = new AmqpMessage(body)
            .Subject(routingKey)
            .ToAddress().Exchange(BasketMessagingConstants.CommandsExchange).Key(routingKey).Build();

        var result = await publisher.PublishAsync(message);
        switch (result.Outcome.State)
        {
            case OutcomeState.Accepted:
                break;
            case OutcomeState.Released:
                Console.Error.WriteLine($"Released message: {result.Message.BodyAsString()}");
                break;
            case OutcomeState.Rejected:
                Console.Error.WriteLine($"Message rejected with error: {result.Outcome.Error}");
                break;
            default:
                Console.Error.WriteLine($"Unexpected publish outcome: {result.Outcome.State}");
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_publisher is not null) await _publisher.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
        if (_environment is not null) await _environment.CloseAsync();
    }
}
