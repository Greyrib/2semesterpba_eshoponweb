using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Messaging;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IMessageRequester _requester;
    private readonly FeatureToggleOptions _featureToggles;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IMessageRequester requester,
        IOptions<FeatureToggleOptions> featureToggles)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _requester = requester;
        _featureToggles = featureToggles.Value;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);

        Guard.Against.Null(basket, nameof(basket));
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemIds = basket.Items.Select(item => item.CatalogItemId).ToArray();
        var catalogItems = _featureToggles.UseCatalogMicroservice
            ? await GetCatalogItemsFromMicroservice(catalogItemIds)
            : await GetCatalogItemsFromLocalDatabase(catalogItemIds);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, catalogItem.PictureUri);
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        if (_featureToggles.UseInventoryMicroservice)
        {
            await DecrementStockInInventoryMicroservice(basket.Items);
        }

        var order = new Order(basket.BuyerId, shippingAddress, items);

        await _orderRepository.AddAsync(order);
    }

    // Throws instead of returning, so CreateOrderAsync stops before saving an order that can't be fulfilled.
    private async Task DecrementStockInInventoryMicroservice(IEnumerable<BasketItem> basketItems)
    {
        var request = new
        {
            Items = basketItems.Select(i => new { i.CatalogItemId, i.Quantity }).ToList()
        };

        DecrementStockResult? result;
        try
        {
            result = await _requester.RequestAsync<object, DecrementStockResult>(
                "inventory.decrement-stock", request, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            // e.g. RabbitMQ itself unreachable
            throw new InventoryUnavailableException(ex);
        }

        // RequestAsync returns null when no reply arrived before the timeout.
        if (result == null) throw new InventoryUnavailableException();
        if (!result.Success) throw new OutOfStockException(result.OutOfStockCatalogItemIds);
    }

    // Clanker-Claussen: both branches return the same RemoteCatalogItemDto shape so CreateOrderAsync's mapping above doesn't need its own if/else;
    // this one composes the picture URI itself since the local DB gives a raw path, whereas the microservice already sends a composed one.
    private async Task<List<RemoteCatalogItemDto>> GetCatalogItemsFromLocalDatabase(int[] catalogItemIds)
    {
        var catalogItemsSpecification = new CatalogItemsSpecification(catalogItemIds);
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);
        return catalogItems.Select(c => new RemoteCatalogItemDto { Id = c.Id, Name = c.Name, PictureUri = _uriComposer.ComposePicUri(c.PictureUri) }).ToList();
    }

    private async Task<List<RemoteCatalogItemDto>> GetCatalogItemsFromMicroservice(int[] catalogItemIds)
    {
        var result = await _requester.RequestAsync<object, List<RemoteCatalogItemDto>>(
            "catalog.list-items-by-ids", new { CatalogItemIds = catalogItemIds }, TimeSpan.FromSeconds(5));
        return result ?? new();
    }
}
