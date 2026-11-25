using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.BasketMessaging;

// Commands (fire-and-forget, sent to BasketMessagingConstants.CommandsExchange)
internal record TransferBasketCommand(string AnonymousId, string UserName);
internal record DeleteBasketCommand(int BasketId);

// Queries (request/reply, sent to BasketMessagingConstants.QueriesQueue)
internal record GetBasketQuery(string BuyerId);
internal record GetBasketByIdQuery(int BasketId);
internal record AddItemCommand(string BuyerId, int CatalogItemId, decimal UnitPrice, int Quantity = 1);
internal record SetQuantitiesCommand(int BasketId, Dictionary<string, int> Quantities);

// Shared reply shape
internal record BasketItemDto(int Id, int CatalogItemId, decimal UnitPrice, int Quantity);
internal record BasketDto(int Id, string BuyerId, List<BasketItemDto> Items);
