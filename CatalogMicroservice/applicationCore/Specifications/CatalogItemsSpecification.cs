using System;
using System.Linq;
using Ardalis.Specification;
using CatalogMicroservice.applicationCore.Entities;

namespace CatalogMicroservice.applicationCore.Specifications;

public class CatalogItemsSpecification : Specification<CatalogItem>
{
    public CatalogItemsSpecification(params int[] ids)
    {
        Query.Where(c => ids.Contains(c.Id));
    }
}
