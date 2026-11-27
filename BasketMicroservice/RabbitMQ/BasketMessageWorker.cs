using System.Linq;
using System.Text.Json;
using BasketMicroservice.Data;
using BasketMicroservice.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

namespace BasketMicroservice.RabbitMQ;

/// <summary>
/// Receiver side. Commands arrive pre-sorted into one queue per command type (the
/// CommandsExchange + bindings route them there - see BasketQueue.InitializeAsync),
/// so each consumer here just does its one job with no dispatch/switch needed.
/// Queries still share one request/reply queue, dispatched on Subject.
/// </summary>
public class BasketMessageWorker : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly IServiceScopeFactory _scopes;
    private readonly BasketBrokerOptions _opt;

    public BasketMessageWorker(IServiceScopeFactory scopes, IOptions<BasketBrokerOptions> opt)
    {
        _scopes = scopes;
        _opt = opt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = ConnectionSettingsBuilder.Create()
            .Host(_opt.Host).Port(_opt.Port)
            .User(_opt.User).Password(_opt.Password)
            .ContainerId(BasketMessagingConstants.ContainerId)
            .Build();

        var environment = AmqpEnvironment.Create(settings);
        var connection = await BasketBrokerRetry.ConnectAsync(environment);
        var management = connection.Management();

        await management.Exchange(BasketMessagingConstants.CommandsExchange).Type(ExchangeType.DIRECT).DeclareAsync();
        await DeclareAndBindAsync(management, BasketMessagingConstants.DeleteBasketQueue, BasketMessagingConstants.DeleteBasketRoutingKey);
        await DeclareAndBindAsync(management, BasketMessagingConstants.TransferBasketQueue, BasketMessagingConstants.TransferBasketRoutingKey);
        await management.Queue(BasketMessagingConstants.QueriesQueue).Type(QueueType.QUORUM).DeclareAsync();

        var deleteConsumer = await connection.ConsumerBuilder()
            .Queue(BasketMessagingConstants.DeleteBasketQueue)
            .InitialCredits(1)
            .MessageHandler(HandleDeleteBasketAsync)
            .BuildAndStartAsync();

        var transferConsumer = await connection.ConsumerBuilder()
            .Queue(BasketMessagingConstants.TransferBasketQueue)
            .InitialCredits(1)
            .MessageHandler(HandleTransferBasketAsync)
            .BuildAndStartAsync();

        var responder = await connection.ResponderBuilder()
            .RequestQueue(BasketMessagingConstants.QueriesQueue)
            .Handler(async (ctx, request) =>
            {
                var (status, result) = await RunQueryAsync(request);
                return ctx.Message(JsonSerializer.SerializeToUtf8Bytes(result)).Subject(status);
            })
            .BuildAsync();

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        await deleteConsumer.CloseAsync();
        await transferConsumer.CloseAsync();
        await responder.CloseAsync();
        await connection.CloseAsync();
        await environment.CloseAsync();
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

    private async Task HandleDeleteBasketAsync(IContext ctx, IMessage msg)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IBasketRepository>();
            await HandleDeleteBasketAsync(repo, JsonSerializer.Deserialize<DeleteBasketCommand>((byte[])msg.Body(), Json)!);
            ctx.Accept();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{ex}, Failed handling DeleteBasket");
            ctx.Discard();
        }
    }

    private async Task HandleTransferBasketAsync(IContext ctx, IMessage msg)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IBasketRepository>();
            await HandleTransferBasketAsync(repo, JsonSerializer.Deserialize<TransferBasketCommand>((byte[])msg.Body(), Json)!);
            ctx.Accept();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{ex}, Failed handling TransferBasket");
            ctx.Discard();
        }
    }

    // AddItem/SetQuantities write to the basket, but the callers (Web pages) need the
    // resulting basket back immediately to render - so despite being writes, they're
    // handled as queries (request/reply) below, not fire-and-forget commands.
    private static async Task<Basket> HandleAddItemAsync(IBasketRepository repo, AddItemCommand cmd)
    {
        var basket = await repo.GetByBuyerIdAsync(cmd.BuyerId);
        if (basket is null)
        {
            basket = new Basket(cmd.BuyerId);
            await repo.AddAsync(basket);
        }
        basket.AddItem(cmd.CatalogItemId, cmd.UnitPrice, cmd.Quantity);
        await repo.SaveChangesAsync();
        return basket;
    }

    private static async Task<Basket?> HandleSetQuantitiesAsync(IBasketRepository repo, SetQuantitiesCommand cmd)
    {
        var basket = await repo.GetByIdAsync(cmd.BasketId);
        if (basket is null) return null;
        basket.SetQuantities(cmd.Quantities);
        await repo.SaveChangesAsync();
        return basket;
    }

    private static async Task HandleTransferBasketAsync(IBasketRepository repo, TransferBasketCommand cmd)
    {
        var anonymousBasket = await repo.GetByBuyerIdAsync(cmd.AnonymousId);
        if (anonymousBasket is null) return;

        var userBasket = await repo.GetByBuyerIdAsync(cmd.UserName);
        if (userBasket is null)
        {
            userBasket = new Basket(cmd.UserName);
            await repo.AddAsync(userBasket);
        }
        foreach (var item in anonymousBasket.Items)
        {
            userBasket.AddItem(item.CatalogItemId, item.UnitPrice, item.Quantity);
        }
        repo.Remove(anonymousBasket);
        await repo.SaveChangesAsync();
    }

    private static async Task HandleDeleteBasketAsync(IBasketRepository repo, DeleteBasketCommand cmd)
    {
        var basket = await repo.GetByIdAsync(cmd.BasketId);
        if (basket is null) return;
        repo.Remove(basket);
        await repo.SaveChangesAsync();
    }

    private async Task<(string Status, object? Result)> RunQueryAsync(IMessage request)
    {
        try
        {
            var body = (byte[])request.Body();
            using var scope = _scopes.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IBasketRepository>();

            switch (request.Subject())
            {
                case "GetBasket":
                    var getQuery = JsonSerializer.Deserialize<GetBasketQuery>(body, Json)!;
                    var found = await repo.GetByBuyerIdAsync(getQuery.BuyerId);
                    return ("ok", found is null ? null : ToDto(found));

                case "GetBasketById":
                    var getByIdQuery = JsonSerializer.Deserialize<GetBasketByIdQuery>(body, Json)!;
                    var foundById = await repo.GetByIdAsync(getByIdQuery.BasketId);
                    return ("ok", foundById is null ? null : ToDto(foundById));

                case "GetOrCreateBasket":
                    var getOrCreateQuery = JsonSerializer.Deserialize<GetBasketQuery>(body, Json)!;
                    var basket = await repo.GetByBuyerIdAsync(getOrCreateQuery.BuyerId);
                    if (basket is null)
                    {
                        basket = new Basket(getOrCreateQuery.BuyerId);
                        await repo.AddAsync(basket);
                        await repo.SaveChangesAsync();
                    }
                    return ("ok", ToDto(basket));

                case "AddItem":
                    var added = await HandleAddItemAsync(repo, JsonSerializer.Deserialize<AddItemCommand>(body, Json)!);
                    return ("ok", ToDto(added));

                case "SetQuantities":
                    var updated = await HandleSetQuantitiesAsync(repo, JsonSerializer.Deserialize<SetQuantitiesCommand>(body, Json)!);
                    return ("ok", updated is null ? null : ToDto(updated));

                default:
                    return ("error", $"Unknown query {request.Subject()}");
            }
        }
        catch (Exception ex)
        {
            return ("error", ex.Message);
        }
    }

    private static BasketDto ToDto(Basket basket) => new(
        basket.Id,
        basket.BuyerId,
        basket.Items.Select(i => new BasketItemDto(i.Id, i.CatalogItemId, i.UnitPrice, i.Quantity)).ToList());
}
