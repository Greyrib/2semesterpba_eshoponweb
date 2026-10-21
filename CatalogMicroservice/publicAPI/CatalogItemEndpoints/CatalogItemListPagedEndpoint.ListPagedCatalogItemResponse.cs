using System;
using System.Collections.Generic;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;
using Microsoft.eShopWeb.CatalogMicroservice.PublicApi;

namespace Microsoft.eShopWeb.CatalogMicroservice.CatalogItemEndpoints;

public class ListPagedCatalogItemResponse : BaseResponse
{
    public ListPagedCatalogItemResponse(Guid correlationId) : base(correlationId)
    {
    }

    public ListPagedCatalogItemResponse()
    {
    }

    public List<CatalogItemDto> CatalogItems { get; set; } = new List<CatalogItemDto>();
    public int PageCount { get; set; }
}
