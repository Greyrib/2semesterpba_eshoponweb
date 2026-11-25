namespace BasketMicroservice.RabbitMQ;

internal static class BasketMessagingConstants
{
    // Single source of truth for queue names/container id. Broker host/port/credentials
    // come from BasketBrokerOptions (config section "Messaging") instead, so this doesn't
    // hardcode "localhost" - see BasketMicroservice/README.md.
    public const string ContainerId = "basketMicroservice";

    // Commands (fire-and-forget): routed through a direct exchange instead of a single
    // shared queue, so routing is a broker concept (exchange + binding key) rather than
    // an in-app "switch (Subject)". One queue per command type, bound by routing key.
    public const string CommandsExchange = "basket-commands-exchange";
    public const string DeleteBasketRoutingKey = "DeleteBasket";
    public const string DeleteBasketQueue = "basket-delete-queue";
    public const string TransferBasketRoutingKey = "TransferBasket";
    public const string TransferBasketQueue = "basket-transfer-queue";

    // Queries (request/reply): stays as a single queue. There's no routing benefit from an
    // exchange here - the client library's requester/responder already correlates the single
    // reply back to the single caller, so splitting it up would add bindings for no gain.
    public const string QueriesQueue = "basket-queries";
}
