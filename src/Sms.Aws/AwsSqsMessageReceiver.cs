using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Sms.Messaging;

namespace Sms.Aws
{
    public class AwsSqsMessageReceiver : IReceiver, IDisposable
    {
        private AmazonSQSClient client;
        private readonly string queueUrl;

        //public bool Receiving { get; private set; }

        public string QueueName { get; private set; }
        public string ProviderName { get; private set; }

        public AwsSqsMessageReceiver(string provideName, string queueName)
        {
            if (queueName == null) throw new ArgumentNullException("queueName");

            QueueName = queueName;
            ProviderName = provideName;

            client = ClientFactory.Create();
            queueUrl = client.GetQueueUrl(queueName);
        }

        /// <summary>
        /// Returns a single message or null
        /// </summary>
        /// <returns></returns>
        public async Task<MessageResult> TryReceiveAsync()
        {
            try
            {
                var request = new ReceiveMessageRequest()
                {
                    MaxNumberOfMessages = 1,
                    QueueUrl = queueUrl,
                    WaitTimeSeconds = 0,
                    MessageAttributeNames = new List<string> { "All" }
                };

                var raw = await client.ReceiveMessageAsync(request);

                return CreateResult(raw);
            }
            catch (Exception ex)
            {
                Logger.Error("Aws.Sqs receiver: Error:" + ex.ToString(), ex);
                throw;
            }
        }

        public async Task<MessageResult> ReceiveAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                try
                {
                    var request = new ReceiveMessageRequest()
                    {
                        MaxNumberOfMessages = 1,
                        QueueUrl = queueUrl,
                        WaitTimeSeconds = 30,
                        MessageAttributeNames = new List<string> { "All" }
                    };

                    var raw = await client.ReceiveMessageAsync(request, cancellationToken);

                    var result = CreateResult(raw);

                    if (result != null)
                    {
                        return result;
                    }

                }
                catch (Exception ex)
                {
                    Logger.Error("Aws.Sqs receiver: Error:" + ex.ToString(), ex);
                    throw;
                }
            }
        }

        private MessageResult CreateResult(ReceiveMessageResponse raw)
        {
            if (raw.HttpStatusCode != HttpStatusCode.OK)
                throw new WebException("Failed to get a message, status code: " + raw.HttpStatusCode);

            if (raw.Messages.Count == 0) return null;


            var awsMessage = raw.Messages[0];

            if (!awsMessage.MessageAttributes.ContainsKey(Config.ToAttributename))
                throw new InvalidOperationException("The Message is missing the To Attribute");


            var headerKeys = new List<string>();
            var headerValues = new List<string>();

            for (int i = 0; i < 100; i++)
            {
                var key = Config.HeaderKeysAttributename + i;
                var valueKey = Config.HeaderValuesAttributename + i;

                if (!awsMessage.MessageAttributes.ContainsKey(key))
                    break;

                headerKeys.Add(awsMessage.MessageAttributes[key].StringValue);
                headerValues.Add(awsMessage.MessageAttributes[valueKey].StringValue);
            }

            var body = Encoding.UTF8.GetString(Convert.FromBase64String(awsMessage.Body));

            var message = new SmsMessageContent
            {
                To = awsMessage.MessageAttributes[Config.ToAttributename].StringValue,
                Body = body,
                HeaderKeys = headerKeys.ToArray(),
                HeaderValues = headerValues.ToArray()
            }.ToMessage();

            Func<string, Action<bool>> onReceive = receiptHandle =>
            {
                Action<bool> handler = x =>
                {
                    if (x)
                        DeleteMessage(receiptHandle);
                    else
                        SetMessageVisible(receiptHandle);
                };
                return handler;
            };

            return new MessageResult(message, onReceive(awsMessage.ReceiptHandle));
        }

        private void SetMessageVisible(string receiptHandle)
        {
            if (client == null)
                throw new InvalidOperationException("The client has been disposed!");

            client.ChangeMessageVisibility(new ChangeMessageVisibilityRequest()
            {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle,
                VisibilityTimeout = 0
            });
        }

        private void DeleteMessage(string receiptHandle)
        {
            if (client == null)
                throw new InvalidOperationException("The client has been disposed!");

            client.DeleteMessage(new DeleteMessageRequest {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle
            });
        }

        public void Dispose()
        {
            if (client != null)
            {
                client.Dispose();
                client = null;
            }
        }

    }
}