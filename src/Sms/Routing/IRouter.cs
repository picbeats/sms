﻿using System;
using System.Collections.Generic;
using Sms.Messaging;

namespace Sms.Routing
{
    public interface IRouter : IDisposable
    {
        void Send(string serviceName, string message, IDictionary<string,string> headers =  null);
        IReceiver<SmsMessage> Receiver(string serviceName);
        
    }
}