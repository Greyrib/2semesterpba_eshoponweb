using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CatalogMicroservice.applicationCore.Entities;
using Messaging;
using CatalogMicroservice.CatalogMicroservice.CatalogTypeEndpoints;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;

namespace CatalogMicroservice.applicationCore.Services;

public class ListCatalogTypesHandler : IRequestHandler<ListCatalogTypesRequest, List<CatalogTypeDto>>
{
    private readonly IRepository<CatalogType> _typeRepository;

    public ListCatalogTypesHandler(IRepository<CatalogType> typeRepository)
    {
        _typeRepository = typeRepository;
    }

    public async Task<List<CatalogTypeDto>> HandleAsync(ListCatalogTypesRequest request, CancellationToken ct)
    {
        var items = await _typeRepository.ListAsync(ct);
        return items.Select(t => new CatalogTypeDto { Id = t.Id, Name = t.Type }).ToList();
    }
}
