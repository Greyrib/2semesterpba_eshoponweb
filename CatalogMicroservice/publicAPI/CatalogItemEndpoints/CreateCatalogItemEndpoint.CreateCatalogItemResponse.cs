using System;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;
using Microsoft.eShopWeb.CatalogMicroservice.PublicApi;

namespace Microsoft.eShopWeb.CatalogMicroservice.CatalogItemEndpoints;

public class CreateCatalogItemResponse : BaseResponse
{
    public CreateCatalogItemResponse(Guid correlationId) : base(correlationId)
    {
    }

    public CreateCatalogItemResponse()
    {
    }

    public CatalogItemDto CatalogItem { get; set; }
}
