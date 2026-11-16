using System;
using System.Threading.Tasks;
using Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;

/// <summary>
/// Get a Catalog Item by Id
/// </summary>
public class CatalogItemGetByIdEndpoint : IEndpoint<IResult, GetByIdCatalogItemRequest, IRepository<CatalogItem>>
{
    private readonly IUriComposer _uriComposer;
    private readonly IMessageRequester _requester;
    private readonly FeatureToggleOptions _featureToggles;

    public CatalogItemGetByIdEndpoint(IUriComposer uriComposer, IMessageRequester requester, IOptions<FeatureToggleOptions> featureToggles)
    {
        _uriComposer = uriComposer;
        _requester = requester;
        _featureToggles = featureToggles.Value;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/catalog-items/{catalogItemId}",
            async (int catalogItemId, IRepository<CatalogItem> itemRepository) =>
            {
                return await HandleAsync(new GetByIdCatalogItemRequest(catalogItemId), itemRepository);
            })
            .Produces<GetByIdCatalogItemResponse>()
            .WithTags("CatalogItemEndpoints");
    }

    public async Task<IResult> HandleAsync(GetByIdCatalogItemRequest request, IRepository<CatalogItem> itemRepository)
    {
        var response = new GetByIdCatalogItemResponse(request.CorrelationId());

        var catalogItem = _featureToggles.UseCatalogMicroservice
            ? await _requester.RequestAsync<object, CatalogItemDto>("catalog.get-by-id", new { CatalogItemId = request.CatalogItemId }, TimeSpan.FromSeconds(5))
            : await GetFromLocalDatabase(request.CatalogItemId, itemRepository);

        if (catalogItem is null)
            return Results.NotFound();

        response.CatalogItem = catalogItem;
        return Results.Ok(response);
    }

    // Clanker-Claussen: pulled into its own method so the local-DB branch can sit in the ternary above, producing the same CatalogItemDto? as the messaging call.
    private async Task<CatalogItemDto?> GetFromLocalDatabase(int catalogItemId, IRepository<CatalogItem> itemRepository)
    {
        var item = await itemRepository.GetByIdAsync(catalogItemId);
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
