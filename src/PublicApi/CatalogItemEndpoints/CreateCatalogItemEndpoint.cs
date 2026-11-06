using System;
using System.Threading.Tasks;
using Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;

/// <summary>
/// Creates a new Catalog Item
/// </summary>
public class CreateCatalogItemEndpoint : IEndpoint<IResult, CreateCatalogItemRequest, IRepository<CatalogItem>>
{
    private readonly IUriComposer _uriComposer;
    private readonly IMessageRequester _requester;
    private readonly FeatureToggleOptions _featureToggles;

    public CreateCatalogItemEndpoint(IUriComposer uriComposer, IMessageRequester requester, IOptions<FeatureToggleOptions> featureToggles)
    {
        _uriComposer = uriComposer;
        _requester = requester;
        _featureToggles = featureToggles.Value;
    }

    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/catalog-items",
            [Authorize(Roles = BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS, AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (CreateCatalogItemRequest request, IRepository<CatalogItem> itemRepository) =>
            {
                return await HandleAsync(request, itemRepository);
            })
            .Produces<CreateCatalogItemResponse>()
            .WithTags("CatalogItemEndpoints");
    }

    public async Task<IResult> HandleAsync(CreateCatalogItemRequest request, IRepository<CatalogItem> itemRepository)
    {
        var response = new CreateCatalogItemResponse(request.CorrelationId());

        if (_featureToggles.UseCatalogMicroservice)
        {
            // Clanker-Claussen: duplicate-name check + create are one call here (microservice does both against its own DB), unlike the 2 separate repo calls below - avoids a check-then-create race across 2 round-trips.
            var message = new
            {
                request.Name,
                request.Description,
                request.Price,
                request.PictureUri,
                request.CatalogTypeId,
                request.CatalogBrandId
            };
            var result = await _requester.RequestAsync<object, CreateCatalogItemResult>("catalog.create-item", message, TimeSpan.FromSeconds(5));
            if (result is null || result.DuplicateName)
            {
                throw new DuplicateException($"A catalogItem with name {request.Name} already exists");
            }

            response.CatalogItem = result.CatalogItem;
            return Results.Created($"api/catalog-items/{result.CatalogItem?.Id}", response);
        }

        var catalogItemNameSpecification = new CatalogItemNameSpecification(request.Name);
        var existingCataloogItem = await itemRepository.CountAsync(catalogItemNameSpecification);
        if (existingCataloogItem > 0)
        {
            throw new DuplicateException($"A catalogItem with name {request.Name} already exists");
        }

        var newItem = new CatalogItem(request.CatalogTypeId, request.CatalogBrandId, request.Description, request.Name, request.Price, request.PictureUri);
        newItem = await itemRepository.AddAsync(newItem);

        if (newItem.Id != 0)
        {
            //We disabled the upload functionality and added a default/placeholder image to this sample due to a potential security risk
            //  pointed out by the community. More info in this issue: https://github.com/dotnet-architecture/eShopOnWeb/issues/537
            //  In production, we recommend uploading to a blob storage and deliver the image via CDN after a verification process.

            newItem.UpdatePictureUri("eCatalog-item-default.png");
            await itemRepository.UpdateAsync(newItem);
        }

        var dto = new CatalogItemDto
        {
            Id = newItem.Id,
            CatalogBrandId = newItem.CatalogBrandId,
            CatalogTypeId = newItem.CatalogTypeId,
            Description = newItem.Description,
            Name = newItem.Name,
            PictureUri = _uriComposer.ComposePicUri(newItem.PictureUri),
            Price = newItem.Price
        };
        response.CatalogItem = dto;
        return Results.Created($"api/catalog-items/{dto.Id}", response);
    }
}
