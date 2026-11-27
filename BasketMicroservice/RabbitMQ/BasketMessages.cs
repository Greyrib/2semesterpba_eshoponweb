namespace BasketMicroservice.RabbitMQ;

// Commands (fire-and-forget, sent to BasketMessagingConstants.CommandsQueue)
public record AddItemCommand(string BuyerId, int CatalogItemId, decimal UnitPrice, int Quantity = 1);
public record SetQuantitiesCommand(int BasketId, Dictionary<string, int> Quantities);
public record TransferBasketCommand(string AnonymousId, string UserName);
public record DeleteBasketCommand(int BasketId);

// Queries (request/reply, sent to BasketMessagingConstants.QueriesQueue)
public record GetBasketQuery(string BuyerId);
public record GetBasketByIdQuery(int BasketId);

// Shared reply shape
public record BasketItemDto(int Id, int CatalogItemId, decimal UnitPrice, int Quantity);
public record BasketDto(int Id, string BuyerId, List<BasketItemDto> Items);
