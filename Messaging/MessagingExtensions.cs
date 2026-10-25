using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, Action<MessagingOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IMessageSender>(sp => sp.GetRequiredService<RabbitMqConnection>());
        return services;
    }

    public static IServiceCollection AddQueueConsumer<TMessage, THandler>(
        this IServiceCollection services, string queue) where THandler : class, IMessageHandler<TMessage>
    {
        services.AddScoped<IMessageHandler<TMessage>, THandler>();
        services.AddSingleton<IHostedService>(sp => new QueueConsumer<TMessage>(
            queue,
            sp.GetRequiredService<RabbitMqConnection>(),
            sp.GetRequiredService<IServiceScopeFactory>()));
        return services;
    }
}
