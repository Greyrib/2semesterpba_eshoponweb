using System.Collections.Generic;
using CatalogMicroservice.publicAPI.CatalogItemEndpoints;

namespace CatalogMicroservice.applicationCore.Services;

// Request/response shapes for the monolith <-> microservice messaging operations.
// Mirrored by matching POCOs on the monolith side (same property names, JSON-compatible).
//
// Every operation gets its own distinct request type, even when the payload is just an int or nothing.
// The queue consumer resolves its handler by TRequest's C# type alone (not by queue name), so two
// operations sharing a primitive type (e.g. both using plain `int`) would collide and silently steal
// each other's messages. Wrapping avoids that regardless of how many operations end up needing an int/none.

public class GetCatalogItemByIdRequest
{
    public int CatalogItemId { get; set; }
}

public class DeleteCatalogItemRequest
{
    public int CatalogItemId { get; set; }
}

public class ListCatalogBrandsRequest
{
}

public class ListCatalogTypesRequest
{
}

public class ListCatalogItemsByIdsRequest
{
    public int[] CatalogItemIds { get; set; } = System.Array.Empty<int>();
}

public class CatalogItemsPagedQuery
{
    public int PageSize { get; set; }
    public int PageIndex { get; set; }
    public int? CatalogBrandId { get; set; }
    public int? CatalogTypeId { get; set; }
}

public class CatalogItemsPagedResult
{
    public List<CatalogItemDto> CatalogItems { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageCount { get; set; }
}

public class CreateCatalogItemMessage
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PictureUri { get; set; } = string.Empty;
    public int CatalogTypeId { get; set; }
    public int CatalogBrandId { get; set; }
}

public class CreateCatalogItemResult
{
    public CatalogItemDto? CatalogItem { get; set; }
    public bool DuplicateName { get; set; }
}

public class UpdateCatalogItemMessage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CatalogTypeId { get; set; }
    public int CatalogBrandId { get; set; }
}

public class UpdateCatalogItemDetailsMessage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
