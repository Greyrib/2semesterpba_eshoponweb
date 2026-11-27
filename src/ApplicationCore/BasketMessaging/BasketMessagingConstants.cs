namespace Microsoft.eShopWeb.ApplicationCore.BasketMessaging;

/// <summary>
/// Must match BasketMicroservice.RabbitMQ.BasketMessagingConstants exactly - sender (here)
/// and receiver (BasketMicroservice) are two separate projects that agree on this wire
/// contract by convention, not by a shared assembly. See BasketMicroservice/README.md.
/// </summary>
internal static class BasketMessagingConstants
{
    // Broker host/port/credentials come from Messaging.MessagingOptions (config section
    // "Messaging") instead of being hardcoded here - see BasketMicroservice/README.md.
    public const string ContainerId = "basketMicroservice.client";

    public const string CommandsExchange = "basket-commands-exchange";
    public const string DeleteBasketRoutingKey = "DeleteBasket";
    public const string DeleteBasketQueue = "basket-delete-queue";
    public const string TransferBasketRoutingKey = "TransferBasket";
    public const string TransferBasketQueue = "basket-transfer-queue";

    public const string QueriesQueue = "basket-queries";
}
