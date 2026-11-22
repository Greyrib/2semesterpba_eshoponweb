using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.PublicApi.Messaging;
using Microsoft.Extensions.Configuration;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;

/// <summary>
/// Get a Catalog Item by Id
/// </summary>
public class CatalogItemGetByIdEndpoint : IEndpoint<IResult, GetByIdCatalogItemRequest, IRepository<CatalogItem>>
{
    private readonly CatalogRpcClient _rpc;
    private readonly IConfiguration _config;
    private readonly IUriComposer _uriComposer;

    public CatalogItemGetByIdEndpoint(IUriComposer uriComposer, CatalogRpcClient rpc, IConfiguration config)
    {
        _uriComposer = uriComposer;
        _rpc = rpc;
        _config = config;
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

        if (_config.GetValue<bool>("FeatureFlags:UseCatalogMicroservice"))
        {
            try
            {
                var dto = await _rpc.RequestAsync<object, CatalogItemDto>(
                    "GetCatalogItemById", new { Id = request.CatalogItemId });
                if (dto is null) return Results.NotFound();

                response.CatalogItem = dto;
                return Results.Ok(response);
            }
            catch (TimeoutException)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        var item = await itemRepository.GetByIdAsync(request.CatalogItemId);
        if (item is null)
            return Results.NotFound();

        response.CatalogItem = new CatalogItemDto
        {
            Id = item.Id,
            CatalogBrandId = item.CatalogBrandId,
            CatalogTypeId = item.CatalogTypeId,
            Description = item.Description,
            Name = item.Name,
            PictureUri = _uriComposer.ComposePicUri(item.PictureUri),
            Price = item.Price
        };
        return Results.Ok(response);
    }
}
