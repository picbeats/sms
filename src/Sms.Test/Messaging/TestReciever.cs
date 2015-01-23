using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Messaging;

namespace Sms.Test
{
    public class TestReceiver : IReceiver
    {
        public void Subscribe(Func<SmsMessage, bool> action)
        {
        }

        public void Dispose()
        {
        }

        public string ProviderName { get; private set; }
        public string QueueName { get { return "TestReceiver"; } }

        public Task<MessageResult> TryReceiveAsync()
        {
            return Task.FromResult<MessageResult>(null);
        }

        public Task<MessageResult> ReceiveAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<MessageResult>(null);
        }       
    }
}
