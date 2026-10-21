using Ardalis.Specification.EntityFrameworkCore;
using CatalogMicroservice.infrastructure.Data;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Data;

public class EfRepository<T> : RepositoryBase<T>, IReadRepository<T>, IRepository<T> where T : class, IAggregateRoot
{
    public EfRepository(CatalogContext dbContext) : base(dbContext)
    {
    }
}
