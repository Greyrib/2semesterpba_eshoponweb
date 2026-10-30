using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RabbitMQ.AMQP.Client;
using RabbitMQ.AMQP.Client.Impl;
using Microsoft.Extensions.Options;

namespace Messaging;

public class RabbitMqConnection : IMessageSender, IAsyncDisposable
{
    private readonly MessagingOptions _opt;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, IPublisher> _publishers = new();
    private IEnvironment? _env;
    private IConnection? _conn;

    public RabbitMqConnection (IOptions<MessagingOptions> opt) => _opt = opt.Value;

    public async Task<IConnection> GetConnectionAsync ()
    {
        if (_conn != null) return _conn;
        await _lock.WaitAsync();
        try
        {
            if (_conn != null) return _conn;
            _env = AmqpEnvironment.Create(ConnectionSettingsBuilder.Create()
                .Host(_opt.Host).Port(_opt.Port)
                .User(_opt.User).Password(_opt.Password)
                .Build());
            _conn = await _env.CreateConnectionAsync();
            return _conn;
        }
        finally { _lock.Release(); }
    }

    public async Task DeclareQueueAsync (string queue)
    {
        var conn = await GetConnectionAsync();
        await conn.Management().Queue(queue).Type(QueueType.QUORUM).DeclareAsync();
    }

    public async Task SendAsync<T> (string queue, T message, CancellationToken ct = default)
    {
        var conn = await GetConnectionAsync();

        await _lock.WaitAsync(ct);
        try
        {
            if (!_publishers.TryGetValue(queue, out var publisher))
            {
                await DeclareQueueAsync(queue);
                publisher = await conn.PublisherBuilder().Queue(queue).BuildAsync();
                _publishers[queue] = publisher;
            }

            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            await publisher.PublishAsync(new AmqpMessage(body), ct);
        }
        finally { _lock.Release(); }
    }

    public async ValueTask DisposeAsync ()
    {
        if (_env != null) await _env.CloseAsync();
    }
}
