using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMSCClient
{
    public class OptionalParameter
    {
        public ushort Tag { get; set; }
        public byte[] Value { get; set; }
        public ushort Length
        {
            get
            {
                if (ReferenceEquals(Value, null))
                    return 0;
                else
                    return (ushort)Value.Length;
            }
        }

        public OptionalParameter(
            ushort tag,
            byte[] value = null
        )
        {
            Tag = tag;
            Value = value;
        }
    }
}
