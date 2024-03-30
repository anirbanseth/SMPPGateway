using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.Entity
{
    public class SmppUser
    {
        public long Id { get; set; }
        public string SystemId { get; set; }
        public string Password { get; set; }
        public string SystemType { get; set; }
        public int TPS { get; set; }
        public int ConcurrentSessions { get; set; }
        public string Routes { get; set; }
        public string WhitelistedIps { get; set; }
        public long UserId { get; set; }
        public string Status {  get; set; }
        public bool Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
        public decimal SmsCost { get; set; }
        public decimal DltCharge { get; set; }
    }

    public class SmppUserSearch : SmppUser
    {

    }
}
