using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;

namespace CatalogMicroservice.RabbitMQ;

public class MessageConsumer
{
    const string brokerUri = "amqp://guest:guest@localhost:5672/%2f";
    string queueName;

    public MessageConsumer(string QueueName)
    {
        if (QueueName.IsNullOrEmpty())
        {
            Console.WriteLine("MAKE SURE TO SET QUEUENAME");
            return;
        }
        queueName = QueueName;
    }

    public async Task Consume()
    {
        Console.WriteLine("Message Consumer was created.");

        ConnectionSettings settings = ConnectionSettingsBuilder.Create()
        .Uri(new Uri(brokerUri))
        .ContainerId("catalogMicroservice")
        .Build();

        IEnvironment environment = AmqpEnvironment.Create(settings);
        IConnection connection = await environment.CreateConnectionAsync();

        try
        {
            IManagement management = connection.Management();
            IQueueSpecification queueSpec = management.Queue(queueName).Type(QueueType.QUORUM);
            await queueSpec.DeclareAsync();

            IConsumer consumer = await connection.ConsumerBuilder()
            .Queue(queueName)
            .MessageHandler((ctx, message) =>
            {
                Console.WriteLine($"Received a message: {Encoding.UTF8.GetString(message.Body()!)}");
                ctx.Accept();
                return Task.CompletedTask;
            })
            .BuildAndStartAsync();
        }
        finally
        {
            await connection.CloseAsync();
            await environment.CloseAsync();
        }
    }


        //const string brokerUri = "amqp://guest:guest@localhost:5672/%2f";
        //const string queueName = "cloudsync";

        //readonly ConnectionSettings settings = ConnectionSettingsBuilder.Create()
        //    .Uri(new Uri(brokerUri))
        //    .ContainerId("cloud-backend")
        //    .Build();

        //IEnvironment? environment;
        //IConnection? connection;
        //IPublisher? publisher;

        //readonly SemaphoreSlim initLock = new(1, 1);


        //async Task EnsureInitializedAsync()
        //{
        //    if (publisher is not null) return;

        //    await initLock.WaitAsync();
        //    try
        //    {
        //        if (publisher is not null) return;

        //        environment = AmqpEnvironment.Create(settings);
        //        connection = await environment.CreateConnectionAsync();

        //        IManagement management = connection.Management();

        //        try
        //        {
        //            IQueueSpecification queueSpec = management.Queue(queueName).Type(QueueType.QUORUM);
        //            await queueSpec.DeclareAsync();
        //        }
        //        finally
        //        {
        //            await management.CloseAsync();
        //        }

        //        publisher = await connection.PublisherBuilder().Queue(queueName).BuildAsync();
        //    }
        //    finally
        //    {
        //        initLock.Release();
        //    }
        //}

        //// This was previously set to 'void', which resulted in errors not surfacing & frontend still receiving 200 as if no problem occurred
        //// E.g. rabbitmq not reachable yet, connection timing issues, etc.
        //public async Task SendMessage(T message)
        //{
        //    await EnsureInitializedAsync();

        //    MessageType msg = new MessageType
        //    {
        //        Message = message,
        //        Timestamp = DateTimeOffset.UtcNow
        //    };

        //    var amqpMessage = new AmqpMessage(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg)));
        //    PublishResult pr = await publisher.PublishAsync(amqpMessage);
        //    switch (pr.Outcome.State)
        //    {
        //        case OutcomeState.Accepted:
        //            Console.WriteLine($" [x] Sent {msg.ToString()}");
        //            break;
        //        case OutcomeState.Released:
        //            Console.Error.WriteLine($"Released message: {pr.Message.BodyAsString()}");
        //            break;
        //        case OutcomeState.Rejected:
        //            Console.Error.WriteLine($"[Publisher] Message: {pr.Message.BodyAsString()} rejected with error: {pr.Outcome.Error}");
        //            break;
        //        default:
        //            Console.Error.WriteLine($"Unexpected publisher outcome: {pr.Message.BodyAsString()}");
        //            break;
        //    }
        //}

        //public async ValueTask DisposeAsync()
        //{
        //    if (publisher is not null)
        //    {
        //        await publisher.CloseAsync();
        //        publisher.Dispose();
        //    }

        //    if (connection is not null) await connection.CloseAsync();
        //    if (environment is not null) await environment.CloseAsync();

        //    initLock.Dispose();
        //}
    }
