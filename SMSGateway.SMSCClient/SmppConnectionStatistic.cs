using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMSCClient
{
    public class SmppConnectionStatistic
    {
        public int Instance { get; set; }
        public string ConnectionType { get; set; }
        public Guid Id { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? BindTime { get; set; }
        public DateTime? UnbindTime { get; set; }

        public int QueuedDeliveries { get; set; }
        public long QueuedDeliveriesMemorySize { get; set; }
        public int PendingDeliveries { get; set; }
        public long PendingDeliveriesMemorySize { get; set; }

        public int PendingMessageParts { get; set; }
        public long PendingMessagePartsMemorySize { get; set; }

        public long TotalMemory { get; set; }
    }
}
