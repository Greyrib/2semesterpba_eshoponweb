using BasketMicroservice.Entities;
using Microsoft.EntityFrameworkCore;

namespace BasketMicroservice.Data;

public class BasketContext : DbContext
{
    public BasketContext(DbContextOptions<BasketContext> options) : base(options) { }

    public DbSet<Basket> Baskets => Set<Basket>();
    public DbSet<BasketItem> BasketItems => Set<BasketItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Basket>(b =>
        {
            b.Navigation(x => x.Items).Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);
            b.Property(x => x.BuyerId).IsRequired().HasMaxLength(256);
        });
    }
}
