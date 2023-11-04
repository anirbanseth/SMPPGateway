using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.Tools
{
    public class LogLevels
    {
        public const int LogPdu = 1;
        public const int LogSteps = 2;
        public const int LogWarnings = 4;
        public const int LogErrors = 8;
        public const int LogExceptions = 16;
        public const int LogInfo = 32;
    }//LogLevels

    public class ConnectionStates
    {
        public const int SMPP_SOCKET_CONNECT_SENT = 1;
        public const int SMPP_SOCKET_CONNECTED = 2;
        public const int SMPP_BIND_SENT = 3;
        public const int SMPP_BINDED = 4;
        public const int SMPP_UNBIND_PENDING = 5;
        public const int SMPP_UNBIND_SENT = 6;
        public const int SMPP_UNBINDED = 7;
        public const int SMPP_SOCKET_DISCONNECTED = 8;
    }//ConnectionStates

    public enum ConnectionType
    {
        Transceiver,
        Receiver,
        Transmitter,
    }

    public enum SmppType
    {
        Server,
        Client
    }

    public class Command
    {
        /// <summary>
        /// No Error
        /// </summary>
        public const uint BIND_RECIEVER = 0x00000001; // bind_reciever

        /// <summary>
        /// Message Length is invalid
        /// </summary>
        public const uint BIND_RECIEVER_RESP = 0x80000001; // bind_reciever_resp

        /// <summary>
        /// Command Length is invalid
        /// </summary>
        public const uint BIND_TRANSMITTER = 0x00000002; // bind_transmitter

        /// <summary>
        /// Invalid Command ID
        /// </summary>
        public const uint BIND_TRANSMITTER_RESP = 0x80000002; // bind_transmitter_resp

        /// <summary>
        /// Incorrect BIND Status for given command
        /// </summary>
        public const uint QUERY_SM = 0x00000003; // query_sm

        /// <summary>
        /// ESME Already in Bound State
        /// </summary>
        public const uint QUERY_SM_RESP = 0x80000003; // query_sm_resp

        /// <summary>
        /// Invalid Priority Flag
        /// </summary>
        public const uint SUBMIT_SM = 0x00000004; // submit_sm

        /// <summary>
        /// Invalid Registered Delivery Flag
        /// </summary>
        public const uint SUBMIT_SM_RESP = 0x80000004; // submit_sm_resp

        /// <summary>
        /// System Error
        /// </summary>
        public const uint DELIVER_SM = 0x00000005; // deliver_sm

        /// <summary>
        /// Invalid Source Address
        /// </summary>
        public const uint DELIVER_SM_RESP = 0x80000005; // deliver_sm_resp

        /// <summary>
        /// Invalid Dest Addr
        /// </summary>
        public const uint UNBIND = 0x00000006; // unbind

        /// <summary>
        /// Message ID is invalid
        /// </summary>
        public const uint UNBIND_RESP = 0x80000006; // unbind_resp

        /// <summary>
        /// bind Failed
        /// </summary>
        public const uint REPLACE_SM = 0x00000007; // replace_sm

        /// <summary>
        /// Invalid Password
        /// </summary>
        public const uint REPLACE_SM_RESP = 0x80000007; // replace_sm_resp

        /// <summary>
        /// Invalid System ID
        /// </summary>
        public const uint CANCEL_SM = 0x00000008; // cancel_sm

        /// <summary>
        /// Cancel SM Failed
        /// </summary>
        public const uint CANCEL_SM_RESP = 0x80000008; // cancel_sm_resp

        /// <summary>
        /// Replace SM Failed
        /// </summary>
        public const uint BIND_TRANSCEIVER = 0x00000009; // bind_transceiver

        /// <summary>
        /// Message Queue Full
        /// </summary>
        public const uint BIND_TRANSCEIVER_RESP = 0x80000009; // bind_transceiver_resp

        /// <summary>
        /// Invalid Service Type
        /// </summary>
        public const uint OUTBIND = 0x0000000B; // outbind

        /// <summary>
        /// Invalid number of destinations
        /// </summary>
        public const uint ENQUIRE_LINK = 0x00000015; // enquire_link

        /// <summary>
        /// Invalid Distribution List name
        /// </summary>
        public const uint ENQUIRE_LINK_RESP = 0x80000015; // enquire_link_resp

        /// <summary>
        /// Destination flag is invalid(submit multi)
        /// </summary>
        public const uint SUBMIT_MULTI = 0x00000021; // submit_multi

        /// <summary>
        /// Invalid `submit with replace? request(i.e. submit_sm with replace_if_present_flag set)
        /// </summary>
        public const uint SUBMIT_MULTI_RESP = 0x80000021; // submit_multi_resp

        /// <summary>
        /// Invalid esm_class field data
        /// </summary>
        public const uint ALERT_NOTIFICATION = 0x00000102; // alert_notification

        /// <summary>
        /// Cannot Submit to Distribution List
        /// </summary>
        public const uint DATA_SM = 0x00000103; // data_sm

        /// <summary>
        /// submit_sm or submit_multi failed
        /// </summary>
        public const uint DATA_SM_RESP = 0x80000103; // data_sm_resp

        /// <summary>
        /// Invalid Source address TON
        /// </summary>
        public const uint GENERIC_NACK = 0x80000000; // generic_nack

        /// <summary>
        /// Invalid Source address NPI
        /// </summary>
        public const uint BROADCAST_SM = 0x00000111; // broadcast_sm

        /// <summary>
        /// Invalid Destination address TON
        /// </summary>
        public const uint BROADCAST_SM_RESP = 0x80000111; // broadcast_sm_resp

        /// <summary>
        /// Invalid Destination address NPI
        /// </summary>
        public const uint QUERY_BROADCAST_SM = 0x00000112; // query_broadcast_sm

        /// <summary>
        /// Invalid system_type field
        /// </summary>
        public const uint QUERY_BROADCAST_SM_RESP = 0x80000112; // query_broadcast_sm_resp

        /// <summary>
        /// Invalid replace_if_present flag
        /// </summary>
        public const uint CANCEL_BROADCAST_SM = 0x00000113; // cancel_broadcast_sm

        /// <summary>
        /// Invalid number of messages
        /// </summary>
        public const uint CANCEL_BROADCAST_SM_RESP = 0x80000113; // cancel_broadcast_sm_resp
    }

    public class StatusCodes
    {
        /// <summary>
        /// No Error
        /// </summary>
        public const int ESME_ROK = 0x00000000; // No Error

        /// <summary>
        /// Message Length is invalid
        /// </summary>
        public const int ESME_RINVMSGLEN = 0x00000001; // Message Length is invalid

        /// <summary>
        /// Command Length is invalid
        /// </summary>
        public const int ESME_RINVCMDLEN = 0x00000002; // Command Length is invalid

        /// <summary>
        /// Invalid Command ID
        /// </summary>
        public const int ESME_RINVCMDID = 0x00000003; // Invalid Command ID

        /// <summary>
        /// Incorrect BIND Status for given command
        /// </summary>
        public const int ESME_RINVBNDSTS = 0x00000004; // Incorrect BIND Status for given command

        /// <summary>
        /// ESME Already in Bound State
        /// </summary>
        public const int ESME_RALYBND = 0x00000005; // ESME Already in Bound State

        /// <summary>
        /// Invalid Priority Flag
        /// </summary>
        public const int ESME_RINVPRTFLG = 0x00000006; // Invalid Priority Flag

        /// <summary>
        /// Invalid Registered Delivery Flag
        /// </summary>
        public const int ESME_RINVREGDLVFLG = 0x00000007; // Invalid Registered Delivery Flag

        /// <summary>
        /// System Error
        /// </summary>
        public const int ESME_RSYSERR = 0x00000008; // System Error

        /// <summary>
        /// Invalid Source Address
        /// </summary>
        public const int ESME_RINVSRCADR = 0x0000000A; // Invalid Source Address

        /// <summary>
        /// Invalid Dest Addr
        /// </summary>
        public const int ESME_RINVDSTADR = 0x0000000B; // Invalid Dest Addr

        /// <summary>
        /// Message ID is invalid
        /// </summary>
        public const int ESME_RINVMSGID = 0x0000000C; // Message ID is invalid

        /// <summary>
        /// bind Failed
        /// </summary>
        public const int ESME_RBINDFAIL = 0x0000000D; // bind Failed

        /// <summary>
        /// Invalid Password
        /// </summary>
        public const int ESME_RINVPASWD = 0x0000000E; // Invalid Password

        /// <summary>
        /// Invalid System ID
        /// </summary>
        public const int ESME_RINVSYSID = 0x0000000F; // Invalid System ID

        /// <summary>
        /// Cancel SM Failed
        /// </summary>
        public const int ESME_RCANCELFAIL = 0x00000011; // Cancel SM Failed

        /// <summary>
        /// Replace SM Failed
        /// </summary>
        public const int ESME_RREPLACEFAIL = 0x00000013; // Replace SM Failed

        /// <summary>
        /// Message Queue Full
        /// </summary>
        public const int ESME_RMSGQFUL = 0x00000014; // Message Queue Full

        /// <summary>
        /// Invalid Service Type
        /// </summary>
        public const int ESME_RINVSERTYP = 0x00000015; // Invalid Service Type

        /// <summary>
        /// Invalid number of destinations
        /// </summary>
        public const int ESME_RINVNUMDESTS = 0x00000033; // Invalid number of destinations

        /// <summary>
        /// Invalid Distribution List name
        /// </summary>
        public const int ESME_RINVDLNAME = 0x00000034; // Invalid Distribution List name

        /// <summary>
        /// Destination flag is invalid(submit multi)
        /// </summary>
        public const int ESME_RINVDESTFLAG = 0x00000040; // Destination flag is invalid(submit_multi)

        /// <summary>
        /// Invalid `submit with replace? request(i.e. submit_sm with replace_if_present_flag set)
        /// </summary>
        public const int ESME_RINVSUBREP = 0x00000042; // Invalid `submit with replace? request(i.e. submit_sm with replace_if_present_flag set)

        /// <summary>
        /// Invalid esm_class field data
        /// </summary>
        public const int ESME_RINVESMCLASS = 0x00000043; // Invalid esm_class field data

        /// <summary>
        /// Cannot Submit to Distribution List
        /// </summary>
        public const int ESME_RCNTSUBDL = 0x00000044; // Cannot Submit to Distribution List

        /// <summary>
        /// submit_sm or submit_multi failed
        /// </summary>
        public const int ESME_RSUBMITFAIL = 0x00000045; // submit_sm or submit_multi failed

        /// <summary>
        /// Invalid Source address TON
        /// </summary>
        public const int ESME_RINVSRCTON = 0x00000048; // Invalid Source address TON

        /// <summary>
        /// Invalid Source address NPI
        /// </summary>
        public const int ESME_RINVSRCNPI = 0x00000049; // Invalid Source address NPI

        /// <summary>
        /// Invalid Destination address TON
        /// </summary>
        public const int ESME_RINVDSTTON = 0x00000050; // Invalid Destination address TON

        /// <summary>
        /// Invalid Destination address NPI
        /// </summary>
        public const int ESME_RINVDSTNPI = 0x00000051; // Invalid Destination address NPI

        /// <summary>
        /// Invalid system_type field
        /// </summary>
        public const int ESME_RINVSYSTYP = 0x00000053; // Invalid system_type field

        /// <summary>
        /// Invalid replace_if_present flag
        /// </summary>
        public const int ESME_RINVREPFLAG = 0x00000054; // Invalid replace_if_present flag

        /// <summary>
        /// Invalid number of messages
        /// </summary>
        public const int ESME_RINVNUMMSGS = 0x00000055; // Invalid number of messages

        /// <summary>
        /// Throttling error (ESME has exceeded allowed message limits)
        /// </summary>
        public const int ESME_RTHROTTLED = 0x00000058; // Throttling error (ESME has exceeded allowed message limits)

        /// <summary>
        /// Invalid Scheduled Delivery Time
        /// </summary>
        public const int ESME_RINVSCHED = 0x00000061; // Invalid Scheduled Delivery Time

        /// <summary>
        /// Invalid message validity period (Expiry time)
        /// </summary>
        public const int ESME_RINVEXPIRY = 0x00000062; // Invalid message validity period (Expiry time)

        /// <summary>
        /// Predefined Message Invalid or Not Found
        /// </summary>
        public const int ESME_RINVDFTMSGID = 0x00000063; // Predefined Message Invalid or Not Found

        /// <summary>
        /// ESME Receiver Temporary App Error Code
        /// </summary>
        public const int ESME_RX_T_APPN = 0x00000064; // ESME Receiver Temporary App Error Code

        /// <summary>
        /// ESME Receiver Permanent App Error Code
        /// </summary>
        public const int ESME_RX_P_APPN = 0x00000065; // ESME Receiver Permanent App Error Code

        /// <summary>
        /// ESME Receiver Reject Message Error Code
        /// </summary>
        public const int ESME_RX_R_APPN = 0x00000066; // ESME Receiver Reject Message Error Code

        /// <summary>
        /// query_sm request failed
        /// </summary>
        public const int ESME_RQUERYFAIL = 0x00000067; // query_sm request failed

        /// <summary>
        /// Error in the optional part of the PDU Body.
        /// </summary>
        public const int ESME_RINVOPTPARSTREAM = 0x000000C0; // Error in the optional part of the PDU Body.

        /// <summary>
        /// Optional Parameter not allowed
        /// </summary>
        public const int ESME_ROPTPARNOTALLWD = 0x000000C1; // Optional Parameter not allowed

        /// <summary>
        /// Invalid Parameter Length.
        /// </summary>
        public const int ESME_RINVPARLEN = 0x000000C2; // Invalid Parameter Length.

        /// <summary>
        /// Expected Optional Parameter missing
        /// </summary>
        public const int ESME_RMISSINGOPTPARAM = 0x000000C3; // Expected Optional Parameter missing

        /// <summary>
        /// Invalid Optional Parameter Value
        /// </summary>
        public const int ESME_RINVOPTPARAMVAL = 0x000000C4; // Invalid Optional Parameter Value

        /// <summary>
        /// Delivery Failure (used for data_sm resp)
        /// </summary>
        public const int ESME_RDELIVERYFAILURE = 0x000000FE; // Delivery Failure (used for data_sm_resp)

        /// <summary>
        /// Unknown Error
        /// </summary>
        public const int ESME_RUNKNOWNERR = 0x000000FF; // Unknown Error


    }//StatusCodes

    public class PriorityFlags
    {
        public const byte Bulk = 0;
        public const byte Normal = 1;
        public const byte Urgent = 2;
        public const byte VeryUrgent = 3;
    }//PriorityFlags

    public class DeliveryReceipts
    {
        public const byte NoReceipt = 0;
        public const byte OnSuccessOrFailure = 1;
        public const byte OnFailure = 2;
    }//DeliveryReceipt

    public class ReplaceIfPresentFlags
    {
        public const byte DoNotReplace = 0;
        public const byte Replace = 1;
    }//ReplaceIfPresentFlag

    public class AddressTons
    {
        public const byte Unknown = 0;
        public const byte International = 1;
        public const byte National = 2;
        public const byte NetworkSpecific = 3;
        public const byte SubscriberNumber = 4;
        public const byte Alphanumeric = 5;
        public const byte Abbreviated = 6;
    }//AddressTon

    public class AddressNpis
    {
        public const byte Unknown = 0;
        public const byte ISDN = 1;
    }//AddressTon

    public class EsmClass
    {
        public class MessagingMode
        {
            public const byte Default = 0;
            public const byte Datagram = 1;
            public const byte Forward = 2;
            public const byte StoreAndForward = 3;
        }

        public class MessageType
        {
            public const byte Default = 0;
            public const byte ContainsMCDeliveryReceipt = 4;
            public const byte ContainsIntermediateDeliveryNotification = 32;
        }
        public class ANSI41
        {
            public const byte DeliveryAcknowledgement = 8;
            public const byte UserAcknowledgement = 16;
            public const byte ConversationAbort = 24;
        }
        public class GSM
        {
            public const byte Default = 0;
            public const byte UDHIndicator = 64;
            public const byte SetReplyPath = 128;
            public const byte SetEdhiAndReplyPath = 192;
        }
    }

    public class RegisteredDelivery
    {
        public class McDeliveryReceipt
        {
            public const byte NoReceipt = 0;
            public const byte Requested = 1;
            public const byte RequestedFailure = 2;
            public const byte RequestedSuccess = 3;
        }
        public class SmeAcknowledgement
        {
            public const byte NotRequested = 0;
            public const byte DeliveryAcknowledgement = 4;
            public const byte UserAcknowledgement = 8;
            public const byte DeliveryAndUserAcknowledgement = 12;
        }
        public class IntermediateNotification
        {
            public const byte NotRequested = 0;
            public const byte Requested = 16;
        }
    }

    public class ServiceType
    {
        public const string Default = "";
        public const string CellularMessaging = "CMT";
        public const string CellularPaging = "CPT";
        public const string VoiceMailNotification = "VMN";
        public const string VoiceMailAlerting = "VMA";
        public const string WirelessApplicationProtocol = "WAP";
        public const string UnstructuredSupplementaryServicesData = "USSD";
        public const string CellBroadcastService = "CBS";
        public const string GenericUDPTransportService = "GUTS";


    }

    public class UDHIndicators
    {
        /// <summary>
        /// 00 Concatenated short messages, 8-bit reference number
        /// </summary>
        public const byte b00 = 0x00;

        /// <summary>
        /// 01 Special SMS Message Indication
        /// </summary>
        public const byte b01 = 0x01;

        /// <summary>
        /// 02 Reserved
        /// </summary>
        public const byte b02 = 0x02;

        /// <summary>
        /// 03 Not used to avoid misinterpretation as <LF> character
        /// </summary>
        public const byte b03 = 0x03;

        /// <summary>
        /// 04 Application port addressing scheme, 8 bit address
        /// </summary>
        public const byte b04 = 0x04;

        /// <summary>
        /// 05 Application port addressing scheme, 16 bit address
        /// </summary>
        public const byte b05 = 0x05;

        /// <summary>
        /// 06 SMSC constrol Parameters
        /// </summary>
        public const byte b06 = 0x06;

        /// <summary>
        /// 07 UDH Source Indicator
        /// </summary>
        public const byte b07 = 0x07;

        /// <summary>
        /// 08 Concatenated short message, 16-bit reference number
        /// </summary>
        public const byte b08 = 0x08;

        /// <summary>
        /// 09 Wireless constrol Message Protocol
        /// </summary>
        public const byte b09 = 0x09;

        /// <summary>
        /// 0A Text Formatting
        /// </summary>
        public const byte b0A = 0x0A;

        /// <summary>
        /// 0B Predefined Sound
        /// </summary>
        public const byte b0B = 0x0B;

        /// <summary>
        /// 0C User Defined Sound (iMelody max 128 bytes)
        /// </summary>
        public const byte b0C = 0x0C;

        /// <summary>
        /// 0D Predefined Animation
        /// </summary>
        public const byte b0D = 0x0D;

        /// <summary>
        /// 0E Large Animation (16*16 times 4 = 32*4 =128 bytes)
        /// </summary>
        public const byte b0E = 0x0E;

        /// <summary>
        /// 0F Small Animation (8*8 times 4 = 8*4 =32 bytes)
        /// </summary>
        public const byte b0F = 0x0F;

        /// <summary>
        /// 10 Large Picture (32*32 = 128 bytes)
        /// </summary>
        public const byte b10 = 0x10;

        /// <summary>
        /// 11 Small Picture (16*16 = 32 bytes)
        /// </summary>
        public const byte b11 = 0x11;

        /// <summary>
        /// 12 Variable Picture
        /// </summary>
        public const byte b12 = 0x12;

        /// <summary>
        /// 13 User prompt indicator
        /// </summary>
        public const byte b13 = 0x13;

        /// <summary>
        /// 14 Extended Object
        /// </summary>
        public const byte b14 = 0x14;

        /// <summary>
        /// 15 Reused Extended Object
        /// </summary>
        public const byte b15 = 0x15;

        /// <summary>
        /// 16 Compression constrol
        /// </summary>
        public const byte b16 = 0x16;

        /// <summary>
        /// 17 Object Distribution Indicator
        /// </summary>
        public const byte b17 = 0x17;

        /// <summary>
        /// 18 Standard WVG object
        /// </summary>
        public const byte b18 = 0x18;

        /// <summary>
        /// 19 Character Size WVG object
        /// </summary>
        public const byte b19 = 0x19;

        /// <summary>
        /// 1A Extended Object Data Request Command
        /// </summary>
        public const byte b1A = 0x1A;

        /// <summary>
        /// 1B Reserved for future EMS features
        /// </summary>
        public const byte b1B = 0x1B;

        /// <summary>
        /// 1C Reserved for future EMS features
        /// </summary>
        public const byte b1C = 0x1C;

        /// <summary>
        /// 1D Reserved for future EMS features
        /// </summary>
        public const byte b1D = 0x1D;

        /// <summary>
        /// 1E Reserved for future EMS features
        /// </summary>
        public const byte b1E = 0x1E;

        /// <summary>
        /// 1F Reserved for future EMS features
        /// </summary>
        public const byte b1F = 0x1F;

        /// <summary>
        /// 20 RFC 822 E-Mail Header
        /// </summary>
        public const byte b20 = 0x20;

        /// <summary>
        /// 21 Hyperlink format element
        /// </summary>
        public const byte b21 = 0x21;

        /// <summary>
        /// 22 Reply Address Element
        /// </summary>
        public const byte b22 = 0x22;

        /// <summary>
        /// 23 Enhanced Voice Mail Information
        /// </summary>
        public const byte b23 = 0x23;

        /// <summary>
        /// 24 National Language Single Shift
        /// </summary>
        public const byte b24 = 0x24;

        /// <summary>
        /// 25 National Language Locking Shift
        /// </summary>
        public const byte b25 = 0x25;

        /// <summary>
        /// 26 – 6F Reserved for future use
        /// </summary>
        //public const byte b26 – 6F = 0x26 – 6F;

        /// <summary>
        /// 70 – 7F (U)SIM Toolkit Security Headers
        /// </summary>
        //public const byte b70 – 7F = 0x70 – 7F;

        /// <summary>
        /// 80 – 9F SME to SME specific use
        /// </summary>
        //public const byte b80 – 9F = 0x80 – 9F;

        /// <summary>
        /// A0 – BF Reserved for future use
        /// </summary>
        //public const byte bA0 – BF = 0xA0 – BF;

        /// <summary>
        /// C0 – DF SC specific use
        /// </summary>
        //public const byte bC0 – DF = 0xC0 – DF;

        /// <summary>
        /// E0 – FF Reserved for future use
        /// </summary>
        //public const byte bE0 – FF = 0xE0 – FF;




    }

    public enum MessageState
    {
        SCHEDULED = 0,
        ENROUTE = 1,
        DELIVERED = 2,
        EXPIRED = 3,
        DELETED = 4,
        UNDELIVERABLE = 5,
        ACCEPTED = 6,
        UNKNOWN = 7,
        REJECTED = 8,
        SKIPPED = 9
    }

    public class TagCodes
    {
        public const ushort DEST_ADDR_SUBUNIT = 0x0005;
        public const ushort DEST_NETWORK_TYPE = 0x0006;
        public const ushort DEST_BEARER_TYPE = 0x0007;
        public const ushort DEST_TELEMATICS_ID = 0x0008;
        public const ushort SOURCE_ADDR_SUBUNIT = 0x000D;
        public const ushort SOURCE_NETWORK_TYPE = 0x000E;
        public const ushort SOURCE_BEARER_TYPE = 0x000F;
        public const ushort SOURCE_TELEMATICS_ID = 0x0010;
        public const ushort QOS_TIME_TO_LIVE = 0x0017;
        public const ushort PAYLOAD_TYPE = 0x0019;
        public const ushort ADDITIONAL_STATUS_INFO_TEXT = 0x001D;
        public const ushort RECEIPTED_MESSAGE_ID = 0x001E;
        public const ushort MS_MSG_WAIT_FACILITIES = 0x0030;
        public const ushort PRIVACY_INDICATOR = 0x0201;
        public const ushort SOURCE_SUBADDRESS = 0x0202;
        public const ushort DEST_SUBADDRESS = 0x0203;
        public const ushort USER_MESSAGE_REFERENCE = 0x0204;
        public const ushort USER_RESPONSE_CODE = 0x0205;
        public const ushort SOURCE_PORT = 0x020A;
        public const ushort DEST_PORT = 0x020B;
        public const ushort SAR_MSG_REF_NUM = 0x020C;
        public const ushort LANGUAGE_INDICATOR = 0x020D;
        public const ushort SAR_TOTAL_SEGMENTS = 0x020E;
        public const ushort SAR_SEGMENT_SEQNUM = 0x020F;
        public const ushort SC_INTERFACE_VERSION = 0x0210;
        public const ushort CALLBACK_NUM_PRES_IND = 0x0302;
        public const ushort CALLBACK_NUM_ATAG = 0x0303;
        public const ushort NUMBER_OF_MESSAGES = 0x0304;
        public const ushort CALLBACK_NUM = 0x0381;
        public const ushort DPF_RESULT = 0x0420;
        public const ushort SET_DPF = 0x0421;
        public const ushort MS_AVAILABILITY_STATUS = 0x0422;
        public const ushort NETWORK_ERROR_CODE = 0x0423;
        public const ushort MESSAGE_PAYLOAD = 0x0424;
        public const ushort DELIVERY_FAILURE_REASON = 0x0425;
        public const ushort MORE_MESSAGES_TO_SEND = 0x0426;
        public const ushort MESSAGE_STATE = 0x0427;
        public const ushort CONGESTION_STATE = 0x0428;
        public const ushort USSD_SERVICE_OP = 0x0501;
        public const ushort BROADCAST_CHANNEL_INDICATOR = 0x0600;
        public const ushort BROADCAST_CONTENT_TYPE = 0x0601;
        public const ushort BROADCAST_CONTENT_TYPE_INFO = 0x0602;
        public const ushort BROADCAST_MESSAGE_CLASS = 0x0603;
        public const ushort BROADCAST_REP_NUM = 0x0604;
        public const ushort BROADCAST_FREQUENCY_INTERVAL = 0x0605;
        public const ushort BROADCAST_AREA_IDENTIFIER = 0x0606;
        public const ushort BROADCAST_ERROR_STATUS = 0x0607;
        public const ushort BROADCAST_AREA_SUCCESS = 0x0608;
        public const ushort BROADCAST_END_TIME = 0x0609;
        public const ushort BROADCAST_SERVICE_GROUP = 0x060A;
        public const ushort BILLING_IDENTIFICATION = 0x060B;
        public const ushort SOURCE_NETWORK_ID = 0x060D;
        public const ushort DEST_NETWORK_ID = 0x060E;
        public const ushort SOURCE_NODE_ID = 0x060F;
        public const ushort DEST_NODE_ID = 0x0610;
        public const ushort DEST_ADDR_NP_RESOLUTION = 0x0611;
        public const ushort DEST_ADDR_NP_INFORMATION = 0x0612;
        public const ushort DEST_ADDR_NP_COUNTRY = 0x0613;
        public const ushort DISPLAY_TIME = 0x1201;
        public const ushort SMS_SIGNAL = 0x1203;
        public const ushort MS_VALIDITY = 0x1204;
        public const ushort ALERT_ON_MESSAGE_DELIVERY = 0x130C;
        public const ushort ITS_REPLY_TYPE = 0x1380;
        public const ushort ITS_SESSION_INFO = 0x1383;

        // 0x1400 - 0x3FFF Reserved for MC Vendor specific TLVs
    }

    public enum DeliveryStatus
    {
        SCHEDULED,
        ENROUTE,
        DELIVERED,
        EXPIRED,
        DELETED,
        UNDELIVERABLE
    }


    public enum MessageEncoding
    {
        DEFAULT = 0b00000000, // 0
        IA5_ASCII = 0b00000001, // 1
        OCTET_UNSPECIFIED0 = 0b00000010, // 2
        LATIN_1 = 0b00000011, // 3
        OCTET_UNSPECIFIED1 = 0b00000100, // 4
        JIS = 0b00000101, // 5
        CRYLLIC = 0b00000110, // 6
        UCS2 = 0b00001000, // 8
        SILENT_GSM = 0b11000000, // 192
        SILENT_CRYLLIC = 0b11000110, // 198
        SILENT_FLASH = 0b11010000, // 208
        UNICODE_FLASH = 0b00011000  // 24
    }

    //public enum SMSCType
    //{
    //    Primary,
    //    Secondary
    //}


    public enum MessageIdType
    {
        DEC_DEC = 0,
        DEC_HEX = 1,
        HEX_DEC = 2,
        HEX_HEX = 3,
        STR = -1,
    }
}
