using System.Threading;
using System.Threading.Tasks;
using CatalogMicroservice.applicationCore.Entities;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;
using Messaging;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;

namespace CatalogMicroservice.applicationCore.Services;

// Answers the monolith's "get catalog item by id" request over messaging.
public class GetCatalogItemByIdHandler : IRequestHandler<GetCatalogItemByIdRequest, CatalogItemDto?>
{
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;

    public GetCatalogItemByIdHandler(IRepository<CatalogItem> itemRepository, IUriComposer uriComposer)
    {
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
    }

    public async Task<CatalogItemDto?> HandleAsync(GetCatalogItemByIdRequest request, CancellationToken ct)
    {
        var item = await _itemRepository.GetByIdAsync(request.CatalogItemId, ct);
        if (item is null) return null;

        return new CatalogItemDto
        {
            Id = item.Id,
            CatalogBrandId = item.CatalogBrandId,
            CatalogTypeId = item.CatalogTypeId,
            Description = item.Description,
            Name = item.Name,
            PictureUri = _uriComposer.ComposePicUri(item.PictureUri),
            Price = item.Price
        };
    }
}
