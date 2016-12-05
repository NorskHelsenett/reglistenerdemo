using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using RegListenerDemo.BusUtils;
using System.Runtime.Serialization;
using System.IO;
using System.Xml;

namespace RegListenerDemo
{
    class ReshListener : GenericQueueListener
    {
        public ReshListener(MessagingFactory mf, string queuePath) : base(mf, queuePath, Log)
        {
        }

        private static void Log(string input)
        {
            Console.WriteLine(input);
        }

        protected override void ProcessMessage(BrokeredMessage message)
        {
            Console.WriteLine($"Resh Got message: Sent {message.EnqueuedTimeUtc.ToLocalTime()} received {DateTime.Now}");

            foreach (var prop in message.Properties)
            {
                Console.WriteLine($"   {prop.Key}:{prop.Value}");
            }
        }
    }
}
