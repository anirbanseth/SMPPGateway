using SMSGateway.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//namespace SMSGateway.SMSCClient
namespace SMSGateway.Entity
{
    public class SmppText
    {
        public Guid Id { get; set; }
        public Guid? CommandId { get; set; }
        public string ServiceType { get; set; }
        public byte SourceAddressTon { get; set; }
        public byte SourceAddressNpi { get; set; }
        public string SourceAddress { get; set; }
        public byte DestAddressTon { get; set; }
        public byte DestAddressNpi { get; set; }
        public string DestAddress { get; set; }
        public byte EsmClass { get; set; }
        public byte PriorityFlag { get; set; }
        public DateTime? ScheduledDeliveryTime { get; set; }
        public DateTime? ValidityPeriod { get; set; }
        public byte RegisteredDelivery { get; set; }
        public byte ReplaceIfPresentFlag { get; set; }
        public byte DataCoding { get; set; }
        public byte SmDefaultMsgId { get; set; }
        public string ShortMessage { get; set; }
        public byte? MessageIdentification { get; set; }
        public byte? TotalParts { get; set; }
        public byte? PartNumber { get; set; }
        public string PEID { get; set; }
        public string TMID { get; set; }
        public string TemplateId { get; set; }
        public string MessageId { get; set; }
        public ulong? SendSmsId { get; set; }
        public MessageState MessageState { get; set; }
        public string Status { get; set; }
        public Guid SessionId { get; set; }
        public long SmppUserId { get;set; }
        public long UserId { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
        public DateTime? MessageStateUpdatedOn { get; set; }
        public decimal SmsCost { get; set; }
        public decimal DltCharge { get; set; }

        public SmppText()
        {
            Id = Guid.NewGuid();
            CreatedOn = DateTime.Now;
            UpdatedOn = DateTime.Now;
        }
    }
}
