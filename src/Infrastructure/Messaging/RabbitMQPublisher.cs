using System;
using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;

namespace Microsoft.eShopWeb.Infrastructure.Messaging;

public class RabbitMQPublisher : IMessagePublisher
{
    public Task PublishAsync<T>(T message)
    {
        throw new NotImplementedException();
    }
}
