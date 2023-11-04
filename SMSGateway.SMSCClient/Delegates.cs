using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SMSGateway.Tools;

namespace SMSGateway.SMSCClient
{
    public delegate void SesionCreatedHandler(SMPPConnection connection, SessionEventArgs e);
    public delegate void SessionTerminatedHandler(SMPPConnection connection, SessionEventArgs e);

    public delegate void PduRecievedHandler(SMPPConnection connection, SmppEventArgs e);
    public delegate void PduSentHandler(SMPPConnection connection, SmppEventArgs e);

    public delegate int BindEventHandler(SMPPConnection connection, BindEventArgs e);
    public delegate uint UnbindEventHandler(SMPPConnection connection);

    public delegate string SubmitSmEventHandler(SMPPConnection connection, SubmitSmEventArgs e);
    public delegate void SubmitSmRespEventHandler(SMPPConnection connection, SubmitSmEventArgs submitSmEventArgs, SubmitSmRespEventArgs submitSmRespEvent);

    public delegate void DeliverSmTimerEventHandler(SMPPConnection connection);
    public delegate void DeliverSmSendEventHandler(SMPPConnection connection, DeliverSmSentEventArgs e);

    public delegate uint DeliverSmEventHandler(SMPPConnection connection, DeliverSmEventArgs e);

    public delegate void LogEventHandler(SMPPConnection connection, LogEventArgs e);

    public delegate void SendSmsEventHandler(SMPPConnection cp, SendSmsEventArgs e);

    /// <summary>
    /// Summary description for LogEventArgs.
    /// </summary>
    public class LogEventArgs : EventArgs
    {
        public LogType LogType { get; internal set; }
        public string Message { get; internal set; }

        public LogEventArgs(LogType logType, string message)
        {
            this.LogType = logType;
            this.Message = message;
        }//LogEventArgs

    }//LogEventArgs

    public class SessionEventArgs : EventArgs
    {
        public Guid Id { get; set; }
        public string Address { get; set; }
        public long ReceivedCount { get; set; }
        public long SentCount { get; set; }
    }

    public class SmppEventArgs : EventArgs, IDisposable
    {
        public Guid Id { get; }
        public uint CommandLength { get; }
        public uint CommandId { get; }
        public uint CommandStatus { get; }
        public uint Sequence { get; }
        public byte[] PDU { get; set; }

        public List<OptionalParameter> OptionalParams { get; set; }

        public SmppEventArgs(
            Guid _id,
            uint length,
            uint id,
            uint status,
            uint sequence,
            byte[] pdu = null,
            List<OptionalParameter> optionalParams = null
        )
        {
            Id = _id;
            CommandLength = length;
            CommandId = id;
            CommandStatus = status;
            Sequence = sequence;
            PDU = pdu;
            OptionalParams = optionalParams;
        }

        public SmppEventArgs(
            uint length,
            uint id,
            uint status,
            uint sequence,
            byte[] pdu = null,
            List<OptionalParameter> optionalParams = null
        ) : this(Guid.NewGuid(), length, id, status, sequence, pdu, optionalParams)
        {
        }

        public SmppEventArgs(
            Guid _id,
            byte[] pdu,
            int length
        )
        {
            Id = _id;

            PDU = new byte[length];
            Array.Copy(pdu, 0, PDU, 0, length);

            CommandLength = (uint)PDU[0] << 24 | (uint)PDU[1] << 16 | (uint)PDU[2] << 8 | (uint)PDU[3];
            CommandId = (uint)PDU[4] << 24 | (uint)PDU[5] << 16 | (uint)PDU[6] << 8 | (uint)PDU[7];
            CommandStatus = (uint)PDU[8] << 24 | (uint)PDU[9] << 16 | (uint)PDU[10] << 8 | (uint)PDU[11];
            Sequence = (uint)PDU[12] << 24 | (uint)PDU[13] << 16 | (uint)PDU[14] << 8 | (uint)PDU[15];


        }

        public SmppEventArgs(
            byte[] pdu,
            int length
        ) : this(Guid.NewGuid(), pdu, length)
        {

        }

        public SmppEventArgs(SmppEventArgs args)
            : this(args.Id, args.CommandLength, args.CommandId, args.CommandStatus, args.Sequence, args.PDU, args.OptionalParams)
        {

        }

        //~SmppEventArgs()
        //{
        //    this.PDU = new byte[0];
        //    this.OptionalParams = null;
        //}

        public void Dispose()
        {
            this.PDU = new byte[0];
            this.OptionalParams = null;
        }
    }

