var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .ConfigureHttpClient((context, handler) =>
    {
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(1);
    });

var app = builder.Build();

//app.MapGet("/", () => "Hello World!");
app.MapReverseProxy();

app.Run();
