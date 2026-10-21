namespace CatalogMicroservice.RabbitMQ;

public class MessageType
{
    public object Message { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public MessageType() { }
}
