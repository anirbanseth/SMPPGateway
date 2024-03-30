using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMSCClient
//namespace SMSGateway.Entity
{
    public class SmppDeliveryData
    {
        public string ServiceType { get; set; }
        public byte SourceAddressTon { get; set; }
        public byte SourceAddressNpi { get; set; }
        public string? SourceAddress { get; set; }
        public byte DestinationAddressTon { get; set; }
        public byte DestinationAddressNpi { get; set; }
        public string? DestAddress { get; set; }

        public byte EsmClass { get; set; }
        public byte ProtocolId { get; set; }
        public byte PriorityFlag {  get; set; }

        public string? ScheduledDeliveryTime { get; set; }
        public string? ValidityPeriod { get; set; }
        public byte RegisteredDelivery { get; set; }
        //public byte ReplaceIfFlagPresent { get; set; }
        public byte DataCoding { get; set; }

        //public byte DefaultMessageId { get; set; }
        public string? ShortMessage { get; set; }

        public List<KeyValuePair<int, string>> TlvParameters { get; set; }
        public Dictionary<string, object > AdditionalParameters { get; set; }

        public SmppDeliveryData()
        {
            TlvParameters = new List<KeyValuePair<int, string>>();
            AdditionalParameters = new Dictionary<string, object>();
        }
    }
}
