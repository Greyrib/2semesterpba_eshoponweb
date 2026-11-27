using System;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

namespace Microsoft.eShopWeb.PublicApi.Messaging;

public class CatalogQueue : IAsyncDisposable
{
    const string brokerUri = "amqp://guest:guest@localhost:5672/%2f";
    const string taskQueueName = "catalog-commands";
    const string containerId = "catalogTasks";
    public readonly IConfiguration _config;
    private IEnvironment? _environment;
    private IConnection? _connection;
    private IPublisher? _publisher;

    public const string CommandsQueue = "catalog-commands";
    public const string QueriesQueue = "catalog-queries";
    public const string DeadQueue = "catalog-commands.dead";
    public const string InvalidQueue = "catalog-commands.invalid";
    public const string DeadExchange = "catalog-dlx";

    public CatalogQueue(IConfiguration config) => _config = config;

    public async Task InitializeAsync()
    {
        ConnectionSettings settings = ConnectionSettingsBuilder.Create()
            .Uri(new Uri(brokerUri))
            .ContainerId(containerId)
            .Build();

        _environment = AmqpEnvironment.Create(settings);
        _connection = await _environment.CreateConnectionAsync();

        IManagement management = _connection.Management();
        await management.Exchange(DeadExchange).Type(ExchangeType.DIRECT).DeclareAsync();
        await management.Queue(DeadQueue).Type(QueueType.QUORUM).DeclareAsync();
        await management.Queue(InvalidQueue).Type(QueueType.QUORUM).DeclareAsync();
        await management.Binding()
            .SourceExchange(DeadExchange)
            .DestinationQueue(DeadQueue)
            .Key(DeadQueue)
            .BindAsync();

        await management.Queue(CommandsQueue)
            .Type(QueueType.QUORUM)
            .DeadLetterExchange(DeadExchange)
            .DeadLetterRoutingKey(DeadQueue)
            .Quorum().DeliveryLimit(3).Queue()
            .DeclareAsync();

        await management.Queue(QueriesQueue).Type(QueueType.QUORUM).DeclareAsync();

        _publisher = await _connection.PublisherBuilder()
            .Queue(taskQueueName)
            .BuildAsync();
    }

    public async Task SendAsync<T>(string messageType, T payload)
    {
        if (_publisher is null)
            throw new InvalidOperationException("CatalogQueue not initlaized");

        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        var message = new AmqpMessage(body).Subject(messageType);

        var result = await _publisher.PublishAsync(message);
        switch (result.Outcome.State)
        {
            case OutcomeState.Accepted:
                break;
            case OutcomeState.Released:
                Console.Error.WriteLine($"Released message: {result.Message.BodyAsString()}");
                break;
            case OutcomeState.Rejected:
                Console.Error.WriteLine($"[Publisher] Message: {result.Message.BodyAsString()} rejected with error: {pr.Outcome.Error}");
                break;
            default:
                Console.Error.WriteLine($"Unexpected publish outcome: {result.Outcome.State}");
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _publisher.CloseAsync();
        await _connection.CloseAsync();
        await _environment.CloseAsync();
    }
}
