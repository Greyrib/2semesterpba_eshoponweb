using InventoryMicroservice.Contracts;
using InventoryMicroservice.Data;
using Messaging;
using Microsoft.EntityFrameworkCore;

namespace InventoryMicroservice.Handlers;

public class GetStockHandler : IRequestHandler<GetStockRequest, List<StockLevelDto>>
{
    private readonly InventoryContext _context;

    public GetStockHandler(InventoryContext context)
    {
        _context = context;
    }

    public async Task<List<StockLevelDto>> HandleAsync(GetStockRequest request, CancellationToken ct)
    {
        var query = _context.Stock.AsNoTracking();
        if (request.CatalogItemIds.Length > 0)
        {
            query = query.Where(s => request.CatalogItemIds.Contains(s.CatalogItemId));
        }

        return await query
            .OrderBy(s => s.CatalogItemId)
            .Select(s => new StockLevelDto { CatalogItemId = s.CatalogItemId, QuantityOnHand = s.QuantityOnHand })
            .ToListAsync(ct);
    }
}
