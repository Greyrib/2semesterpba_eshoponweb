using System;

namespace Microsoft.eShopWeb.ApplicationCore.Exceptions;

public class InventoryUnavailableException : Exception
{
    public InventoryUnavailableException(Exception? innerException = null)
        : base("The inventory service did not answer, so stock could not be checked.", innerException)
    {
    }
}
