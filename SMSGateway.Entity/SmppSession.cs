using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//namespace SMSGateway.SMSCClient
namespace SMSGateway.Entity
{
    public class SmppSession
    {
        public Guid Id { get; set; }
        public string Address { get; set; }
        public long SmppUserId { get; set; }
        public long UserId { get; set; }
        public DateTime? LastRecieved { get; set; }
        public DateTime? BindRequest { get; set; }
        public DateTime? UnbindRequest { get; set; }
        public long ReceivedCount { get; set; }
        public long SentCount { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }

        public SmppSession()
        {
            ValidFrom = DateTime.Now;
            ValidTo = DateTime.MaxValue;
        }

        //public SmppSession(DateTime validFrom)
        //{
        //    ValidForm = validFrom;
        //    ValidTo = DateTime.MaxValue;
        //}
    }
}
