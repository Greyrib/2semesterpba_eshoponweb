namespace Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;

// Mirrors CatalogMicroservice's CreateCatalogItemResult (same property names, JSON-compatible).
public class CreateCatalogItemResult
{
    public CatalogItemDto? CatalogItem { get; set; }
    public bool DuplicateName { get; set; }
}
