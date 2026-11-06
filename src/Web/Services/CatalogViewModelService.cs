using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Messaging;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.eShopWeb.ApplicationCore;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Web.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Web.Services;

/// <summary>
/// This is a UI-specific service so belongs in UI project. It does not contain any business logic and works
/// with UI-specific types (view models and SelectListItem types).
/// </summary>
public class CatalogViewModelService : ICatalogViewModelService
{
    private readonly ILogger<CatalogViewModelService> _logger;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IRepository<CatalogBrand> _brandRepository;
    private readonly IRepository<CatalogType> _typeRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IMessageRequester _requester;
    private readonly FeatureToggleOptions _featureToggles;

    public CatalogViewModelService(
        ILoggerFactory loggerFactory,
        IRepository<CatalogItem> itemRepository,
        IRepository<CatalogBrand> brandRepository,
        IRepository<CatalogType> typeRepository,
        IUriComposer uriComposer,
        IMessageRequester requester,
        IOptions<FeatureToggleOptions> featureToggles)
    {
        _logger = loggerFactory.CreateLogger<CatalogViewModelService>();
        _itemRepository = itemRepository;
        _brandRepository = brandRepository;
        _typeRepository = typeRepository;
        _uriComposer = uriComposer;
        _requester = requester;
        _featureToggles = featureToggles.Value;
    }

    public async Task<CatalogIndexViewModel> GetCatalogItems(int pageIndex, int itemsPage, int? brandId, int? typeId)
    {
        _logger.LogInformation("GetCatalogItems called.");

        List<CatalogItemViewModel> catalogItems;
        int totalItems;

        if (_featureToggles.UseCatalogMicroservice)
        {
            // Clanker-Claussen: local DB path below makes 2 separate repo calls (ListAsync + CountAsync); bundled into 1 round-trip here, same reason as PublicApi's paged endpoint.
            var query = new { PageSize = itemsPage, PageIndex = pageIndex, CatalogBrandId = brandId, CatalogTypeId = typeId };
            var result = await _requester.RequestAsync<object, RemoteCatalogItemsPagedResult>("catalog.list-items-paged", query, TimeSpan.FromSeconds(5))
                ?? new RemoteCatalogItemsPagedResult();

            catalogItems = result.CatalogItems.Select(i => new CatalogItemViewModel()
            {
                Id = i.Id,
                Name = i.Name,
                PictureUri = i.PictureUri,
                Price = i.Price
            }).ToList();
            totalItems = result.TotalItems;
        }
        else
        {
            var filterSpecification = new CatalogFilterSpecification(brandId, typeId);
            var filterPaginatedSpecification =
                new CatalogFilterPaginatedSpecification(itemsPage * pageIndex, itemsPage, brandId, typeId);

            var itemsOnPage = await _itemRepository.ListAsync(filterPaginatedSpecification);
            totalItems = await _itemRepository.CountAsync(filterSpecification);

            catalogItems = itemsOnPage.Select(i => new CatalogItemViewModel()
            {
                Id = i.Id,
                Name = i.Name,
                PictureUri = _uriComposer.ComposePicUri(i.PictureUri),
                Price = i.Price
            }).ToList();
        }

        var vm = new CatalogIndexViewModel()
        {
            CatalogItems = catalogItems,
            Brands = (await GetBrands()).ToList(),
            Types = (await GetTypes()).ToList(),
            BrandFilterApplied = brandId ?? 0,
            TypesFilterApplied = typeId ?? 0,
            PaginationInfo = new PaginationInfoViewModel()
            {
                ActualPage = pageIndex,
                ItemsPerPage = catalogItems.Count,
                TotalItems = totalItems,
                TotalPages = int.Parse(Math.Ceiling(((decimal)totalItems / itemsPage)).ToString())
            }
        };

        vm.PaginationInfo.Next = (vm.PaginationInfo.ActualPage == vm.PaginationInfo.TotalPages - 1) ? "is-disabled" : "";
        vm.PaginationInfo.Previous = (vm.PaginationInfo.ActualPage == 0) ? "is-disabled" : "";

        return vm;
    }

    public async Task<IEnumerable<SelectListItem>> GetBrands()
    {
        _logger.LogInformation("GetBrands called.");

        // Clanker-Claussen: full if/else (not a ternary) because the remote DTO's field is "Name" while the local entity's is "Brand" - different property to project from.
        List<SelectListItem> items;
        if (_featureToggles.UseCatalogMicroservice)
        {
            var brands = await _requester.RequestAsync<object, List<RemoteCatalogBrandDto>>("catalog.list-brands", new { }, TimeSpan.FromSeconds(5))
                ?? new();
            items = brands
                .Select(brand => new SelectListItem() { Value = brand.Id.ToString(), Text = brand.Name })
                .OrderBy(b => b.Text)
                .ToList();
        }
        else
        {
            var brands = await _brandRepository.ListAsync();
            items = brands
                .Select(brand => new SelectListItem() { Value = brand.Id.ToString(), Text = brand.Brand })
                .OrderBy(b => b.Text)
                .ToList();
        }

        var allItem = new SelectListItem() { Value = null, Text = "All", Selected = true };
        items.Insert(0, allItem);

        return items;
    }

    public async Task<IEnumerable<SelectListItem>> GetTypes()
    {
        _logger.LogInformation("GetTypes called.");

        // Clanker-Claussen: same reason as GetBrands above - remote DTO's "Name" vs local entity's "Type" field means the projection differs per branch.
        List<SelectListItem> items;
        if (_featureToggles.UseCatalogMicroservice)
        {
            var types = await _requester.RequestAsync<object, List<RemoteCatalogTypeDto>>("catalog.list-types", new { }, TimeSpan.FromSeconds(5))
                ?? new();
            items = types
                .Select(type => new SelectListItem() { Value = type.Id.ToString(), Text = type.Name })
                .OrderBy(t => t.Text)
                .ToList();
        }
        else
        {
            var types = await _typeRepository.ListAsync();
            items = types
                .Select(type => new SelectListItem() { Value = type.Id.ToString(), Text = type.Type })
                .OrderBy(t => t.Text)
                .ToList();
        }

        var allItem = new SelectListItem() { Value = null, Text = "All", Selected = true };
        items.Insert(0, allItem);

        return items;
    }
}
