using System;
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

public class ListCatalogItemsPagedHandler : IRequestHandler<CatalogItemsPagedQuery, CatalogItemsPagedResult>
{
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IUriComposer _uriComposer;

    public ListCatalogItemsPagedHandler(IRepository<CatalogItem> itemRepository, IUriComposer uriComposer)
    {
        _itemRepository = itemRepository;
        _uriComposer = uriComposer;
    }

    public async Task<CatalogItemsPagedResult> HandleAsync(CatalogItemsPagedQuery request, CancellationToken ct)
    {
        var filterSpec = new CatalogFilterSpecification(request.CatalogBrandId, request.CatalogTypeId);
        int totalItems = await _itemRepository.CountAsync(filterSpec, ct);

        var pagedSpec = new CatalogFilterPaginatedSpecification(
            skip: request.PageIndex * request.PageSize,
            take: request.PageSize,
            brandId: request.CatalogBrandId,
            typeId: request.CatalogTypeId);

        var items = await _itemRepository.ListAsync(pagedSpec, ct);

        var result = new CatalogItemsPagedResult
        {
            CatalogItems = items.Select(i => new CatalogItemDto
            {
                Id = i.Id,
                CatalogBrandId = i.CatalogBrandId,
                CatalogTypeId = i.CatalogTypeId,
                Description = i.Description,
                Name = i.Name,
                PictureUri = _uriComposer.ComposePicUri(i.PictureUri),
                Price = i.Price
            }).ToList(),
            TotalItems = totalItems
        };

        result.PageCount = request.PageSize > 0
            ? int.Parse(Math.Ceiling((decimal)totalItems / request.PageSize).ToString())
            : (totalItems > 0 ? 1 : 0);

        return result;
    }
}
