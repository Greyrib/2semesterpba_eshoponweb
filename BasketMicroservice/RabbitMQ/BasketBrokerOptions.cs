namespace BasketMicroservice.RabbitMQ;

/// <summary>
/// This project is deliberately standalone (no reference to the ApplicationCore/Messaging
/// projects), so it keeps its own copy of these settings rather than sharing a class with
/// BasketQueue/BasketRpcClient's Messaging.MessagingOptions. Bound from config section
/// "Messaging" - same shape, same broker, just not the same compiled type.
/// </summary>
public class BasketBrokerOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
