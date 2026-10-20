using System;
using Microsoft.eShopWeb.CatalogMicroservice.PublicApi;

namespace Microsoft.eShopWeb.CatalogMicroservice.CatalogItemEndpoints;

public class DeleteCatalogItemResponse : BaseResponse
{
    public DeleteCatalogItemResponse(Guid correlationId) : base(correlationId)
    {
    }

    public DeleteCatalogItemResponse()
    {
    }

    public string Status { get; set; } = "Deleted";
}
