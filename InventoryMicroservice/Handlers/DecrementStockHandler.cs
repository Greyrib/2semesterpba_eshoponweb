using InventoryMicroservice.Contracts;
using InventoryMicroservice.Data;
using Messaging;
using Microsoft.EntityFrameworkCore;

namespace InventoryMicroservice.Handlers;

public class DecrementStockHandler : IRequestHandler<DecrementStockRequest, DecrementStockResult>
{
    private readonly InventoryContext _context;
    private readonly ILogger<DecrementStockHandler> _logger;

    public DecrementStockHandler(InventoryContext context, ILogger<DecrementStockHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DecrementStockResult> HandleAsync(DecrementStockRequest request, CancellationToken ct)
    {
        var lines = request.Items
            .Where(i => i.Quantity > 0)
            .GroupBy(i => i.CatalogItemId)
            .Select(g => new StockLine { CatalogItemId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();

        // All-or-nothing: if any line is short, the transaction is rolled back and no stock changes.
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var outOfStock = new List<int>();

        foreach (var line in lines)
        {
            // Check and subtract in one statement, so two checkouts can't both take the last item.
            // 0 rows changed = not enough stock (or no stock record for that item).
            var rowsChanged = await _context.Stock
                .Where(s => s.CatalogItemId == line.CatalogItemId && s.QuantityOnHand >= line.Quantity)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.QuantityOnHand, s => s.QuantityOnHand - line.Quantity), ct);

            if (rowsChanged == 0)
            {
                outOfStock.Add(line.CatalogItemId);
            }
        }

        if (outOfStock.Count > 0)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogInformation("Stock decrement rejected; not enough stock for catalog items {Ids}", outOfStock);
            return new DecrementStockResult { Success = false, OutOfStockCatalogItemIds = outOfStock.ToArray() };
        }

        await transaction.CommitAsync(ct);
        _logger.LogInformation("Stock decremented for {Count} catalog item(s)", lines.Count);
        return new DecrementStockResult { Success = true };
    }
}
