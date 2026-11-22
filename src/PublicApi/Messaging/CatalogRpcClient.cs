using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Result;
using Microsoft.Extensions.Configuration;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

namespace Microsoft.eShopWeb.PublicApi.Messaging;

public class CatalogRpcClient : IAsyncDisposable
{
    const string brokerUri = "amqp://guest:guest@localhost:5672/%2f";
    const string queueName = "catalog-queries";
    const string containerId = "catalogTasks";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public readonly IConfiguration _config;
    private IEnvironment? _environment;
    private IConnection? _connection;
    private IRequester? _requester;

    public CatalogRpcClient(IConfiguration config) => _config = config;

    public async Task InitalizeAsync()
    {
        ConnectionSettings settings = ConnectionSettingsBuilder.Create()
            .Uri(new Uri(brokerUri))
            .ContainerId(containerId)
            .Build();

        _environment = AmqpEnvironment.Create(settings);
        _connection = await _environment.CreateConnectionAsync();

        IManagement management = _connection.Management();
        IQueueSpecification queueSpec = management
            .Queue(queueName)
            .Type(QueueType.QUORUM);
        await queueSpec.DeclareAsync();

        _requester = await _connection.RequesterBuilder()
            .RequestAddress()
            .Queue(queueName)
            .Requester()
            .BuildAsync();
    }

    public async Task<TResponse?> RequestAsync<TRequest, TResponse>(string messageType, TRequest payload, TimeSpan? timeout = null)
    {
        if (_requester is null)
            throw new InvalidOperationException("catalogRpcClient not initialized");

        var request = new AmqpMessage(JsonSerializer.SerializeToUtf8Bytes(payload)).Subject(messageType);

        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
        IMessage reply;
        try
        {
            reply = await _requester.PublishAsync(request, cts.Token);
        }
        catch
        {
            throw new TimeoutException($"No reply for '{messageType}. Is the microservice running?'");
        }

        var body = (byte[])reply.Body();

        if (reply.Subject() == "error")
            throw new Exception($"Microservice error: {JsonSerializer.Deserialize<string>(body, Json)}");

        return JsonSerializer.Deserialize<TResponse>(body, Json);

    }

    public async ValueTask DisposeAsync()
    {
        await _requester.CloseAsync();
        await _connection.CloseAsync();
        await _environment.CloseAsync();
    }
}
