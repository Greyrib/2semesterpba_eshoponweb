using System.Threading;
using System.Threading.Tasks;
using CatalogMicroservice.applicationCore.Entities;
using Messaging;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;

namespace CatalogMicroservice.applicationCore.Services;

public class UpdateCatalogItemHandler : IRequestHandler<UpdateCatalogItemMessage, CatalogItemDto?>
{
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;

    public UpdateCatalogItemHandler(IRepository<CatalogItem> itemRepository, IUriComposer uriComposer)
    {
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
    }

    public async Task<CatalogItemDto?> HandleAsync(UpdateCatalogItemMessage request, CancellationToken ct)
    {
        var existingItem = await _itemRepository.GetByIdAsync(request.Id, ct);
        if (existingItem is null) return null;

        var details = new CatalogItem.CatalogItemDetails(request.Name, request.Description, request.Price);
        existingItem.UpdateDetails(details);
        existingItem.UpdateBrand(request.CatalogBrandId);
        existingItem.UpdateType(request.CatalogTypeId);

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
