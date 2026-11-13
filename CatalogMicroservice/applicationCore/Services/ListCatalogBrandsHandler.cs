using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CatalogMicroservice.applicationCore.Entities;
using Messaging;
using CatalogMicroservice.publicAPI.CatalogBrandEndpoints;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;

namespace CatalogMicroservice.applicationCore.Services;

public class ListCatalogBrandsHandler : IRequestHandler<ListCatalogBrandsRequest, List<CatalogBrandDto>>
{
    private readonly IRepository<CatalogBrand> _brandRepository;

    public ListCatalogBrandsHandler(IRepository<CatalogBrand> brandRepository)
    {
        _brandRepository = brandRepository;
    }

    public async Task<List<CatalogBrandDto>> HandleAsync(ListCatalogBrandsRequest request, CancellationToken ct)
    {
        var items = await _brandRepository.ListAsync(ct);
        return items.Select(b => new CatalogBrandDto { Id = b.Id, Name = b.Brand }).ToList();
    }
}
