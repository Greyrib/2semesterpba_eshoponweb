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

        await connection.Management().Queue(CommandsQueue).Type(QueueType.QUORUM).DeclareAsync();
        await connection.Management().Queue(QueriesQueue).Type(QueueType.QUORUM).DeclareAsync();

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
        catch (Exception ex)
        {
            ctx.Discard();
        }
    }

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
