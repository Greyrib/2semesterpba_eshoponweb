using System.Text.Json;
using CatalogMicroservice.applicationCore.Entities;
using CatalogMicroservice.CatalogMicroservice.CatalogItemEndpoints;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;
using Microsoft.eShopWeb.CatalogMicroservice.CatalogItemEndpoints;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

namespace CatalogMicroservice.RabbitMQ;

public class CatalogMessageWorker : BackgroundService
{
    const string brokerUri = "amqp://guest:guest@localhost:5672/%2f";
    private const string CommandsQueue = "catalog-commands";
    private const string QueriesQueue = "catalog-queries";
    public const string DeadQueue = "catalog-commands.dead";
    public const string InvalidQueue = "catalog-commands.invalid";
    public const string DeadExchange = "catalog-dlx";

    private IPublisher? _invalidPublisher;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopes;

    public CatalogMessageWorker(IConfiguration config, IServiceScopeFactory scopes)
    {
        _config = config;
        _scopes = scopes;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ConnectionSettings settings = ConnectionSettingsBuilder.Create()
        .Uri(new Uri(brokerUri))
        .ContainerId("catalogMicroservice")
        .Build();

        IEnvironment environment = AmqpEnvironment.Create(settings);
        IConnection connection = await environment.CreateConnectionAsync();

        var management = connection.Management();

        // Dead-letter exchange + the two "parking" queues
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

        await management.Queue(QueriesQueue).Type(QueueType.CLASSIC).DeclareAsync();

        _invalidPublisher = await connection.PublisherBuilder().Queue(InvalidQueue).BuildAsync();

        //Commands
        var commandsConsumer = await connection.ConsumerBuilder()
            .Queue(CommandsQueue)
            .InitialCredits(1)
            .MessageHandler(HandleCommandAsync)
            .BuildAndStartAsync();

        //Queries
        var responder = await connection.ResponderBuilder()
            .RequestQueue(QueriesQueue)
            .Handler(async (ctx, request) =>
            {
                var (status, result) = await RunQueryAsync(request);
                return ctx.Message(JsonSerializer.SerializeToUtf8Bytes(result)).Subject(status);
            })
            .BuildAsync();

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        await commandsConsumer.CloseAsync();
        await responder.CloseAsync();
        if (_invalidPublisher is not null) await _invalidPublisher.CloseAsync();
        await connection.CloseAsync();
        await environment.CloseAsync();

    }

    //Commands
    private async Task HandleCommandAsync(IContext ctx, IMessage msg)
    {
        try
        {
            var body = (byte[])msg.Body();
            using var scope = _scopes.CreateScope();
            var sp = scope.ServiceProvider;
            var repo = sp.GetRequiredService<IRepository<CatalogItem>>();

            switch (msg.Subject())
            {
                case "CreateCatalogItem":
                    var create = ActivatorUtilities.CreateInstance<CreateCatalogItemEndpoint>(sp);
                    await create.HandleAsync(JsonSerializer.Deserialize<CreateCatalogItemRequest>(body, Json)!, repo);
                    break;
                case "UpdateCatalogItem":
                    var update = ActivatorUtilities.CreateInstance<UpdateCatalogItemEndpoint>(sp);
                    await update.HandleAsync(JsonSerializer.Deserialize<UpdateCatalogItemRequest>(body, Json)!, repo);
                    break;
                case "DeleteCatalogItem":
                    var delete = ActivatorUtilities.CreateInstance<DeleteCatalogItemEndpoint>(sp);
                    await delete.HandleAsync(JsonSerializer.Deserialize<DeleteCatalogItemRequest>(body, Json)!, repo);
                    break;
                default:
                    ctx.Discard();
                    return;
            }

            ctx.Accept();
        }
        catch (Exception ex) when (ex is JsonException or InvalidMessageException)
        {
            //invalid
            var parked = new AmqpMessage((byte[])msg.Body())
                .Subject(msg.Subject() ?? "")
                .Property("error", ex.Message);

            var outcome = await _invalidPublisher!.PublishAsync(parked);
            if (outcome.Outcome.State == OutcomeState.Accepted)
                ctx.Accept();
            else
                ctx.Requeue();
        }
        catch (Exception ex)
        {
            //failed. Dead letter
            ctx.Requeue();
        }
    }

    private sealed class InvalidMessageException(string message) : Exception(message);

    public async Task<(string Status, object? Result)> RunQueryAsync(IMessage request)
    {
        try
        {
            var body = (byte[])request.Body();
            using var scope = _scopes.CreateScope();
            var sp = scope.ServiceProvider;
            var repo = sp.GetRequiredService<IRepository<CatalogItem>>();

            IResult result;
            switch (request.Subject())
            {
                case "GetCatalogItemById":
                    var getById = ActivatorUtilities.CreateInstance<CatalogItemGetByIdEndpoint>(sp);
                    result = await getById.HandleAsync(JsonSerializer.Deserialize<GetByIdCatalogItemRequest>(body, Json)!, repo);
                    break;
                case "ListCatalogItemsPaged":
                    var list = ActivatorUtilities.CreateInstance<CatalogItemListPagedEndpoint>(sp);
                    result = await list.HandleAsync(JsonSerializer.Deserialize<ListPagedCatalogItemRequest>(body, Json)!, repo);
                    break;
                default:
                    return ("error", $"Unknown query {request.Subject()}");
            }

            return ToReply(result);
        }
        catch (Exception ex)
        {
            return ("error", ex.Message);
        }
    }
    private static (string Status, object? Result) ToReply(IResult result) => result switch
    {
        IStatusCodeHttpResult { StatusCode: 404 } => ("ok", null),   // not found -> null
        IStatusCodeHttpResult { StatusCode: >= 400 and var code } => ("error", $"Endpoint returned {code}"),
        IValueHttpResult v => ("ok", v.Value),
        _ => ("error", "Unexpected endpoint result")
    };
}
