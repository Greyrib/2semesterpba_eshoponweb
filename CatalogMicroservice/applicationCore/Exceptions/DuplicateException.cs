using System;

namespace Microsoft.eShopWeb.CatalogMicroservice.Exceptions;

public class DuplicateException : Exception
{
    public DuplicateException(string message) : base(message)
    {

    }

}
