using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class OutOfStockException : Exception
{
    public int[] CatalogItemIds { get; }

    public OutOfStockException(int[] catalogItemIds)
        : base($"Not enough stock for catalog item(s): {string.Join(", ", catalogItemIds)}")
    {
        CatalogItemIds = catalogItemIds;
    }
}
