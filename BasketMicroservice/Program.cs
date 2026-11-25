using BasketMicroservice.Data;
using BasketMicroservice.RabbitMQ;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BasketContext>(options => options.UseInMemoryDatabase("BasketMicroservice"));
builder.Services.AddScoped<IBasketRepository, EfBasketRepository>();

builder.Services.Configure<BasketBrokerOptions>(builder.Configuration.GetSection("Messaging"));
builder.Services.AddHostedService<BasketMessageWorker>();

var app = builder.Build();

app.Logger.LogInformation("BasketMicroservice started, listening on '{Exchange}' ({Delete}/{Transfer}) and '{Queries}'",
    BasketMessagingConstants.CommandsExchange, BasketMessagingConstants.DeleteBasketQueue,
    BasketMessagingConstants.TransferBasketQueue, BasketMessagingConstants.QueriesQueue);

app.Run();

public partial class Program { }
