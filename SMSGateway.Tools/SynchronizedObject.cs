using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.Tools
{
    public class SynchronizedObject<T>
    {
        // *** Locking ***
        private object lockObject;

        // *** Value buffer ***
        private T valueObject;


        internal T Value
        {
            get
            {
                lock (lockObject)
                {
                    return valueObject;
                }
            }
            set
            {
                lock (lockObject)
                {
                    valueObject = value;
                }
            }
        }

        // *******************
        // *** Constructor ***
        // *******************
        public SynchronizedObject()
        {
            lockObject = new object();
        }

        public SynchronizedObject(T value)
        {
            lockObject = new object();
            Value = value;
        }

        public SynchronizedObject(T value, object Lock)
        {
            lockObject = Lock;
            Value = value;
        }

        // ********************************
        // *** Type casting overloading ***
        // ********************************
        public static implicit operator T(SynchronizedObject<T> value)
        {
            return value.Value;
        }

    }
}