    public class BindEventArgs : SmppEventArgs, IDisposable
    {
        public const int Success = StatusCodes.ESME_ROK;
        public const int Failed = StatusCodes.ESME_RBINDFAIL;
        public const int InvalidPassword = StatusCodes.ESME_RINVPASWD;
        public const int InvalidSystemId = StatusCodes.ESME_RINVSYSID;
        public const int InvalidTon = StatusCodes.ESME_RINVSRCTON;
        public const int InvalidNpi = StatusCodes.ESME_RINVSRCNPI;
        public const int InvalidSystemType = StatusCodes.ESME_RINVSYSTYP;
        public const int UnknownError = StatusCodes.ESME_RUNKNOWNERR;

        public BindEventArgs(
            uint length,
            uint id,
            uint status,
            uint sequence,
            string system_id,
            string password,
            string system_type,
            byte interface_version,
            byte addr_ton,
            byte addr_npi,
            string address_range
        ) : base(length, id, status, sequence)
        {
            this.SystemId = system_id;
            this.Password = password;
            this.SystemType = system_type;
            this.InterfaceVersion = interface_version;
            this.AddrTon = addr_ton;
            this.AddrNpi = addr_npi;
            this.AddressRange = address_range;
        }

        public BindEventArgs(
            SmppEventArgs smppEventArgs,
            string system_id,
            string password,
            string system_type,
            byte interface_version,
            byte addr_ton,
            byte addr_npi,
            string address_range
        ) : base(
                smppEventArgs.Id,
                smppEventArgs.CommandLength,
                smppEventArgs.CommandId,
                smppEventArgs.CommandStatus,
                smppEventArgs.Sequence,
                smppEventArgs.PDU
            )
        {
            this.SystemId = system_id;
            this.Password = password;
            this.SystemType = system_type;
            this.InterfaceVersion = interface_version;
            this.AddrTon = addr_ton;
            this.AddrNpi = addr_npi;
            this.AddressRange = address_range;
        }

        public string SystemId { get; }//SystemId

        public string Password { get; }//Password

        public string SystemType { get; }//SystemType

        public byte InterfaceVersion { get; }//InterfaceVersion

        public byte AddrTon { get; }//AddrTon

        public byte AddrNpi { get; }//AddrNpi

        public string AddressRange { get; }//AddressRange
    }

    /// <summary>
    /// Summary description for SubmitSmEventArgs.
    /// </summary>
    public class SubmitSmEventArgs : SmppEventArgs, IDisposable
    {

        public SubmitSmEventArgs(
            SmppEventArgs smppEventArgs,
            string serviceType,
            byte sourceAddrTon,
            byte sourceAddrNpi,
            string sourceAddress,
            byte destAddrTon,
            byte destAddrNpi,
            string destAddress,
            byte esmClass,
            byte protocolId,
            byte priorityFlag,
            DateTime? scheduledDeliveryTime,
            DateTime? validityPeriod,
            byte registeredDelivery,
            byte replaceIfPresentFlag,
            byte dataCoding,
            byte defaultMsgId,
            byte length,
            byte[] message,
            List<OptionalParameter> optionalParams = null,
            string? refId = null,
            int retryIndex = 0
        )
            : this(
                  smppEventArgs,
                  serviceType,
                  sourceAddrTon, 
                  sourceAddrNpi, 
                  sourceAddress, 
                  destAddrTon, 
                  destAddrNpi, 
                  destAddress,
                  esmClass,
                  protocolId, 
                  priorityFlag, 
                  scheduledDeliveryTime,
                  validityPeriod, 
                  registeredDelivery, 
                  replaceIfPresentFlag, 
                  dataCoding, 
                  defaultMsgId, 
                  length, 
                  message, 
                  optionalParams  
            )
        {
            RefId = refId;
            RetryIndex = retryIndex;
        }

        public SubmitSmEventArgs(
            SmppEventArgs smppEventArgs,
            string serviceType,
            byte sourceAddrTon,
            byte sourceAddrNpi,
            string sourceAddress,
            byte destAddrTon,
            byte destAddrNpi,
            string destAddress,
            byte esmClass,
            byte protocolId,
            byte priorityFlag,
            DateTime? scheduledDeliveryTime,
            DateTime? validityPeriod,
            byte registeredDelivery,
            byte replaceIfPresentFlag,
            byte dataCoding,
            byte defaultMsgId,
            byte length,
            byte[] message,
            List<OptionalParameter> optionalParams = null
        ) : base(
                smppEventArgs.Id,
                smppEventArgs.CommandLength,
                smppEventArgs.CommandId,
                smppEventArgs.CommandStatus,
                smppEventArgs.Sequence,
                smppEventArgs.PDU,
                optionalParams)
        {
            this.ServiceType = serviceType;
            this.SourceAddrTon = sourceAddrTon;
            this.SourceAddrNpi = sourceAddrNpi;
            this.SourceAddress = sourceAddress;
            this.DestAddrTon = destAddrTon;
            this.DestAddrNpi = destAddrNpi;
            this.DestAddress = destAddress;
            this.EsmClass = esmClass;
            this.ProtocolId = protocolId;
            this.PriorityFlag = priorityFlag;
            this.ScheduledDeliveryTime = scheduledDeliveryTime;
            this.ValidityPeriod = validityPeriod;
            this.RegisteredDelivery = registeredDelivery;
            this.ReplaceIfPresentFlag = replaceIfPresentFlag;
            this.DataCoding = dataCoding;
            this.DefaultMsgId = defaultMsgId;
            this.SmLength = length;
            this.Message = message;
        }

