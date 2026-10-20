using Microsoft.eShopWeb.CatalogMicroservice.PublicApi;

namespace Microsoft.eShopWeb.CatalogMicroservice.CatalogItemEndpoints;

public class DeleteCatalogItemRequest : BaseRequest
{
    public int CatalogItemId { get; init; }

    public DeleteCatalogItemRequest(int catalogItemId)
    {
        CatalogItemId = catalogItemId;
    }
}
