using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using RegListenerDemo.BusUtils;
using RegListenerDemo.AR;
using System.Runtime.Serialization;
using System.IO;
using System.Xml;

namespace RegListenerDemo
{
    class ArListener : GenericQueueListener
    {
        public ArListener(MessagingFactory mf, string queuePath) : base(mf, queuePath, Log)
        {
        }

        private static void Log(string input)
        {
            Console.WriteLine(input);
        }

        protected override void ProcessMessage(BrokeredMessage message)
        {
            Console.WriteLine($"AR Got message: Sent {message.EnqueuedTimeUtc.ToLocalTime()} received {DateTime.Now}");
            var bodyComPart = TryGetBodyComPart(message);
            if (bodyComPart != null)
            {
                Console.WriteLine($"   HerId:{bodyComPart.HerId} Name: {bodyComPart.DisplayName ?? bodyComPart.Name}");
            }

            foreach (var prop in message.Properties)
            {
                Console.WriteLine($"   {prop.Key}:{prop.Value}");
            }
        }

        private static DataContractSerializer Serializer = new DataContractSerializer(typeof(CommunicationParty));
        private CommunicationParty TryGetBodyComPart(BrokeredMessage message)
        {
            try
            {
                using (var stream = message.GetBody<Stream>())
                {
                    return (CommunicationParty)Serializer.ReadObject(stream);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("   message did not contain a commparty. Ex: " + ex.Message);
                return null;
            }
        }
    }
}
