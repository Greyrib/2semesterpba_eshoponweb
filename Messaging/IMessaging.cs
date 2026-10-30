using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Messaging;

//public interface IMessaging
//{
//}

public interface IMessageSender
{
    Task SendAsync<T> (string queue, T message, CancellationToken ct = default);
}

public interface IMessageHandler<in T>
{
    Task HandleAsync (T message, CancellationToken ct);
}
