using InventoryMicroservice.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryMicroservice.Data;

public static class InventoryContextSeed
{
    private const int SeededCatalogItemCount = 12; // Matches the 12 items seeded by the Catalog service/monolith (IDs 1-12)
    private const int InitialQuantity = 10;

    public static async Task SeedAsync(InventoryContext context, ILogger logger, int retry = 0)
    {
        try
        {
            await context.Database.MigrateAsync();

            if (!await context.Stock.AnyAsync())
            {
                var stock = Enumerable.Range(1, SeededCatalogItemCount)
                    .Select(id => new Stock { CatalogItemId = id, QuantityOnHand = InitialQuantity });
                await context.Stock.AddRangeAsync(stock);
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            // SQL Server in Docker can take a while to accept connections.
            if (retry >= 10) throw;
            logger.LogWarning(ex, "Seeding inventory failed (attempt {Attempt}), retrying in 5 seconds", retry + 1);
            await Task.Delay(TimeSpan.FromSeconds(5));
            await SeedAsync(context, logger, retry + 1);
        }
    }
}
