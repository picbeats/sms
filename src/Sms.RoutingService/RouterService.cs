﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sms.Messaging;
using Sms.Routing;
using log4net;
using log4net.Config;

namespace Sms.RoutingService
{
    public class RouterService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(RouterService));
        private Reciever sendQueue, nextMessageQueue;

        public RouterService()
        {
            Config = new Configuration();
            SetUpLogging();
            LoadConfiguration();
        }

        public Configuration Config { get; private set; }

        private void SetUpLogging()
        {
            var configFile = new FileInfo("log4net.config");

            if (!configFile.Exists)
                throw new ApplicationException("Log4net config file doesn't exist!. No you can't run without it!");

            XmlConfigurator.ConfigureAndWatch(configFile);

            log.Info("RouterService logging setup...");
        }

        private void LoadConfiguration()
        {
            var section = (NameValueCollection)ConfigurationManager.GetSection("ServiceConfiguration");

            if (section == null)
            {
                log.Error("Configuration could not be read");
                return;
            }

            var services = new List<ServiceEndpoint>();

            foreach (string key in section.Keys)
            {
                string value = section[key];

                var valueSplit = value.Split(new[] { "://" }, 2, StringSplitOptions.RemoveEmptyEntries);

                string provider = valueSplit[0];
                string queueName = valueSplit[1];

                services.Add(new ServiceEndpoint()
                    {
                        ServiceName = key,
                        ProviderName = provider,
                        QueueIdentifier = queueName
                    });
            }

            Config.Load(services);
        }

        public void Start()
        {
            //Listen on the send Queue and forward messages to the configured service.
            sendQueue = new Reciever(SmsFactory.Receiver(RouterSettings.ProviderName, RouterSettings.SendQueueName));

            Task.Factory.StartNew(() => sendQueue.Subscribe(message =>
                {
                    try
                    {
                        var configInfo = Config.Get(message.ToAddress);

                        using (var sender = SmsFactory.Sender(configInfo.ProviderName, configInfo.QueueIdentifier))
                        {
                            sender.Send(message);
                            message.Success();
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Fatal("Exception", ex);
                        throw;
                    }
                }));


            //Listen for the next message queue, Then setup a receive (if needed) and forward onto the unique queue provided..
            nextMessageQueue = new Reciever(SmsFactory.Receiver(RouterSettings.ProviderName, RouterSettings.NextMessageQueueName));
            Task.Factory.StartNew(() => nextMessageQueue.Subscribe(message =>
            {
                try
                {
                    string queueIdentifier = message.Body;

                    var configInfo = Config.Get(message.ToAddress);

                    if (!receivers.ContainsKey(queueIdentifier))
                    {
                        var messageReciever = SmsFactory.Receiver(configInfo.ProviderName, configInfo.QueueIdentifier);
                        var toQueue = SmsFactory.Sender(RouterSettings.ProviderName, queueIdentifier);

                        receivers[queueIdentifier] = new PipingMessageReciever(messageReciever, toQueue, TimeSpan.FromMilliseconds(10));
                    }

                    receivers[queueIdentifier].IsActive = true;

                    message.Success();
                }
                catch (Exception ex)
                {
                    log.Fatal("Exception", ex);
                    throw;
                }
            }));

            Task.Factory.StartNew(() =>{
                                           try
                                           {
                                               while (!stop)
                                               {
                                                   bool hasWork = false;
                                                   foreach (var item in receivers)
                                                   {
                                                       hasWork = item.Value.CheckOne() || hasWork;
                                                   }

                                                   Thread.Sleep(hasWork ? 10 : 100);
                                               }
                                           }
                                           catch (Exception ex)
                                           {
                                               log.Fatal("Exception", ex);
                                               throw;
                                           }
            });
        }

        private readonly ConcurrentDictionary<string, PipingMessageReciever> receivers = new ConcurrentDictionary<string, PipingMessageReciever>();

        private bool stop;

        public void Stop()
        {
            this.stop = true;

            if (sendQueue != null)
                sendQueue.Dispose();

            if (nextMessageQueue != null)
                nextMessageQueue.Dispose();
        }
    }

    public class PipingMessageReciever : IDisposable
    {
        private readonly TimeSpan timeSpan;
        public IMessageReciever Reciever { get; set; }
        public IMessageSender ToQueue { get; set; }

        public PipingMessageReciever(IMessageReciever reciever, IMessageSender toQueue, TimeSpan timeSpan)
        {
            this.timeSpan = timeSpan;
            Reciever = reciever;
            ToQueue = toQueue;
        }
        public bool IsActive { get; set; }

        public bool CheckOne()
        {
            if (!IsActive) return false;

            var message = Reciever.Receive(timeSpan);

            if (message != null)
            {
                ToQueue.Send(message);
                message.Success();
                IsActive = false;
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            Reciever.Dispose();
            ToQueue.Dispose();
        }
    }
}