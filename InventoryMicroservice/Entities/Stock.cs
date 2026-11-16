namespace InventoryMicroservice.Entities;

// One row per catalog item. CatalogItemId is only a reference by ID to the Catalog service;
// Inventory knows nothing else about the item (name, price, ...).
public class Stock
{
    public int CatalogItemId { get; set; }
    public int QuantityOnHand { get; set; }
}
