using System;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;
using Microsoft.eShopWeb.CatalogMicroservice.PublicApi;

namespace Microsoft.eShopWeb.CatalogMicroservice.CatalogItemEndpoints;

public class UpdateCatalogItemResponse : BaseResponse
{
    public UpdateCatalogItemResponse(Guid correlationId) : base(correlationId)
    {
    }

    public UpdateCatalogItemResponse()
    {
    }

    public CatalogItemDto CatalogItem { get; set; }
}
