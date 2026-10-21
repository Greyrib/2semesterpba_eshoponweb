using Ardalis.Specification;
using Microsoft.eShopWeb.CatalogMicroservice.Interfaces;

namespace Microsoft.eShopWeb.ApplicationCore.Interfaces;

public interface IReadRepository<T> : IReadRepositoryBase<T> where T : class, IAggregateRoot
{
}
