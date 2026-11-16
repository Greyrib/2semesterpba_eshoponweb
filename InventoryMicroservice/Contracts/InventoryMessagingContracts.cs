namespace InventoryMicroservice.Contracts;

// Request/response shapes for the monolith <-> Inventory messaging operations.
// Mirrored by matching POCOs on the monolith side (same property names, JSON-compatible); deliberately not shared.
// Every operation has its own request type: the queue consumer picks the handler by C# type alone.

// Asks for the stock levels of the given catalog items (empty array = all items).
public class GetStockRequest
{
    public int[] CatalogItemIds { get; set; } = Array.Empty<int>();
}

// One catalog item's current stock level, as sent back to the caller.
public class StockLevelDto
{
    public int CatalogItemId { get; set; }
    public int QuantityOnHand { get; set; }
}

// Asks Inventory to subtract stock for every line of an order, all-or-nothing.
public class DecrementStockRequest
{
    public List<StockLine> Items { get; set; } = new();
}

// One order line in a DecrementStockRequest: which item, and how many to subtract.
public class StockLine
{
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; }
}

// Inventory's answer to a DecrementStockRequest: success, or which items were short.
public class DecrementStockResult
{
    public bool Success { get; set; }
    // Filled when Success is false: the items that didn't have enough stock (or have no stock record at all).
    public int[] OutOfStockCatalogItemIds { get; set; } = Array.Empty<int>();
}
