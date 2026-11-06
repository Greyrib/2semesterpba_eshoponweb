using System.Collections.Generic;

namespace Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;

// Mirrors CatalogMicroservice's CatalogItemsPagedResult (same property names, JSON-compatible).
public class CatalogItemsPagedResult
{
    public List<CatalogItemDto> CatalogItems { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageCount { get; set; }
}
