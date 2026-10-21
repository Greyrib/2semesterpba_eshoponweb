using System;
using System.Collections.Generic;
using CatalogMicroservice.publicAPI.CatalogBrandEndpoints;
using Microsoft.eShopWeb.CatalogMicroservice.PublicApi;

namespace Microsoft.eShopWeb.PublicApi.CatalogBrandEndpoints;

public class ListCatalogBrandsResponse : BaseResponse
{
    public ListCatalogBrandsResponse(Guid correlationId) : base(correlationId)
    {
    }

    public ListCatalogBrandsResponse()
    {
    }

    public List<CatalogBrandDto> CatalogBrands { get; set; } = new List<CatalogBrandDto>();
}
