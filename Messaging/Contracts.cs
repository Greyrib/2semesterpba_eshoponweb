using System;
using System.Collections.Generic;
using System.Text;

namespace Messaging;

public static class Queues
{
    public const string MyQueue = "my-queue";
}

public record MyQueue(int exampleValue, string exampleData, int exampleId);

public interface IMessageHandler<in T>
{
    Task HandleAsync(T message, CancellationToken ct);
}

public interface IMessageSender
{
    Task SendAsync<T>(string queue, T message, CancellationToken ct = default);
}

public class MessagingOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
