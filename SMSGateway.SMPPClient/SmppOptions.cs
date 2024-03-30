using SMSGateway.SMSCClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMPPClient
{
    public class SmppOptions
    {
        public SMSC[] Providers { get; set; }
        public SmscServerOptions[] Servers { get; set; }

        public DatabaseSettings DatabaseSettings { get; set; }
        public KernelParameterOptions KernelParameters { get; set; }
        public Dictionary<string, Dictionary<string, Dictionary<byte, string[]>>[]>[] DeliveryGenerateParams { get; set; }
    }

    public class SmscServerOptions
    {
        public string? Name { get; set; }
        public int Port { get; set; }
        public bool Secured { get;set; }
        public string? DefaultEncoding { get; set; }
        public int DeliveryRetry { get; set; }
    }

    public class DatabaseSettings
    {
        public string DatabaseType { get; set;}
        public string ConnectionString { get; set;}
    }

    public class KernelParameterOptions
    {
        public int MaxBufferSize{ get; set; }
        public int MaxPduSize{ get; set; }
        public int ReconnectTimeout{ get; set; }
        public int WaitPacketResponse{ get; set; }
        public int CanBeDisconnected{ get; set; }
        public int NationalNumberLength{ get; set; }
        public int MaxUndeliverableMessages{ get; set; }
        public byte AskDeliveryReceipt{ get; set; }
        public bool SplitLongText{ get; set; }
        public bool UseEnquireLink{ get; set; }
        public int EnquireLinkTimeout{ get; set; }
        public uint MaxSequenceNumber{ get; set; }
        public byte MaxIdentificationNumber{ get; set; }
        //public int waitForResponse{ get; set; }
        public int DeliveryLoadTimeout{ get; set; }
        public int DeliverySendTimeout{ get; set; }

        public void Save()
        {
            KernelParameters.MaxBufferSize = this.MaxBufferSize;
            KernelParameters.MaxPduSize = this.MaxPduSize;
            KernelParameters.ReconnectTimeout = this.ReconnectTimeout;
            KernelParameters.WaitPacketResponse = this.WaitPacketResponse;
            KernelParameters.CanBeDisconnected = this.CanBeDisconnected;
            KernelParameters.NationalNumberLength = this.NationalNumberLength;
            KernelParameters.MaxUndeliverableMessages = this.MaxUndeliverableMessages;
            KernelParameters.AskDeliveryReceipt = this.AskDeliveryReceipt;
            KernelParameters.SplitLongText = this.SplitLongText;
            KernelParameters.UseEnquireLink = this.UseEnquireLink;
            KernelParameters.EnquireLinkTimeout = this.EnquireLinkTimeout;
            KernelParameters.MaxSequenceNumber = this.MaxSequenceNumber;
            KernelParameters.MaxIdentificationNumber = this.MaxIdentificationNumber;
            KernelParameters.DeliveryLoadTimeout = this.DeliveryLoadTimeout;
            KernelParameters.DeliverySendTimeout = this.DeliverySendTimeout;

        }
    }
}
