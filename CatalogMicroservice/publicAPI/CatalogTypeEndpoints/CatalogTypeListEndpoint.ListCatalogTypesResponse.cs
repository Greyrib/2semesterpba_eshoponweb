using System;
using System.Collections.Generic;
using CatalogMicroservice.CatalogMicroservice.CatalogTypeEndpoints;
using Microsoft.eShopWeb.CatalogMicroservice.PublicApi;

namespace Microsoft.eShopWeb.CatalogMicroservice.CatalogTypeEndpoints;

public class ListCatalogTypesResponse : BaseResponse
{
    public ListCatalogTypesResponse(Guid correlationId) : base(correlationId)
    {
    }

    public ListCatalogTypesResponse()
    {
    }

    public List<CatalogTypeDto> CatalogTypes { get; set; } = new List<CatalogTypeDto>();
}
