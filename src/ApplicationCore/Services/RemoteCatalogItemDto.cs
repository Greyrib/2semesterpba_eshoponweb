using System.Collections.Generic;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

// Mirrors CatalogMicroservice's CatalogItemDto for the fields the monolith's non-PublicApi
// callers (OrderService, Web's view-model services) actually need. JSON deserialization
// ignores the extra fields the microservice sends that aren't declared here.
public class RemoteCatalogItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PictureUri { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class RemoteCatalogItemsPagedResult
{
    public List<RemoteCatalogItemDto> CatalogItems { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageCount { get; set; }
}

public class RemoteCatalogBrandDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RemoteCatalogTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
