using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMSCClient
{
    public class SmppText
    {
        public Guid Id { get; set; }
        public Guid CommandId { get; set; }
        public string SourceAddress { get; set; }
        public string DestAddress { get; set; }
        public byte EsmClass { get; set; }
        public byte PriorityFlag { get; set; }
        public DateTime? ScheduledDeliveryTime { get; set; }
        public DateTime? ValidityPeriod { get; set; }
        public byte DataCoding { get; set; }
        public string ShortMessage { get; set; }
        public byte? MessageIdentification { get; set; }
        public byte? TotalParts { get; set; }
        public byte? PartNumber { get; set; }
        public string PEID { get; set; }
        public string TMID { get; set; }
        public string TemplateId { get; set; }
        public string MessageId { get; set; }
        public Guid? RecordId { get; set; }
        public string Status { get; set; }
        public Guid SessionId { get; set; }
        public DateTime CreatedOn { get; set; }


        public SmppText()
        {
            Id = Guid.NewGuid();
            CreatedOn = DateTime.Now;
        }
    }
}
