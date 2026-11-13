using System;
using Ardalis.GuardClauses;
using Messaging;
using Microsoft.eShopWeb.ApplicationCore;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Services;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.eShopWeb.Web.ViewModels;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.Web.Services;

public class CatalogItemViewModelService : ICatalogItemViewModelService
{
    private readonly IRepository<CatalogItem> _catalogItemRepository;
    private readonly IMessageRequester _requester;
    private readonly FeatureToggleOptions _featureToggles;

    public CatalogItemViewModelService(IRepository<CatalogItem> catalogItemRepository, IMessageRequester requester, IOptions<FeatureToggleOptions> featureToggles)
    {
        _catalogItemRepository = catalogItemRepository;
        _requester = requester;
        _featureToggles = featureToggles.Value;
    }

    public async Task UpdateCatalogItem(CatalogItemViewModel viewModel)
    {
        if (_featureToggles.UseCatalogMicroservice)
        {
            var message = new { Id = viewModel.Id, Name = viewModel.Name, Price = viewModel.Price };
            var updated = await _requester.RequestAsync<object, RemoteCatalogItemDto>("catalog.update-item-details", message, TimeSpan.FromSeconds(5));
            Guard.Against.Null(updated, nameof(updated));
            return;
        }

        var existingCatalogItem = await _catalogItemRepository.GetByIdAsync(viewModel.Id);

        Guard.Against.Null(existingCatalogItem, nameof(existingCatalogItem));

        CatalogItem.CatalogItemDetails details = new(viewModel.Name, existingCatalogItem.Description, viewModel.Price);
        existingCatalogItem.UpdateDetails(details);
        await _catalogItemRepository.UpdateAsync(existingCatalogItem);
    }
}
