using System.Threading;
using System.Threading.Tasks;
using CatalogMicroservice.applicationCore.Entities;
using Messaging;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;

namespace CatalogMicroservice.applicationCore.Services;

// Mirrors Web's CatalogItemViewModelService.UpdateCatalogItem: only Name/Price change, Description/Brand/Type stay as-is.
public class UpdateCatalogItemDetailsHandler : IRequestHandler<UpdateCatalogItemDetailsMessage, CatalogItemDto?>
{
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;

    public UpdateCatalogItemDetailsHandler(IRepository<CatalogItem> itemRepository, IUriComposer uriComposer)
    {
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
    }

    public async Task<CatalogItemDto?> HandleAsync(UpdateCatalogItemDetailsMessage request, CancellationToken ct)
    {
        var existingItem = await _itemRepository.GetByIdAsync(request.Id, ct);
        if (existingItem is null) return null;

        var details = new CatalogItem.CatalogItemDetails(request.Name, existingItem.Description, request.Price);
        existingItem.UpdateDetails(details);
        await _itemRepository.UpdateAsync(existingItem, ct);

        return new CatalogItemDto
        {
            Id = existingItem.Id,
            CatalogBrandId = existingItem.CatalogBrandId,
            CatalogTypeId = existingItem.CatalogTypeId,
            Description = existingItem.Description,
            Name = existingItem.Name,
            PictureUri = _uriComposer.ComposePicUri(existingItem.PictureUri),
            Price = existingItem.Price
        };
    }
}