        public SubmitSmEventArgs(
            uint commandLength,
            uint commandStatus,
            uint sequence,
            byte[] pdu,
            string serviceType,
            byte sourceAddrTon,
            byte sourceAddrNpi,
            string sourceAddress,
            byte destAddrTon,
            byte destAddrNpi,
            string destAddress,
            byte esmClass,
            byte protocolId,
            byte priorityFlag,
            DateTime? scheduledDeliveryTime,
            DateTime? validityPeriod,
            byte registeredDelivery,
            byte replaceIfPresentFlag,
            byte dataCoding,
            byte defaultMsgId,
            byte length,
            byte[] message,
            List<OptionalParameter> optionalParams = null
        ) : base(commandLength, Command.SUBMIT_SM, commandStatus, sequence, pdu, optionalParams)
        {
            this.ServiceType = serviceType;
            this.SourceAddrTon = sourceAddrTon;
            this.SourceAddrNpi = sourceAddrNpi;
            this.SourceAddress = sourceAddress;
            this.DestAddrTon = destAddrTon;
            this.DestAddrNpi = destAddrNpi;
            this.DestAddress = destAddress;
            this.EsmClass = esmClass;
            this.ProtocolId = protocolId;
            this.PriorityFlag = priorityFlag;
            this.ScheduledDeliveryTime = scheduledDeliveryTime;
            this.ValidityPeriod = validityPeriod;
            this.RegisteredDelivery = registeredDelivery;
            this.ReplaceIfPresentFlag = replaceIfPresentFlag;
            this.DataCoding = dataCoding;
            this.DefaultMsgId = defaultMsgId;
            this.SmLength = length;
        }
        public string ServiceType { get; }
        public byte SourceAddrTon { get; }
        public byte SourceAddrNpi { get; }
        public string SourceAddress { get; private set; }
        public byte DestAddrTon { get; }
        public byte DestAddrNpi { get; }
        public string DestAddress { get; private set; }
        public byte EsmClass { get; }
        public byte ProtocolId { get; }
        public byte PriorityFlag { get; }
        public DateTime? ScheduledDeliveryTime { get; }
        public DateTime? ValidityPeriod { get; }
        public byte RegisteredDelivery { get; }
        public byte ReplaceIfPresentFlag { get; }
        public byte DataCoding { get; }
        public byte DefaultMsgId { get; }
        public byte SmLength { get; }
        public byte[] Message { get; private set; }
        public string? RefId { get; set; }
        public int RetryIndex { get; set; }
        public new void Dispose()
        {
            this.SourceAddress = String.Empty;
            this.DestAddress = String.Empty;
            this.Message = new byte[0];

            base.Dispose();
        }
    }

    /// <summary>
    /// Summary description for SubmitSmRespEventArgs.
    /// </summary>
    public class SubmitSmRespEventArgs : SmppEventArgs, IDisposable
    {
        public SubmitSmRespEventArgs(
            SmppEventArgs smppEventArgs,
            string messageID,
            List<OptionalParameter>  tlvParameters
        ) : base(
                smppEventArgs.Id,
                smppEventArgs.CommandLength,
                smppEventArgs.CommandId,
                smppEventArgs.CommandStatus,
                smppEventArgs.Sequence,
                smppEventArgs.PDU,
                smppEventArgs.OptionalParams
            )
        {
            this.MessageID = messageID;
            this.TLVParameters = tlvParameters;
        }

        //public SubmitSmRespEventArgs(uint sequence, uint status, string messageID)
        //    : base()
        //{
        //    this.Sequence = sequence;
        //    this.Status = status;
        //    this.MessageID = messageID;
        //}//SubmitSmRespEventArgs

        public string MessageID { get; }//MessageID
        public List<OptionalParameter> TLVParameters { get; set; }

    }//SubmitSmRespEventArgs


    /// <summary>
    /// Summary description for DeliverSmEventArgs.
    /// </summary>
    public class DeliverSmEventArgs : EventArgs
    {

