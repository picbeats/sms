using System;
using System.Messaging;
using System.Threading;
using System.Threading.Tasks;
using Sms.Messaging;

namespace Sms.Msmq
{
    public class MsmqMessageReceiver : IReceiver, IDisposable
    {
        readonly MessageQueue messageQueue = null;
        //private bool stopping;
        private readonly string queueName;

        //public bool Receiving { get; private set; }

        public string ProviderName { get; private set; }

        public MsmqMessageReceiver(string providerName, string queueName)
        {
            ProviderName = providerName;
            QueueName = queueName;
            this.queueName = @".\Private$\" + queueName;

            EnsureQueueExists.OfName(this.queueName);

            
            messageQueue = new MessageQueue(this.queueName);
            messageQueue.Formatter = new XmlMessageFormatter(new Type[1] { typeof(SmsMessageContent) });
        }

        public void Dispose()
        {
            messageQueue.Dispose();
        }

        public string QueueName { get; private set; }

        public Task<MessageResult> TryReceiveAsync()
        {
            MessageQueueTransaction transaction = null;
            try
            {
                try
                {
                    transaction = new MessageQueueTransaction();
                    transaction.Begin();

                    using (var raw = messageQueue.Receive(TimeSpan.Zero, transaction))
                    {
                        var message = ((SmsMessageContent) raw.Body).ToMessage();
                        return Task.FromResult(BuildMessageResult(transaction, message));
                    }
                }
                catch (MessageQueueException ex)
                {
                    TryToAbortTransaction(transaction);

                    if (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                    {
                        return Task.FromResult<MessageResult>(null);
                    }

                    throw;
                }
            }
            catch (Exception ex)
            {
                TryToAbortTransaction(transaction);
                Logger.Warn("Msmq receiver: unknow error: {0}", ex);
                throw;
            }
        }

        public Task<MessageResult> ReceiveAsync(CancellationToken cancelleationToken)
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    cancelleationToken.ThrowIfCancellationRequested();
                    var transaction = new MessageQueueTransaction();
                    try
                    {
                        transaction.Begin();

                        using (var raw = messageQueue.Receive(TimeSpan.FromMilliseconds(500), transaction))
                        {
                            var message = ((SmsMessageContent)raw.Body).ToMessage();
                            return BuildMessageResult(transaction, message);
                        }
                    }
                    catch (MessageQueueException ex)
                    {
                        TryToAbortTransaction(transaction);

                        if (ex.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
                        {
                            continue;
                        }

                        throw;
                    }
                    catch (Exception ex)
                    {
                        TryToAbortTransaction(transaction);
                        Logger.Warn("Msmq receiver: unknow error: {0}", ex);
                        throw;
                    }
                }
            });
        }


        private static MessageResult BuildMessageResult(MessageQueueTransaction transaction, SmsMessage message)
        {
            Func<MessageQueueTransaction, Action<bool>> onReceive = queueTransaction =>
            {
                Action<bool> handler = x =>
                {
                    if (queueTransaction.Status == MessageQueueTransactionStatus.Pending)
                    {
                        if (x)
                            queueTransaction.Commit();
                        else
                            queueTransaction.Abort();
                    }

                    queueTransaction.Dispose();
                };
                return handler;
            };

            return new MessageResult(message, onReceive(transaction));
        }

        private void TryToAbortTransaction(MessageQueueTransaction transaction)
        {
            if (transaction != null)
            {
                try
                {
                    transaction.Abort();
                    transaction.Dispose();
                    transaction = null;
                }
                catch (Exception ex)
                {
                    Logger.Warn("Msmq receiver: error shutting down transaction: {0}", ex);
                }
            }
        }
    }
}