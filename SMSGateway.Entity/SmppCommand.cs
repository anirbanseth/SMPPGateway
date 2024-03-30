using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//namespace SMSGateway.SMSCClient
namespace SMSGateway.Entity
{
    public enum SmppCommandType
    {
        Sent = 'S',
        Recieved = 'R'
    }
    public class SmppCommand
    {
        public Guid Id { get; set; }
        public SmppCommandType CommandType { get; set; }
        public uint CommandLength { get; set; }
        public uint CommandId { get; set; }
        public uint CommandStatus { get; set; }
        public uint Sequence { get; set; }
        public byte[] PDU { get; set; }
        public Guid SessionId { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
