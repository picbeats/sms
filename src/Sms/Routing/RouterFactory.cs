﻿using System;
using Sms.Messaging;

namespace Sms.Routing
{
    public static class RouterFactory
    {
        public static Router Build()
        {
            var sender = SmsFactory.Sender(RouterSettings.ProviderName, RouterSettings.SendQueueName);
            var nextMessage = SmsFactory.Sender(RouterSettings.ProviderName, RouterSettings.NextMessageQueueName);

            Func<string, IReceiver<SmsMessage>> receiverFactory = s => SmsFactory.Receiver(RouterSettings.ProviderName, RouterSettings.ReceiveMessageQueueNamePrefix + s);

            return new Router(sender,nextMessage, receiverFactory);
        }
    }
}