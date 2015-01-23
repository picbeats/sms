using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Messaging
{
    public class ReceiveTask : IDisposable
    {
        private Action<MessageResult> Action { get; set; }
        private readonly IReceiver receiver;
        private bool stopping;
        private Task task;
        private CancellationTokenSource cancellationTokenSource; 
        private readonly TimeSpan? receiveTimeOut;

        public ReceiveTask(IReceiver receiver, Action<MessageResult> action)
        {
            Action = action;
            this.receiver = receiver;
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        private bool Receiving { get; set; }

        public TaskStatus Status    
        {
            get { return task.Status; }
        }

        public void Dispose()
        {
            this.Stop();
            receiver.Dispose();
        }

        public Task Start()
        {
            if (task != null)
                return task;
            
            Receiving = true;
            stopping = false;

            task = Run();
            return task;
        }

        private async Task Run()
        {
            try
            {
                int noMessageCount = 0;
                while (!stopping)
                {
                    var message = await receiver.ReceiveAsync(cancellationTokenSource.Token);

                    if (stopping)
                        break;

                    if (message != null)
                    {
                        noMessageCount = 0;
                        try
                        {
                            Action(message);
                        }
                        catch
                        {
                            message.Failed();
                            throw;
                        }
                    }
                    else
                    {
                        noMessageCount++;
                        Thread.Sleep(Math.Min(100 * noMessageCount, 1000));
                    }
                }
            }
            finally
            {
                Receiving = false;
            }
        }

        public Exception Stop()
        {
            stopping = true;
            try
            {
                if(task != null)
                    Task.WaitAll(task);

                return null;
            }
            catch (AggregateException ae)
            {
                ae.Flatten();
                return ae.InnerExceptions.First();
            }
        }
    }
}
