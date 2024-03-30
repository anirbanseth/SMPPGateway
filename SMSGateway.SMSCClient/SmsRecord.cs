using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMSCClient
{
    public class SmppSmsRecord
    {
        public Guid Id { get; set; }
        public string SMSType { get; set; }
        public string Source { get; set; }
        public string Destination { get; set; }
        public string Message { get; set; }
        public byte Priority { get; set; }
        public byte DataCoding { get; set; }
        public string Language { get; set; }
        public string Status { get; set; }
        public decimal? RefID { get; set; }
        public string TemplateId { get; set; }
        public Guid SessionId { get; set; }
        public long UserId { get; set; }
        public DateTime CreatedOn { get; set; }
        public string DeliveryStatus { get; set; }
        public DateTime? DeliveredOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
