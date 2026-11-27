using BasketMicroservice.Entities;

namespace BasketMicroservice.Data;

public interface IBasketRepository
{
    Task<Basket?> GetByBuyerIdAsync(string buyerId, CancellationToken ct = default);
    Task<Basket?> GetByIdAsync(int basketId, CancellationToken ct = default);
    Task AddAsync(Basket basket, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    void Remove(Basket basket);
}
