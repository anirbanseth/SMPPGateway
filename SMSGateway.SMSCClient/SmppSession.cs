using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMSCClient
{
    public class SmppSession
    {
        public Guid Id { get; set; }
        public string Address { get; set; }
        public SmppUser User { get; set; }
        public DateTime? LastRecieved { get; set; }
        public DateTime? BindRequest { get; set; }
        public DateTime? UnbindRequest { get; set; }
        public long ReceivedCount { get; set; }
        public long SentCount { get; set; }
        public DateTime ValidForm { get; set; }
        public DateTime ValidTo { get; set; }

        public SmppSession()
        {
            ValidForm = DateTime.Now;
            ValidTo = DateTime.MaxValue;
        }

        //public SmppSession(DateTime validFrom)
        //{
        //    ValidForm = validFrom;
        //    ValidTo = DateTime.MaxValue;
        //}
    }
}
