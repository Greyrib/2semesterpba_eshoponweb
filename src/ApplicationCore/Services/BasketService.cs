using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Ardalis.Result;
using Microsoft.eShopWeb.ApplicationCore.BasketMessaging;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class BasketService : IBasketService
{
    private readonly IRepository<Basket> _basketRepository;
    private readonly IAppLogger<BasketService> _logger;
    private readonly BasketQueue? _queue;
    private readonly BasketRpcClient? _rpc;
    private readonly IBasketQueryService? _basketQueryService;
    private readonly FeatureToggleOptions _featureToggles;

    // queue/rpc/basketQueryService/featureToggles are optional (default off) so existing call
    // sites that only pass (basketRepository, logger) - e.g. the monolith's own unit tests -
    // keep compiling unchanged; they never hit the UseBasketMicroservice branches below.
    public BasketService(IRepository<Basket> basketRepository,
        IAppLogger<BasketService> logger,
        BasketQueue? queue = null,
        BasketRpcClient? rpc = null,
        IBasketQueryService? basketQueryService = null,
        IOptions<FeatureToggleOptions>? featureToggles = null)
    {
        _basketRepository = basketRepository;
        _logger = logger;
        _queue = queue;
        _rpc = rpc;
        _basketQueryService = basketQueryService;
        _featureToggles = featureToggles?.Value ?? new FeatureToggleOptions();
    }

    public async Task<Basket> AddItemToBasket(string username, int catalogItemId, decimal price, int quantity = 1)
    {
        if (_featureToggles.UseBasketMicroservice)
        {
            var dto = await _rpc!.RequestAsync<AddItemCommand, BasketDto>(
                "AddItem", new AddItemCommand(username, catalogItemId, price, quantity));
            return ToBasket(dto!);
        }

        var basketSpec = new BasketWithItemsSpecification(username);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);

        if (basket == null)
        {
            basket = new Basket(username);
            await _basketRepository.AddAsync(basket);
        }

        basket.AddItem(catalogItemId, price, quantity);

        await _basketRepository.UpdateAsync(basket);
        return basket;
    }

    public async Task DeleteBasketAsync(int basketId)
    {
        if (_featureToggles.UseBasketMicroservice)
        {
            await _queue!.SendAsync(BasketMessagingConstants.DeleteBasketRoutingKey, new DeleteBasketCommand(basketId));
            return;
        }

        var basket = await _basketRepository.GetByIdAsync(basketId);
        Guard.Against.Null(basket, nameof(basket));
        await _basketRepository.DeleteAsync(basket);
    }

    public async Task<Result<Basket>> SetQuantities(int basketId, Dictionary<string, int> quantities)
    {
        if (_featureToggles.UseBasketMicroservice)
        {
            var dto = await _rpc!.RequestAsync<SetQuantitiesCommand, BasketDto>(
                "SetQuantities", new SetQuantitiesCommand(basketId, quantities));
            return dto is null ? Result<Basket>.NotFound() : ToBasket(dto);
        }

        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);
        if (basket == null) return Result<Basket>.NotFound();

        foreach (var item in basket.Items)
        {
            if (quantities.TryGetValue(item.Id.ToString(), out var quantity))
            {
                if (_logger != null) _logger.LogInformation($"Updating quantity of item ID:{item.Id} to {quantity}.");
                item.SetQuantity(quantity);
            }
        }
        basket.RemoveEmptyItems();
        await _basketRepository.UpdateAsync(basket);
        return basket;
    }

    public async Task TransferBasketAsync(string anonymousId, string userName)
    {
        if (_featureToggles.UseBasketMicroservice)
        {
            await _queue!.SendAsync(BasketMessagingConstants.TransferBasketRoutingKey, new TransferBasketCommand(anonymousId, userName));
            return;
        }

        var anonymousBasketSpec = new BasketWithItemsSpecification(anonymousId);
        var anonymousBasket = await _basketRepository.FirstOrDefaultAsync(anonymousBasketSpec);
        if (anonymousBasket == null) return;
        var userBasketSpec = new BasketWithItemsSpecification(userName);
        var userBasket = await _basketRepository.FirstOrDefaultAsync(userBasketSpec);
        if (userBasket == null)
        {
            userBasket = new Basket(userName);
            await _basketRepository.AddAsync(userBasket);
        }
        foreach (var item in anonymousBasket.Items)
        {
            userBasket.AddItem(item.CatalogItemId, item.UnitPrice, item.Quantity);
        }
        await _basketRepository.UpdateAsync(userBasket);
        await _basketRepository.DeleteAsync(anonymousBasket);
    }

    public async Task<Basket> GetOrCreateBasketIfHasNoneForUser(string buyerId)
    {
        if (_featureToggles.UseBasketMicroservice)
        {
            var dto = await _rpc!.RequestAsync<GetBasketQuery, BasketDto>(
                "GetOrCreateBasket", new GetBasketQuery(buyerId));
            return ToBasket(dto!);
        }

        var basketSpec = new BasketWithItemsSpecification(buyerId);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);
        if (basket != null) return basket;

        basket = new Basket(buyerId);
        await _basketRepository.AddAsync(basket);
        return basket;
    }

    public async Task<int> CountTotalBasketItems(string buyerId)
    {
        if (_featureToggles.UseBasketMicroservice)
        {
            var dto = await _rpc!.RequestAsync<GetBasketQuery, BasketDto>(
                "GetBasket", new GetBasketQuery(buyerId));
            return dto?.Items.Sum(i => i.Quantity) ?? 0;
        }

        if (_basketQueryService != null) return await _basketQueryService.CountTotalBasketItems(buyerId);

        var basketSpec = new BasketWithItemsSpecification(buyerId);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);
        return basket?.TotalItems ?? 0;
    }

    private static Basket ToBasket(BasketDto dto)
    {
        var items = dto.Items.Select(i => BasketItem.Rehydrate(i.Id, i.CatalogItemId, i.UnitPrice, i.Quantity));
        return Basket.Rehydrate(dto.Id, dto.BuyerId, items);
    }
}
