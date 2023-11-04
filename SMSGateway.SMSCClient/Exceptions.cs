using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMSCClient
{
    public class SmppException : Exception
    {
        public uint ErrorCode { get; }
        public SmppException(uint errorCode)
        {
            this.ErrorCode = errorCode;
        }
    }

    public class TlvException : SmppException
    {
        public TlvException(uint errorCode) : base(errorCode)
        {

        }
    }
}
