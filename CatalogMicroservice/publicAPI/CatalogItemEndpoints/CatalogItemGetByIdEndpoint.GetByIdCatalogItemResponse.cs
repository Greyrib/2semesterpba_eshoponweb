using System;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;
using Microsoft.eShopWeb.CatalogMicroservice.PublicApi;

namespace Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;

public class GetByIdCatalogItemResponse : BaseResponse
{
    public GetByIdCatalogItemResponse(Guid correlationId) : base(correlationId)
    {
    }

    public GetByIdCatalogItemResponse()
    {
    }

    public CatalogItemDto CatalogItem { get; set; }
}
