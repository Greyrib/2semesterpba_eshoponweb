using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Messaging;
using Microsoft.eShopWeb.ApplicationCore.BasketMessaging;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
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
    private readonly BasketRpcClient? _basketRpc;
    private readonly FeatureToggleOptions _featureToggles;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IMessageRequester requester,
        IOptions<FeatureToggleOptions> featureToggles,
        BasketRpcClient? basketRpc = null)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _requester = requester;
        _featureToggles = featureToggles.Value;
        _basketRpc = basketRpc;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        // Basket now lives in BasketMicroservice (not this process's DB) once that toggle is
        // on - reading it via BasketWithItemsSpecification here would always come back empty.
        var basket = _featureToggles.UseBasketMicroservice
            ? await GetBasketFromMicroservice(basketId)
            : await GetBasketFromLocalDatabase(basketId);

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

        var order = new Order(basket.BuyerId, shippingAddress, items);

        await _orderRepository.AddAsync(order);
    }

    private async Task<Basket?> GetBasketFromLocalDatabase(int basketId)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        return await _basketRepository.FirstOrDefaultAsync(basketSpec);
    }

    private async Task<Basket?> GetBasketFromMicroservice(int basketId)
    {
        var dto = await _basketRpc!.RequestAsync<GetBasketByIdQuery, BasketDto>(
            "GetBasketById", new GetBasketByIdQuery(basketId));
        if (dto is null) return null;

        var items = dto.Items.Select(i => BasketItem.Rehydrate(i.Id, i.CatalogItemId, i.UnitPrice, i.Quantity));
        return Basket.Rehydrate(dto.Id, dto.BuyerId, items);
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
