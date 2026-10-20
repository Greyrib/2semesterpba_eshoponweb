using Ardalis.Specification;
using CatalogMicroservice.applicationCore.Entities;

namespace CatalogMicroservice.applicationCore.Specifications;

public class CatalogItemNameSpecification : Specification<CatalogItem>
{
    public CatalogItemNameSpecification(string catalogItemName)
    {
        Query.Where(item => catalogItemName == item.Name);
    }
}
