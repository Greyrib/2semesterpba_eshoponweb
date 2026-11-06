using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Messaging;

public interface IMessageRequester
{
    Task<TResponse?> RequestAsync<TRequest, TResponse>(string requestQueue, TRequest request, TimeSpan timeout, CancellationToken ct = default);
}

public class RequestEnvelope<T>
{
    public Guid CorrelationId { get; set; }
    public string ReplyQueue { get; set; } = string.Empty;
    public T Payload { get; set; } = default!;
}

public class ReplyEnvelope
{
    public Guid CorrelationId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
}

// Sends a request and waits for its matching reply, correlated by CorrelationId.
// Reusable for any request/response pair; not specific to catalog.
public class MessageRequester : IMessageRequester, IMessageHandler<ReplyEnvelope>
{
    private readonly IMessageSender _sender;
    private readonly ILogger<MessageRequester> _logger;
    private readonly string _replyQueue;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<string>> _pending = new();

    public MessageRequester(IMessageSender sender, ILogger<MessageRequester> logger, string replyQueue)
    {
        _sender = sender;
        _logger = logger;
        _replyQueue = replyQueue;
    }

    public async Task<TResponse?> RequestAsync<TRequest, TResponse>(string requestQueue, TRequest request, TimeSpan timeout, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = tcs;

        try
        {
            await _sender.SendAsync(requestQueue, new RequestEnvelope<TRequest> { CorrelationId = correlationId, ReplyQueue = _replyQueue, Payload = request }, ct);

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var registration = timeoutCts.Token.Register(() => tcs.TrySetCanceled());

            var payloadJson = await tcs.Task;
            return JsonSerializer.Deserialize<TResponse>(payloadJson);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Timed out waiting for reply to request on {Queue} (correlation {CorrelationId})", requestQueue, correlationId);
            return default;
        }
        finally
        {
            _pending.TryRemove(correlationId, out _);
        }
    }

    public Task HandleAsync(ReplyEnvelope message, CancellationToken ct)
    {
        if (_pending.TryGetValue(message.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(message.PayloadJson);
        }

        return Task.CompletedTask;
    }
}

// Implemented by the side that answers requests (e.g. CatalogMicroservice).
public interface IRequestHandler<TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}

// Adapts an IRequestHandler into a plain IMessageHandler, and sends the reply itself once done.
public class ReplyingMessageHandler<TRequest, TResponse> : IMessageHandler<RequestEnvelope<TRequest>>
{
    private readonly IRequestHandler<TRequest, TResponse> _handler;
    private readonly IMessageSender _sender;

    public ReplyingMessageHandler(IRequestHandler<TRequest, TResponse> handler, IMessageSender sender)
    {
        _handler = handler;
        _sender = sender;
    }

    public async Task HandleAsync(RequestEnvelope<TRequest> message, CancellationToken ct)
    {
        var response = await _handler.HandleAsync(message.Payload, ct);
        var reply = new ReplyEnvelope
        {
            CorrelationId = message.CorrelationId,
            PayloadJson = JsonSerializer.Serialize(response)
        };

        await _sender.SendAsync(message.ReplyQueue, reply, ct);
    }
}
