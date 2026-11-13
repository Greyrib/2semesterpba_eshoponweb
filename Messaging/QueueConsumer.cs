using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging;

public class QueueConsumer<T> : BackgroundService
{
    private readonly string _queue;
    private readonly RabbitMqConnection _rabbit;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<QueueConsumer<T>> _logger;

    public QueueConsumer (string queue, RabbitMqConnection rabbit, IServiceScopeFactory scopes, ILogger<QueueConsumer<T>> logger)
    {
        _queue = queue;
        _rabbit = rabbit;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync (CancellationToken ct)
    {
        await Task.Yield();

        while (true)
        {
            try
            {
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
                            var msg = JsonSerializer.Deserialize<T>(body)
                                ?? throw new JsonException("Message body deserialized to null.");

                            using var scope = _scopes.CreateScope();
                            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<T>>();
                            await handler.HandleAsync(msg, ct);

                            ctx.Accept();
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested)
                        {
                            // Interrupted by shutdown, not a failed message: let it be processed again.
                            ctx.Requeue();
                        }
                        catch (Exception ex)
                        {
                            // Dead-lettered by the broker to "<queue>.dead", for manual action.
                            _logger.LogError(ex, "Unhandled failure on queue {Queue}; moving message to {DeadQueue}", _queue, $"{_queue}.dead");
                            ctx.Discard();
                        }
                    })
                    .BuildAndStartAsync();

                break;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Could not start consumer for queue {Queue}, retrying in 5 seconds", _queue);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        await Task.Delay(Timeout.Infinite, ct);
    }
}
