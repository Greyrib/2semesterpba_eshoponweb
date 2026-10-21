
using CatalogMicroservice.BlazorShared.Models;

namespace CatalogMicroservice.BlazorShared.Models;

public class CreateCatalogItemResponse
{
    public CatalogItem CatalogItem { get; set; } = new CatalogItem();
}
