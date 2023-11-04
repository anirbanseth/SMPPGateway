using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMSCClient
{
    public class SmppUser
    {
        public Guid Id { get; set; }
        public string SystemId { get; set; }
        public string Password { get; set; }
        public string SystemType { get; set; }
        public int TPS { get; set; }
        public int ConcurrentSessions { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
