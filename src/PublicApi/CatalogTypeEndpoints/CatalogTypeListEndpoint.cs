using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.CatalogTypeEndpoints;

/// <summary>
/// List Catalog Types
/// </summary>
public class CatalogTypeListEndpoint : IEndpoint<IResult, IRepository<CatalogType>>
{
    private readonly IMapper _mapper;
    private readonly IMessageRequester _requester;
    private readonly FeatureToggleOptions _featureToggles;

    public CatalogTypeListEndpoint(IMapper mapper, IMessageRequester requester, IOptions<FeatureToggleOptions> featureToggles)
    {
        _mapper = mapper;
        _requester = requester;
        _featureToggles = featureToggles.Value;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/catalog-types",
            async (IRepository<CatalogType> catalogTypeRepository) =>
            {
                return await HandleAsync(catalogTypeRepository);
            })
            .Produces<ListCatalogTypesResponse>()
            .WithTags("CatalogTypeEndpoints");
    }

    public async Task<IResult> HandleAsync(IRepository<CatalogType> catalogTypeRepository)
    {
        var response = new ListCatalogTypesResponse();

        var items = _featureToggles.UseCatalogMicroservice
            ? await _requester.RequestAsync<object, List<CatalogTypeDto>>("catalog.list-types", new { }, TimeSpan.FromSeconds(5)) ?? new()
            : (await catalogTypeRepository.ListAsync()).Select(_mapper.Map<CatalogTypeDto>).ToList();

        response.CatalogTypes.AddRange(items);

        return Results.Ok(response);
    }
}
