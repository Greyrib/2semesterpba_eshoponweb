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

namespace Microsoft.eShopWeb.PublicApi.CatalogBrandEndpoints;

/// <summary>
/// List Catalog Brands
/// </summary>
public class CatalogBrandListEndpoint : IEndpoint<IResult, IRepository<CatalogBrand>>
{
    private readonly IMapper _mapper;
    private readonly IMessageRequester _requester;
    private readonly FeatureToggleOptions _featureToggles;

    public CatalogBrandListEndpoint(IMapper mapper, IMessageRequester requester, IOptions<FeatureToggleOptions> featureToggles)
    {
        _mapper = mapper;
        _requester = requester;
        _featureToggles = featureToggles.Value;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/catalog-brands",
            async (IRepository<CatalogBrand> catalogBrandRepository) =>
            {
                return await HandleAsync(catalogBrandRepository);
            })
           .Produces<ListCatalogBrandsResponse>()
           .WithTags("CatalogBrandEndpoints");
    }

    public async Task<IResult> HandleAsync(IRepository<CatalogBrand> catalogBrandRepository)
    {
        var response = new ListCatalogBrandsResponse();

        var items = _featureToggles.UseCatalogMicroservice
            ? await _requester.RequestAsync<object, List<CatalogBrandDto>>("catalog.list-brands", new { }, TimeSpan.FromSeconds(5)) ?? new()
            : (await catalogBrandRepository.ListAsync()).Select(_mapper.Map<CatalogBrandDto>).ToList();

        response.CatalogBrands.AddRange(items);

        return Results.Ok(response);
    }
}
