using InventoryMicroservice.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryMicroservice.Data;

public class InventoryContext : DbContext
{
    public InventoryContext(DbContextOptions<InventoryContext> options) : base(options) { }

    public DbSet<Stock> Stock => Set<Stock>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Stock>(stock =>
        {
            stock.ToTable("Stock");
            stock.HasKey(s => s.CatalogItemId);
            // The ID comes from the Catalog service, so the database must not generate it.
            stock.Property(s => s.CatalogItemId).ValueGeneratedNever();
            stock.ToTable(t => t.HasCheckConstraint("CK_Stock_QuantityOnHand_NotNegative", "[QuantityOnHand] >= 0"));
        });
    }
}
