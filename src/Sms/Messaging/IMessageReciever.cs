using System;
using System.Threading.Tasks;
using System.Threading;

namespace Sms.Messaging
{
    public interface IReceiver : IDisposable
    {
        string ProviderName { get;  }

        string QueueName { get; }

        /// <summary>
        /// Returns a single message or null
        /// </summary>
        /// <returns></returns>
        Task<MessageResult> TryReceiveAsync();

        /// <summary>
        ///     Blocks until a single message is returned
        /// </summary>
        /// <returns></returns>
        Task<MessageResult> ReceiveAsync(CancellationToken cancellationToken);
    }
}