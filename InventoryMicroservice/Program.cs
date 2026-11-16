using InventoryMicroservice.Contracts;
using InventoryMicroservice.Data;
using InventoryMicroservice.Handlers;
using Messaging;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<InventoryContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("InventoryConnection")));

builder.Services.AddMessaging(builder.Configuration.GetSection("Messaging").Bind);
builder.Services.AddRequestHandler<GetStockRequest, List<StockLevelDto>, GetStockHandler>("inventory.get-stock");
builder.Services.AddRequestHandler<DecrementStockRequest, DecrementStockResult, DecrementStockHandler>("inventory.decrement-stock");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<InventoryContext>();
    try
    {
        await InventoryContextSeed.SeedAsync(context, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred seeding the inventory DB.");
    }
}

app.UseSwagger();
app.UseSwaggerUI();

// Read-only HTTP view of the stock, handy for checking that checkouts actually subtract.
app.MapGet("/api/stock", async (InventoryContext context) =>
    await context.Stock.AsNoTracking()
        .OrderBy(s => s.CatalogItemId)
        .Select(s => new StockLevelDto { CatalogItemId = s.CatalogItemId, QuantityOnHand = s.QuantityOnHand })
        .ToListAsync());

app.Run();
