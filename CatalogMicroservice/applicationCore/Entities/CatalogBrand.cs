using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;
using Microsoft.eShopWeb.CatalogMicroservice.Entities;

namespace CatalogMicroservice.applicationCore.Entities;

public class CatalogBrand : BaseEntity, IAggregateRoot
{
    public string Brand { get; private set; }
    public CatalogBrand(string brand)
    {
        Brand = brand;
    }
}
