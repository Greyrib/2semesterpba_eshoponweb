using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CatalogMicroservice.applicationCore.Entities;
using CatalogMicroservice.applicationCore.Specifications;
using Messaging;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;

namespace CatalogMicroservice.applicationCore.Services;

public class ListCatalogItemsByIdsHandler : IRequestHandler<ListCatalogItemsByIdsRequest, List<CatalogItemDto>>
{
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;

    public ListCatalogItemsByIdsHandler(IRepository<CatalogItem> itemRepository, IUriComposer uriComposer)
    {
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
    }

    public async Task<List<CatalogItemDto>> HandleAsync(ListCatalogItemsByIdsRequest request, CancellationToken ct)
    {
        var spec = new CatalogItemsSpecification(request.CatalogItemIds);
        var items = await _itemRepository.ListAsync(spec, ct);

        return items.Select(i => new CatalogItemDto
        {
            Id = i.Id,
            CatalogBrandId = i.CatalogBrandId,
            CatalogTypeId = i.CatalogTypeId,
            Description = i.Description,
            Name = i.Name,
            PictureUri = _uriComposer.ComposePicUri(i.PictureUri),
            Price = i.Price
        }).ToList();
    }
}
