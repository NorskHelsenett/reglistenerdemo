using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.ServiceBus.Messaging;

namespace RegListenerDemo.BusUtils
{
    internal class PermanentMessageErrorException : Exception
    {
        public PermanentMessageErrorException(string message) : base(message)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class PropMatchHandleAttribute : Attribute
    {
        private KeyValuePair<string, string> _pv;
        public readonly string PropertyName;
        public readonly string PropertyValue;

        public PropMatchHandleAttribute(string propertyName, string propertyValue)
        {
            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));
            if (propertyValue == null) throw new ArgumentNullException(nameof(propertyValue));
            PropertyName = propertyName.ToLower();
            PropertyValue = propertyValue.ToLower();
        }
    }

    internal struct HandlerInfo
    {
        public HandlerInfo(string propertyName, string propertyValue, Action<BrokeredMessage> handler)
        {
            PropertyName = propertyName;
            PropertyValue = propertyValue;
            Handler = handler;
        }

        public readonly string PropertyName;
        public readonly string PropertyValue;
        public Action<BrokeredMessage> Handler;
    }

    public abstract class GenericQueueListener
    {
        private readonly MessagingFactory _messagingFactory;
        private readonly string _queuePath;
        private bool _listen;
        private Thread _thread;
        private List<HandlerInfo> _handlers;
        private Action<string> _logger;
        private MessageReceiver _queueListener;

        protected GenericQueueListener(MessagingFactory messagingFactory, string queuePath, Action<string> logger)
        {
            _messagingFactory = messagingFactory;
            _queuePath = queuePath;
            _logger = logger;
        }

        /// <summary>
        /// You still need to implement default handler ProcessMessage
        /// </summary>
        protected void ConfigureAttributeBasedHandling()
        {
            _handlers = BuildHandlerList();
        }

        private List<HandlerInfo> BuildHandlerList()
        {
            var ret = new List<HandlerInfo>();

            foreach (var method in GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var ya = method.GetCustomAttributes<PropMatchHandleAttribute>().FirstOrDefault();
                if (ya == null)
                    continue;
                ret.Add(new HandlerInfo(ya.PropertyName, ya.PropertyValue, brokeredMessage => method.Invoke(this, new object[] { brokeredMessage })));
            }

            if (ret.Count == 0)
                throw new InvalidOperationException("No handlers found!");

            ret.Sort((a, b) => String.Compare(a.PropertyName, b.PropertyName, StringComparison.Ordinal));

            return ret;
        }

        public void Start()
        {
            if (_listen || _thread != null)
                throw new InvalidOperationException("Starting already started listener??");
            _listen = true;
            _thread = new Thread(StartInner) //Don't use tasks for long running tasks.. :-)
            {
                Name = "GQL: " + _queuePath.Replace("/", "_")
            };
            _thread.Start();
        }

        private void StartInner()
        {
            _queueListener = _messagingFactory.CreateMessageReceiver(_queuePath, ReceiveMode.PeekLock);
            _logger("Starting listening to queue " + _queuePath);
            while (_listen)
            {
                //Listen for 5 minutes
                BrokeredMessage message;
                try
                {
                    message = _queueListener.Receive(TimeSpan.FromMinutes(5));
                }
                catch (TimeoutException)
                {
                    continue;
                }
                catch (OperationCanceledException) //When Cancel() is called on client or factory
                {
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    _logger("Stop listening for messages on queue ");
                    return;
                }

                if (message == null)
                    continue;
                //Read message
                _logger($"*** Queue Received new message on {_queuePath} ***");

                try
                {
                    if (_handlers != null)
                        HandleByAttributes(message);
                    else
                        ProcessMessage(message);

                    message.Complete();
                }
                catch (PermanentMessageErrorException ex)
                {
                    _logger("Permanent failure on message. Moving to dead letter queue." + ex.Message);
                    message.DeadLetter();
                }
                catch (Exception ex)
                {
                    _logger("Failed to process queue command, leaving message on bus: " + ex);
                    message.Abandon();
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            }

            _queueListener = null;
        }

        private void HandleByAttributes(BrokeredMessage message)
        {
            string lastPropName = null;
            string propValue = null;

            foreach (var handler in _handlers)
            {
                if (lastPropName != handler.PropertyName)
                {
                    lastPropName = handler.PropertyName;
                    propValue = message.Properties[handler.PropertyName] as string;
                    propValue = propValue?.ToLower();
                }

                if (propValue == handler.PropertyValue)
                {
                    handler.Handler(message);
                    return;
                }
            }

            //No matches, use default.
            ProcessMessage(message);
        }

        protected abstract void ProcessMessage(BrokeredMessage message);

        public void Stop(bool waitForThreadCompletion)
        {
            _listen = false;

            if (_queueListener != null)
                _queueListener.Close();
 
            if (waitForThreadCompletion)
                _thread?.Join(5000);
        }
    }
}