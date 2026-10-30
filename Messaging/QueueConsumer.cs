using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Messaging;

public class QueueConsumer<T> : BackgroundService
{
    private readonly string _queue;
    private readonly RabbitMqConnection _rabbit;
    private readonly IServiceScopeFactory _scopes;

    public QueueConsumer (string queue, RabbitMqConnection rabbit, IServiceScopeFactory scopes)
    {
        _queue = queue;
        _rabbit = rabbit;
        _scopes = scopes;
    }

    protected override async Task ExecuteAsync (CancellationToken ct)
    {
        await Task.Yield();

        await _rabbit.DeclareQueueAsync(_queue);
        var conn = await _rabbit.GetConnectionAsync();

        await conn.ConsumerBuilder()
            .Queue(_queue)
            .InitialCredits(10)
            .MessageHandler(async (ctx, message) =>
            {
                try
                {
                    var body = (byte[])message.Body();
                    var msg = JsonSerializer.Deserialize<T>(body);

                    using var scope = _scopes.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<T>>();
                    await handler.HandleAsync(msg!, ct);

                    ctx.Accept();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex}, Failed handling message on queue {_queue}");
                    ctx.Discard();
                }
            })
            .BuildAndStartAsync();

        await Task.Delay(Timeout.Infinite, ct);
    }
}
