using System;
using System.Threading.Tasks;
using RabbitMQ.AMQP.Client;

namespace Microsoft.eShopWeb.ApplicationCore.BasketMessaging;

/// <summary>
/// BasketQueue/BasketRpcClient connect eagerly at app startup (unlike the generic
/// Messaging library's RabbitMqConnection, which connects lazily on first send - that's
/// why it never races RabbitMQ's own startup). Docker's depends_on/healthcheck narrows
/// the race but isn't guaranteed across every compose tool/version, so retry here too.
/// </summary>
internal static class BasketBrokerRetry
{
    public static async Task<IConnection> ConnectAsync(IEnvironment environment, int maxAttempts = 10, int delayMs = 2000)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await environment.CreateConnectionAsync();
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                Console.Error.WriteLine($"[BasketMessaging] Connection attempt {attempt}/{maxAttempts} to RabbitMQ failed ({ex.Message}), retrying in {delayMs}ms...");
                await Task.Delay(delayMs);
            }
        }
    }
}
