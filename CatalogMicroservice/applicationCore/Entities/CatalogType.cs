using Microsoft.eShopWeb.CatalogMicroservice.Entities;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;

namespace CatalogMicroservice.applicationCore.Entities;

public class CatalogType : BaseEntity, IAggregateRoot
{
    public string Type { get; private set; }
    public CatalogType(string type)
    {
        Type = type;
    }
}
