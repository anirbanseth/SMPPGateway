using SMSGateway.Entity;
using SMSGateway.SMSCClient;
using SMSGateway.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMPPClient
{
    public delegate void SmsMessageHandler(SmsMessage e);

    public class Messages
    {
        //private static ConcurrentDictionary<string, SortedList<int, object>> messageDictionary { get; set; }
        private static ConcurrentDictionary<string, ConcurrentQueue<SmsMessage>> messageDictionary { get; set; }

        //private static ConcurrentBag<SmsMessage> _messages { get; set; }
        //private static object _lock;

        public static event SmsMessageHandler OnMessageRemoved;

        static Messages()
        {
            //_lock = new object();
            messageDictionary = new ConcurrentDictionary<string, ConcurrentQueue<SmsMessage>>();
        }

        public static ICollection<string> Keys
        {
            get {
                if (!ReferenceEquals(messageDictionary, null))
                {
                    return messageDictionary.Keys;
                }
                return new Collection<string>();
            }
        }

        public static bool TryAddOperator(string key)
        {
            try
            {
                if (messageDictionary.ContainsKey(key))
                {
                    return true;
                }
                else
                {
                    messageDictionary.TryAdd(key, new ConcurrentQueue<SmsMessage>());
                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
            
        }

        public static bool TryRemoveOperator(string key)
        {
            try
            {
                if (messageDictionary.ContainsKey(key))
                {
                    foreach (var item in messageDictionary[key])
                    {
                        Task.Run(() => OnMessageRemoved?.Invoke(item)).Wait();
                    }
                    return true;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
}

        public static void Append(string key, SmsMessage message)
        {
            if (TryAddOperator(key))
            {
                messageDictionary[key].Append(message);
            }
        }

        public static void Enqueue(string key, SmsMessage message)
        {
            if (TryAddOperator(key))
            {
                messageDictionary[key].Enqueue(message);
            }
        }

        public static void TryPeek(string key, out SmsMessage message)
        {
            SmsMessage m = null;
            if (TryAddOperator(key))
            {
                messageDictionary[key].TryPeek(out m);
            }
            message = m;
        }

        public static void TryDequeue(string key, out SmsMessage message)
        {
            SmsMessage m = null;
            if (TryAddOperator(key))
            {
                messageDictionary[key].TryDequeue(out m);
            }
            message = m;
        }

        public static IEnumerable<SmsMessage> Take(string key, int count)
        {
            if (TryAddOperator(key))
            {
                return messageDictionary[key].Take(count);
            }
            return null;
        }


        public static IEnumerable<SmsMessage> TryDequeueN(string key, int count)
        {
            List<SmsMessage> list = new List<SmsMessage>();
            if (TryAddOperator(key))
            {
                while (messageDictionary[key].Count > 0 && list.Count < count)
                {
                    SmsMessage sm;
                    if (messageDictionary[key].TryDequeue(out sm))
                        list.Add(sm);
                }
            }
            return list;
        }

        public static int Count(string key)
        {
            if (TryAddOperator(key))
            {
                return messageDictionary[key].Count;
            }
            else 
            { 
                return 0; 
            }
        }

        public static void Clear(string key)
        {
            TryRemoveOperator(key);
        }
    }
}