        public DeliverSmEventArgs(uint sequence_number, string to, string from, string textString, string hexString, byte dataCoding, byte esmClass, bool isDeliveryReceipt, byte messageState, DateTime? submitDate, DateTime? doneDate, string receiptedMessageID)
        {
            this.SequenceNumber = sequence_number;
            this.To = to;
            this.From = from;
            this.TextString = textString;

            this.HexString = hexString;
            this.DataCoding = dataCoding;
            this.EsmClass = esmClass;

            this.IsDeliveryReceipt = isDeliveryReceipt;
            this.MessageState = messageState;
            this.ReceiptedMessageID = receiptedMessageID;

            this.SubmitDate = submitDate;
            this.DoneDate = doneDate;

        }//DeliverSmEventArgs

        public DeliverSmEventArgs(uint sequence_number, string to, string from, string textString, string hexString, byte dataCoding, byte esmClass, bool isDeliveryReceipt, byte messageState, string receiptedMessageID)
        {
            this.SequenceNumber = sequence_number;
            this.To = to;
            this.From = from;
            this.TextString = textString;

            this.HexString = hexString;
            this.DataCoding = dataCoding;
            this.EsmClass = esmClass;

            this.IsDeliveryReceipt = isDeliveryReceipt;
            this.MessageState = messageState;
            this.ReceiptedMessageID = receiptedMessageID;

        }//DeliverSmEventArgs

        public DeliverSmEventArgs(uint sequence_number, string to, string from, string textString, string hexString, byte dataCoding, byte esmClass)
        {
            this.SequenceNumber = sequence_number;
            this.To = to;
            this.From = from;
            this.TextString = textString;

            this.HexString = hexString;
            this.DataCoding = dataCoding;
            this.EsmClass = esmClass;
        }//DeliverSmEventArgs

        public DeliverSmEventArgs(uint sequence_number, string to, string from, string textString)
        {
            this.SequenceNumber = sequence_number;
            this.To = to;
            this.From = from;
            this.TextString = textString;
        }//DeliverSmEventArgs

        public uint SequenceNumber { get; }//SequenceNumber

        public string To { get; }//To

        public string From { get; }//From

        public string TextString { get; }//TextString

        public string HexString { get; }//HexString
= "";//HexString

        public byte DataCoding { get; }//DataCoding
= 0;//DataCoding

        public byte EsmClass { get; }//EsmClass
= 0;//EsmClass

        public bool IsDeliveryReceipt { get; }//IsDeliveryReceipt
= false;//IsDeliveryReceipt

        public byte MessageState { get; }//MessageState
= 0;//MessageState

        public string ReceiptedMessageID { get; }//ReceiptedMessageID
= "";//ReceiptedMessageID

        public DateTime? DoneDate { get; set; }
        public DateTime? SubmitDate { get; set; }

    }//DeliverSmEventArgs


    public class DeliverSmSentEventArgs : SmppEventArgs, IDisposable
    {
        //public Guid Id { get; set; }
        //public Guid? CommandId { get; set; }
        //public Guid UserId { get; set; }
        //public string SourceAddress { get; set; }
        //public string DestAddress { get; set; }
        //public string MessageId { get; set; }
        //public string ShortMessage { get; set; }
        //public byte DeliveryStatus { get; set; }
        //public DateTime? DeliveryTime { get; set; }
        //public string ErrorCode { get; set; }
        //public string Status { get; set; }
        //public DateTime CreatedOn { get; set; }
        //public DateTime UpdatedOn { get; set; }

        //public DeliverSmSentEventArgs()

        //{

        //}

        public DeliverSmSentEventArgs(SmppEventArgs args)
            : base(args)
        {

        }


        public DeliverSmSentEventArgs(
            uint length,
            uint id,
            uint status,
            uint sequence,
            byte[] pdu = null,
            List<OptionalParameter> optionalParams = null
        ) : base(Guid.NewGuid(), length, id, status, sequence, pdu, optionalParams)
        {
        }


        public SmppDelivery Data { get; set; }
        public int SentStatus { get; set; }

        public new void Dispose()
        {
            this.Data = null;
            base.Dispose();
        }
    }


    public class SendSmsEventArgs : IDisposable
    {
        //public SendSmsEventArgs(SmppEventArgs args)
        //    : base(args)
        //{

        //}

        public SendSmsEventArgs()
        {

        }

        public string SourceAddress { get;set; }
        public string DestinationAddress { get;set; }
        public string MessageContent { get; set; }
        public int MessageCount { get; set; }
        public MessageEncoding DataCoding { get; set; }
        public string PEID { get; set; }
        public string TMID { get; set; }
        public string TemplateID { get; set; }
        public string RefId { get; set; }
        public int RetryIndex { get; set; }
        public DateTime SentOn { get; set; }
        public void Dispose()
        {
            //throw new NotImplementedException();
        }
    }
}