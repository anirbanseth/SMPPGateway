using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//namespace SMSGateway.SMSCClient
namespace SMSGateway.Entity
{
    public class SmppDelivery : SmppText
    {
        //public Guid Id { get; set; }
        //public Guid? CommandId { get; set; }
        //public Guid UserId { get; set; }
        //public string SourceAddress { get; set; }
        //public string DestAddress { get; set; }
        //public string MessageId { get; set; }
        //public string ShortMessage { get; set; }
        //public DateTime? SubmitTime { get; set; }
        //public byte DeliveryStatus { get; set; }
        //public DateTime? DeliveryTime { get; set; }
        //public string ErrorCode { get; set; }
        //public int RetryIndex { get; set; }
        //public DateTime RetryOn { get; set; }
        //public string Status { get; set; }
        //public DateTime? SentOn { get; set; }
        //public DateTime CreatedOn { get; set; }
        //public DateTime UpdatedOn { get; set; }
        public string ErrorCode { get; set; }
        public byte RetryCount { get; set; }
        public DateTime RetryOn { get; set; }

    }
}
