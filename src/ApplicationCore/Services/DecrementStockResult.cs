using System;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

// InventoryMicroservice's answer to "inventory.decrement-stock": success, or which items were short.
public class DecrementStockResult
{
    public bool Success { get; set; }
    public int[] OutOfStockCatalogItemIds { get; set; } = Array.Empty<int>();
}
