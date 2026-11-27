using BasketMicroservice.Entities;
using Microsoft.EntityFrameworkCore;

namespace BasketMicroservice.Data;

public class EfBasketRepository : IBasketRepository
{
    private readonly BasketContext _db;

    public EfBasketRepository(BasketContext db) => _db = db;

    public Task<Basket?> GetByBuyerIdAsync(string buyerId, CancellationToken ct = default) =>
        _db.Baskets.Include(b => b.Items).FirstOrDefaultAsync(b => b.BuyerId == buyerId, ct);

    public Task<Basket?> GetByIdAsync(int basketId, CancellationToken ct = default) =>
        _db.Baskets.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == basketId, ct);

    public async Task AddAsync(Basket basket, CancellationToken ct = default) =>
        await _db.Baskets.AddAsync(basket, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public void Remove(Basket basket) => _db.Baskets.Remove(basket);
}
