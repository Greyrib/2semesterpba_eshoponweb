using System.Threading;
using System.Threading.Tasks;
using CatalogMicroservice.applicationCore.Entities;
using CatalogMicroservice.applicationCore.Specifications;
using Messaging;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;

namespace CatalogMicroservice.applicationCore.Services;

public class CreateCatalogItemHandler : IRequestHandler<CreateCatalogItemMessage, CreateCatalogItemResult>
{
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;

    public CreateCatalogItemHandler(IRepository<CatalogItem> itemRepository, IUriComposer uriComposer)
    {
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
    }

    public async Task<CreateCatalogItemResult> HandleAsync(CreateCatalogItemMessage request, CancellationToken ct)
    {
        var nameSpec = new CatalogItemNameSpecification(request.Name);
        var existingCount = await _itemRepository.CountAsync(nameSpec, ct);
        if (existingCount > 0)
        {
            return new CreateCatalogItemResult { DuplicateName = true };
        }

        var newItem = new CatalogItem(request.CatalogTypeId, request.CatalogBrandId, request.Description, request.Name, request.Price, request.PictureUri);
        newItem = await _itemRepository.AddAsync(newItem, ct);

        if (newItem.Id != 0)
        {
            newItem.UpdatePictureUri("eCatalog-item-default.png");
            await _itemRepository.UpdateAsync(newItem, ct);
        }

        return new CreateCatalogItemResult
        {
            CatalogItem = new CatalogItemDto
            {
                Id = newItem.Id,
                CatalogBrandId = newItem.CatalogBrandId,
                CatalogTypeId = newItem.CatalogTypeId,
                Description = newItem.Description,
                Name = newItem.Name,
                PictureUri = _uriComposer.ComposePicUri(newItem.PictureUri),
                Price = newItem.Price
            }
        };
    }
}
