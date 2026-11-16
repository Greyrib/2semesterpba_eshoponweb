using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging (this IServiceCollection services, Action<MessagingOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IMessageSender>(sp => sp.GetRequiredService<RabbitMqConnection>());
        return services;
    }

    public static IServiceCollection AddQueueConsumer<TMessage, THandler> (this IServiceCollection services, string queue)
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddScoped<IMessageHandler<TMessage>, THandler>();
        services.AddSingleton<IHostedService>(sp => new QueueConsumer<TMessage>(
            queue,
            sp.GetRequiredService<RabbitMqConnection>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<QueueConsumer<TMessage>>>()));
        return services;
    }

    // Lets callers do SendAsync-then-await-reply instead of pure fire-and-forget.
    // replyQueue is where this process listens for replies correlated back to its own requests.
    public static IServiceCollection AddRequestResponse (this IServiceCollection services, string replyQueue)
    {
        services.AddSingleton(sp => new MessageRequester(
            sp.GetRequiredService<IMessageSender>(),
            sp.GetRequiredService<ILogger<MessageRequester>>(),
            replyQueue));
        services.AddSingleton<IMessageRequester>(sp => sp.GetRequiredService<MessageRequester>());
        services.AddSingleton<IMessageHandler<ReplyEnvelope>>(sp => sp.GetRequiredService<MessageRequester>());
        services.AddSingleton<IHostedService>(sp => new QueueConsumer<ReplyEnvelope>(
            replyQueue,
            sp.GetRequiredService<RabbitMqConnection>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<QueueConsumer<ReplyEnvelope>>>()));
        return services;
    }

    // Registered on the side that answers requests (e.g. CatalogMicroservice): listens on
    // requestQueue, runs THandler, and publishes the result to replyQueue itself.
    public static IServiceCollection AddRequestHandler<TRequest, TResponse, THandler> (this IServiceCollection services, string requestQueue)
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        services.AddScoped<IRequestHandler<TRequest, TResponse>, THandler>();
        services.AddScoped<IMessageHandler<RequestEnvelope<TRequest>>>(sp =>
            new ReplyingMessageHandler<TRequest, TResponse>(
                sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>(),
                sp.GetRequiredService<IMessageSender>()));

        services.AddSingleton<IHostedService>(sp => new QueueConsumer<RequestEnvelope<TRequest>>(
            requestQueue,
            sp.GetRequiredService<RabbitMqConnection>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<QueueConsumer<RequestEnvelope<TRequest>>>>()));
        return services;
    }
}
