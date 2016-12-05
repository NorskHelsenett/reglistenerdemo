using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using NHN.DtoContracts.ServiceBus.Service;
using NHN.WcfClientFactory;

namespace RegListenerDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var clientFactory = GetWcfClientFactory();

            // In this demo, we'll listen to AR and Resh.
            var serviceBusManager = clientFactory.Get<IServiceBusManager>();

            var arSubscriptionInfo = serviceBusManager.AddOrGetSubscription("addressregister", "RegDemo");
            Console.WriteLine("Sub on AR: " + arSubscriptionInfo.FullPath);
            var reshSubscriptionInfo = serviceBusManager.AddOrGetSubscription("resh", "RegDemo");
            Console.WriteLine("Sub on Resh: " + reshSubscriptionInfo.FullPath);

            var sbusConnString = GetServiceBusConnstring();
            var mf = MessagingFactory.CreateFromConnectionString(sbusConnString);

            var arListener = new ArListener(mf, arSubscriptionInfo.FullPath);
            arListener.Start();
            var reshListener = new ReshListener(mf, reshSubscriptionInfo.FullPath);
            reshListener.Start();

            Console.WriteLine("Listening. Any key quits.");

            Console.ReadKey();

            arListener.Stop(true);
            reshListener.Stop(true);
        }

        public static string GetServiceBusConnstring()
        {
            Func<string, string> gas = key => ConfigurationManager.AppSettings[key];
            return gas("ServiceBusConnectionString").Replace("[Username]", gas("Username")).Replace("[Password]", gas("Password"));
        }

        private static WcfClientFactory GetWcfClientFactory()
        {
            var username = ConfigurationManager.AppSettings["Username"];
            var password = ConfigurationManager.AppSettings["Password"];
            var host = ConfigurationManager.AppSettings["Host"];

            return new WcfClientFactory(host)
            {
                Username = username,
                Password = password,
                FixDnsIdentityProblem = true,
                Transport = Transport.Https
            };
        }
    }
}
