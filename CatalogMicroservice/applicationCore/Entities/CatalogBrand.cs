using Microsoft.eShopWeb.CatalogMicroservice.Entities;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;

namespace CatalogMicroservice.applicationCore.Entities;

public class CatalogBrand : BaseEntity, IAggregateRoot
{
    public string Brand { get; private set; }
    public CatalogBrand(string brand)
    {
        Brand = brand;
    }
}
