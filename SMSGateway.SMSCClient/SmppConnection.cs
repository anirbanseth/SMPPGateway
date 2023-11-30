using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Configuration;
using System.Reflection;
using System.Net;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Concurrent;
using SMSGateway.Tools;
using System.Diagnostics;

namespace SMSGateway.SMSCClient
{

    public class ConnectionStateObject
    {
        //public Socket workSocket = null;								// Client socket.
        public TcpClient workSocket = null;								// Client socket.
        public SslStream stream = null;								    // Client stream.
        public static int BufferSize = KernelParameters.MaxBufferSize;      // Size of receive buffer.
        public int Position = 0;										// Size of receive buffer.
        public byte[] buffer = new byte[BufferSize];					// receive buffer.
    }

    public partial class SMPPConnection : IDisposable
    {
        #region Private variables
        static int Instance;
        private int ConnectionNumber;
        //private Socket clientSocket;
        private TcpClient clientSocket;
        private Stream sslStream;
        //private Stream stream;

        private int connectionState;

        private DateTime enquireLinkSendTime;
        private DateTime enquireLinkResponseTime;
        private DateTime lastSeenConnected;
        private DateTime lastPacketSentTime;

        private Timer enquireLinkTimer;
        private int undeliveredMessages = 0;

        private Timer deliveryTimer;
        private Timer deliveryReportTimer;
        private Timer deliveryReportTimeoutTimer;
        private Timer messagePartsTimer;

        //private SMSCArray smscArray = new SMSCArray();

        private byte[] mbResponse = new byte[KernelParameters.MaxBufferSize];
        private int mPos = 0;
        private int mLen = 0;

        private bool mustBeConnected;

        private int logLevel = 0;
        private byte askDeliveryReceipt = KernelParameters.AskDeliveryReceipt;
        private bool splitLongText = KernelParameters.SplitLongText;
        private int nationalNumberLength = KernelParameters.NationalNumberLength;
        private bool useEnquireLink = KernelParameters.UseEnquireLink;
        private int enquireLinkTimeout = KernelParameters.EnquireLinkTimeout;
        private int reconnectTimeout = KernelParameters.ReconnectTimeout;
        private int deliveryLoadTimeout = KernelParameters.DeliveryLoadTimeout;
        private int deliverySendTimeout = KernelParameters.DeliverySendTimeout;
        private int deliveryPurgeTimeout = 1000 * 2 * KernelParameters.WaitPacketResponse;
        private SortedList sarMessages = SortedList.Synchronized(new SortedList());
        private SortedList submittedMessages = SortedList.Synchronized(new SortedList());
        //private string gToNo = "";
        //private string gIDNo = "";
        private readonly string DeliverySmDateFormat = "yyMMddHHmmss"; //ConfigurationManager.AppSettings["DeliverySmDateFormat"];
        public SMSC MC { get; }
        private SMSC esme;
        public SMSC ESME { get { return esme; } }
        public object Identifier { get; set; }
        private ConnectionType ConnectionType;

        private object sendingDeliveryLock = new object();
        //private int sendingDelivery;
        //public Queue DeliveryQueue;
        public ConcurrentQueue<SmppDelivery> DeliveryQueue;

        //private object PendingDeliveryLock = new object();
        //private Dictionary<uint, SmppDelivery> PendingDelivery;
        private ConcurrentDictionary<uint, SmppDelivery> PendingDelivery;
        //private SortedList PendingDelivery;
        private const int MaxDeliverySendingQueue = 100;

        public DateTime LastConnected
        {
            get
            {
                if (lastPacketSentTime > DateTime.Now)
                    return lastSeenConnected;
                else if (lastSeenConnected > lastPacketSentTime)
                    return lastSeenConnected;
                else
                    return lastPacketSentTime;
            }
        }
        CancellationTokenSource disconnectTokenSource = new CancellationTokenSource();
        //public SortedList<int, SortedList<int, SmppText>> MessageBuilder { get; set; }
        public SortedList MessageBuilder { get; set; }
        //public List<KeyValuePair<ulong,DateTime>> MessageBuilderHash;
        //public Hashtable MessageBuilderHash;
        public Dictionary<ulong, DateTime> MessageBuilderHash;
        #endregion Private variables

        #region Public Functions

        #region [ Constructor ]
        public SMPPConnection(
            SMSC smsc,
            TcpClient tcpClient,
            string deliverySmDateFormat = "yyMMddHHmmss",
            SesionCreatedHandler onSessionStart = null,
            //Func<Task> onSessionStart = null,
            SmppType smppType = SmppType.Server
        )
        {
            ConnectionNumber = Instance++;

            ProcessingMessages = SortedList.Synchronized(new SortedList());

            this.MC = smsc;
            this.clientSocket = tcpClient;
            this.DeliverySmDateFormat = deliverySmDateFormat;

            //this.connectionState = ConnectionStates.SMPP_SOCKET_DISCONNECTED;
            this.connectionState = new SynchronizedObject<int>(ConnectionStates.SMPP_SOCKET_DISCONNECTED);

            this.enquireLinkSendTime = DateTime.Now;
            this.enquireLinkResponseTime = enquireLinkSendTime.AddSeconds(1);
            this.lastSeenConnected = DateTime.Now;
            this.lastPacketSentTime = DateTime.MaxValue;

            this.mustBeConnected = false;

            //this.DeliveryQueue = Queue.Synchronized(new Queue(1000));
            this.DeliveryQueue = new ConcurrentQueue<SmppDelivery>();
            //this.PendingDelivery = new Dictionary<uint, SmppDelivery>();
            this.PendingDelivery = new ConcurrentDictionary<uint, SmppDelivery>();
            //this.PendingDelivery = SortedList.Synchronized(new SortedList());
            MessageBuilder = SortedList.Synchronized(new SortedList());
            //MessageBuilderHash = new List<KeyValuePair<ulong, DateTime>>();
            //MessageBuilderHash = new Hashtable();
            MessageBuilderHash = new Dictionary<ulong, DateTime>();
            initParameters();

            TimerCallback timerDelegate = new TimerCallback(checkSystemIntegrity);
            this.enquireLinkTimer = new Timer(timerDelegate, null, Timeout.Infinite, Timeout.Infinite);

            TimerCallback deliveryTimerDelegate = new TimerCallback(deliveryTimerTick);
            this.deliveryTimer = new Timer(deliveryTimerDelegate, null, Timeout.Infinite, Timeout.Infinite);

            TimerCallback deliveryReportTimerDelegate = new TimerCallback(deliveryReportTimerCallback);
            this.deliveryReportTimer = new Timer(deliveryReportTimerDelegate, null, Timeout.Infinite, Timeout.Infinite);

            TimerCallback deliveryReportTimeoutTimerDelegate = new TimerCallback(deliveryReportTimeoutTimerCallback);
            this.deliveryReportTimeoutTimer = new Timer(deliveryReportTimeoutTimerDelegate, null, Timeout.Infinite, Timeout.Infinite);

            TimerCallback messagePartsTimerDelegate = new TimerCallback(messagePartsTimerCallback);
            this.messagePartsTimer = new Timer(messagePartsTimerDelegate, null, 60 * 1000, 60 * 1000);


            if (!ReferenceEquals(clientSocket, null))
            {
                if (MC.Secured)
                {
                    //ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    //X509Certificate serverCertificate = getServerCertificate();

                    DirectoryInfo dirInfo = new DirectoryInfo(ConfigurationManager.AppSettings["CertificateDir"]);
                    var files = dirInfo.GetFiles("*.pfx");
                    X509Certificate serverCertificate = new X509Certificate(files[0].FullName, "123456");

                    X509Certificate2 certificate = new X509Certificate2(serverCertificate);
                    sslStream = new SslStream(
                        clientSocket.GetStream(),
                        false,
                        new RemoteCertificateValidationCallback(ValidateServerCertificate),
                        new LocalCertificateSelectionCallback(ValidateClientCertificate)
                    );
                    ((SslStream)sslStream).AuthenticateAsServer(certificate, true, System.Security.Authentication.SslProtocols.Tls, false);
                }
                else
                {
                    sslStream = clientSocket.GetStream();
                }

                this.OnClientSessionStart = onSessionStart;

                if (OnClientSessionStart != null)
                {
                    //var pi = sslStream.GetType().GetProperty("Socket", BindingFlags.NonPublic | BindingFlags.Instance);
                    //var socketIp = ((Socket)pi.GetValue(sslStream, null)).RemoteEndPoint.ToString();
                    SessionEventArgs session = new SessionEventArgs
                    {
                        Id = Guid.NewGuid(),
                        Address = tcpClient.Client.RemoteEndPoint.ToString()
                    };
                    //OnSessionStart(this, session);
                    logMessage(LogLevels.LogSteps, String.Format("Creating Connection {0} :: Instance {1}", session.Id, ConnectionNumber));
                    connectionState = ConnectionStates.SMPP_SOCKET_CONNECT_SENT;
                    //OnSessionStart.BeginInvoke(this, session, onSessionStartEventComplete, session);
                    foreach (Func<Task> handler in OnClientSessionStart.GetInvocationList())
                    {
                        handler.Invoke().Wait();
                    }
                }
                //else
                //{
                //    receive();
                //}
                try
                {
                    connectionState = ConnectionStates.SMPP_SOCKET_CONNECTED;
                    logMessage(LogLevels.LogSteps, "Connection Created :: " + ConnectionNumber.ToString());
                    receive();
                }
                catch (Exception ex)
                {
                    logMessage(LogLevels.LogExceptions, "onSessionStartEventComplete | " + ex.ToString());
                    this.Disconnect();
                    this.Dispose();
                }
            }
        }//SMPPClient


        public SMPPConnection(
            SMSC smsc,
            BindEventHandler onSmppBinded = null,
            SmppType smppType = SmppType.Client
        )
        {
            ConnectionNumber = Instance++;

            ProcessingMessages = SortedList.Synchronized(new SortedList());

            this.MC = smsc;
            //this.clientSocket = tcpClient;
            this.clientSocket = new TcpClient();
            //this.clientSocket.BeginConnect(smsc.Host, smsc.Port, new AsyncCallback(connectCallback), clientSocket);
            this.DeliverySmDateFormat = smsc.DeliveryDateFormat;

            //this.connectionState = ConnectionStates.SMPP_SOCKET_DISCONNECTED;
            this.connectionState = new SynchronizedObject<int>(ConnectionStates.SMPP_SOCKET_DISCONNECTED);

            this.enquireLinkSendTime = DateTime.Now;
            this.enquireLinkResponseTime = enquireLinkSendTime.AddSeconds(1);
            this.lastSeenConnected = DateTime.Now;
            this.lastPacketSentTime = DateTime.MaxValue;

            this.mustBeConnected = true;

            //this.DeliveryQueue = Queue.Synchronized(new Queue(1000));
            this.DeliveryQueue = new ConcurrentQueue<SmppDelivery>();
            //this.PendingDelivery = new Dictionary<uint, SmppDelivery>();
            this.PendingDelivery = new ConcurrentDictionary<uint, SmppDelivery>();
            //this.PendingDelivery = SortedList.Synchronized(new SortedList());
            MessageBuilder = SortedList.Synchronized(new SortedList());
            //MessageBuilderHash = new List<KeyValuePair<ulong, DateTime>>();
            //MessageBuilderHash = new Hashtable();
            MessageBuilderHash = new Dictionary<ulong, DateTime>();
            //initParameters();

            TimerCallback timerDelegate = new TimerCallback(checkSystemIntegrity);
            this.enquireLinkTimer = new Timer(timerDelegate, null, KernelParameters.EnquireLinkTimeout, KernelParameters.EnquireLinkTimeout);

            TimerCallback deliveryTimerDelegate = new TimerCallback(deliveryTimerTick);
            this.deliveryTimer = new Timer(deliveryTimerDelegate, null, Timeout.Infinite, Timeout.Infinite);

            TimerCallback deliveryReportTimerDelegate = new TimerCallback(deliveryReportTimerCallback);
            this.deliveryReportTimer = new Timer(deliveryReportTimerDelegate, null, Timeout.Infinite, Timeout.Infinite);

            TimerCallback deliveryReportTimeoutTimerDelegate = new TimerCallback(deliveryReportTimeoutTimerCallback);
            this.deliveryReportTimeoutTimer = new Timer(deliveryReportTimeoutTimerDelegate, null, Timeout.Infinite, Timeout.Infinite);

            TimerCallback messagePartsTimerDelegate = new TimerCallback(messagePartsTimerCallback);
            this.messagePartsTimer = new Timer(messagePartsTimerDelegate, null, 60 * 1000, 60 * 1000);


            submittedMessages = SortedList.Synchronized(new SortedList());
            connectToSMSC();
        }//SMPPClient

        //private void onSessionStartEventComplete(IAsyncResult iar)
        //{
        //    var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //    var invokedMethod = (SesionCreatedHandler)ar.AsyncDelegate;
        //    var args = (SessionEventArgs)iar.AsyncState;

        //    try
        //    {
        //        connectionState = ConnectionStates.SMPP_SOCKET_CONNECTED;
        //        invokedMethod.EndInvoke(iar);
        //        logMessage(LogLevels.LogSteps, "Connection Created :: " + ConnectionNumber.ToString());
        //        receive();
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "onSessionStartEventComplete | " + ex.ToString());
        //        this.Disconnect();
        //        this.Dispose();
        //    }
        //}
        #endregion

        //public void Connect()
        //{
        //    try
        //    {
        //        mustBeConnected = true;
        //        connectToSMSC();
        //        //unBind();
        //        //disconnectSocket();
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "connectToSMSC | " + ex.ToString());
        //    }
        //}//connectToSMSC

        #region [ Disconnect ]



        public void Disconnect()
        {
            System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
            logMessage(LogLevels.LogInfo, t.ToString());

            try
            {
                logMessage(LogLevels.LogSteps, String.Format("DisconnectFromSMSC | Begin Disconnecting"));

                mustBeConnected = false;
                connectionState = ConnectionStates.SMPP_UNBIND_PENDING;

                //logMessage(LogLevels.LogSteps, String.Format("Disable enquireLinkTimer"));
                if (!ReferenceEquals(enquireLinkTimer, null))
                    enquireLinkTimer.Change(Timeout.Infinite, Timeout.Infinite);

                //logMessage(LogLevels.LogSteps, String.Format("Disable deliveryTimer"));
                if (!ReferenceEquals(deliveryTimer, null))
                    deliveryTimer.Change(Timeout.Infinite, Timeout.Infinite);

                if (!ReferenceEquals(deliveryReportTimeoutTimer, null))
                    deliveryReportTimeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);

                //logMessage(LogLevels.LogSteps, String.Format("Disable deliveryReportTimer"));
                if (!ReferenceEquals(deliveryReportTimer, null))
                    deliveryReportTimer.Change(Timeout.Infinite, Timeout.Infinite);

                if (!ReferenceEquals(messagePartsTimer, null))
                    messagePartsTimer.Change(Timeout.Infinite, Timeout.Infinite);

                while (this.MessageBuilder.Count > 0)
                {
                    logMessage(LogLevels.LogSteps, String.Format("DisconnectFromSMSC | Waiting for pending messages to complete"));
                    Task.Run(() => { CleanMessageParts(2); }).Wait();
                    Task.Delay(100).Wait();
                }

                //logMessage(LogLevels.LogSteps, String.Format("Sending Unbind"));
                unBind();
                //Thread.Sleep(10000);
                //disconnectSocket();
                //logMessage(LogLevels.LogSteps, String.Format("Unbind Sent"));

                CancellationToken disconnectToken = disconnectTokenSource.Token;

                Task.Delay(10000, disconnectToken)
                    .ContinueWith((task) => {
                        if (task.IsCanceled)
                        {
                            return;
                        }
                        else if (task.IsFaulted)
                        {
                            throw task.Exception;
                        }
                        else if (task.IsCompleted)
                        {
                            logMessage(
                                LogLevels.LogSteps,
                                String.Format(
                                    "Unbind without response, terminated Connection {0} sucessfully :: Instance {1}",
                                    ReferenceEquals(this.Identifier, null) ? new Guid() : ((SmppSession)this.Identifier).Id,
                                    ConnectionNumber
                                )
                            );
                        }
                    }).Wait();
                //if (connectionState != ConnectionStates.SMPP_SOCKET_DISCONNECTED)
                //    this.Dispose();

                logMessage(LogLevels.LogSteps, String.Format("DisconnectFromSMSC | Disconnected"));



            }
            catch (ObjectDisposedException ex)
            {

            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "DisconnectFromSMSC | " + ex.ToString());
            }
        }//DisconnectFromSMSC
        #endregion




        //public void ClearSMSC()
        //{
        //    try
        //    {
        //        smscArray.Clear();
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "AddSMSC | " + ex.ToString());
        //    }

        //}//ClearSMSC

        //public void AddSMSC(SMSC mSMSC)
        //{
        //    try
        //    {
        //        smscArray.AddSMSC(mSMSC);
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "AddSMSC | " + ex.ToString());
        //    }

        //}//AddSMSC

        //public int SendDeliveyReport(
        //    string from, 
        //    string to, 
        //    string messageText, 
        //    string messageId, 
        //    DeliveryStatus status, 
        //    DateTime? delivery_time,
        //    ref SmppEventArgs args
        //)
        //{
        //    int result = -1;
        //    try
        //    {
        //        if (CanSend)
        //        {
        //            //int sequenceNo = -1;
        //            byte sourceAddressTon = 5;
        //            byte sourceAddressNpi = 9;
        //            string sourceAddress;
        //            byte destinationAddressTon = 1;
        //            byte destinationAddressNpi = 1;
        //            string destinationAddress;
        //            byte registeredDelivery;
        //            //byte maxLength;
        //            //byte splitSize;

        //            sourceAddress = Utility.GetString(from, 20, "");

        //            destinationAddress = Utility.GetString(to, 20, "");

        //            registeredDelivery = askDeliveryReceipt;

        //            byte protocolId = 0;
        //            byte priorityFlag = PriorityFlags.VeryUrgent;
        //            //DateTime sheduleDeliveryTime = DateTime.MinValue;
        //            //DateTime validityPeriod = DateTime.MinValue;
        //            byte replaceIfPresentFlag = ReplaceIfPresentFlags.DoNotReplace;
        //            byte smDefaultMsgId = 0;

        //            //byte[] message = Utility.ConvertStringToByteArray(messageText.Length > 255 ? messageText.Substring(0, 255) : messageText);
        //            //string smsText;

        //            List<OptionalParameter> parameters = new List<OptionalParameter>();


        //            parameters.Add(new OptionalParameter(TagCodes.RECEIPTED_MESSAGE_ID, Utility.ConvertStringToByteArray(messageId)));
        //            parameters.Add(new OptionalParameter(TagCodes.MESSAGE_STATE, new byte[1] { (byte) status }));


        //            string sServiceType = Utility.GetString("", 5, "");
        //            byte[] _service_type = new byte[sServiceType.Length + 1];
        //            Array.Copy(Utility.ConvertStringToByteArray(sServiceType), 0, _service_type, 0, sServiceType.Length);

        //            string sSourceAddress = Utility.GetString(sourceAddress, 20, "");
        //            byte[] _source_addr = new byte[sSourceAddress.Length + 1];
        //            Array.Copy(Utility.ConvertStringToByteArray(sSourceAddress), 0, _source_addr, 0, sSourceAddress.Length);

        //            string sDestAddr = Utility.GetString(destinationAddress, 20, "");
        //            byte[] _dest_addr = new byte[sDestAddr.Length + 1];
        //            Array.Copy(Utility.ConvertStringToByteArray(sDestAddr), 0, _dest_addr, 0, sDestAddr.Length);

        //            string sScheduledDeliveryTime = ReferenceEquals(delivery_time, null) ? "" : Utility.GetDateString((DateTime)delivery_time);
        //            byte[] _scheduled_delivery_time = new byte[sScheduledDeliveryTime.Length + 1];
        //            Array.Copy(Utility.ConvertStringToByteArray(sScheduledDeliveryTime), 0, _scheduled_delivery_time, 0, sScheduledDeliveryTime.Length);

        //            string sValidityPeriod = ReferenceEquals(delivery_time, null) ? "" : Utility.GetDateString((DateTime)delivery_time);
        //            byte[] _validity_period = new byte[sValidityPeriod.Length + 1];
        //            Array.Copy(Utility.ConvertStringToByteArray(sValidityPeriod), 0, _validity_period, 0, sValidityPeriod.Length);

        //            string sShortMessage = Utility.GetString(messageText, 254, "");
        //            byte[] _short_message = new byte[sShortMessage.Length + 1];
        //            Array.Copy(Utility.ConvertStringToByteArray(sShortMessage), 0, _short_message, 0, sShortMessage.Length);


        //            result = sendDeliverSm(
        //                _service_type,
        //                sourceAddressTon,
        //                sourceAddressNpi,
        //                _source_addr,
        //                destinationAddressTon,
        //                destinationAddressNpi,
        //                _dest_addr,
        //                0x04, // esm-class
        //                protocolId,
        //                priorityFlag,
        //                _scheduled_delivery_time,
        //                _validity_period,
        //                registeredDelivery,
        //                replaceIfPresentFlag,
        //                0x00,
        //                smDefaultMsgId,
        //                (byte)_short_message.Length,
        //                _short_message,
        //                parameters,
        //                ref args
        //            );

        //            //if (result != 0)
        //            //    throw new SmppException(StatusCodes.ESME_RSYSERR);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "SendDeliveyReport | " + ex.ToString());
        //    }
        //    return result;
        //}

        #region [ Send Message ]
        /// <summary>
        /// Send message using smpp connection
        /// </summary>
        /// <param name="from">Source Number / Code</param>
        /// <param name="to">Destinatio Number</param>
        /// <param name="splitLongText">Split long text</param>
        /// <param name="text">Message Text</param>
        /// <param name="askDeliveryReceipt">Request delivery receipt</param>
        /// <param name="priorityFlag">Message priority : Bulk = 0, Normal = 1, Urgent = 2, VeryUrgent = 3</param>
        /// <param name="dataEncoding">Text data encoding</param>
        /// <param name="dataCoding">SMPP encoding to use</param>
        /// <param name="refid"></param>
        /// <param name="peId">PE ID</param>
        /// <param name="templateId">Template ID</param>
        /// <param name="tmId">TM ID</param>
        /// <param name="retryIndex">Retry Number</param>
        /// <returns></returns>
        public int SendSms(
            String from,
            String to,
            bool splitLongText,
            String text,
            byte askDeliveryReceipt,
            byte priorityFlag,
            //byte esmClass, 
            Encoding dataEncoding,
            MessageEncoding dataCoding,
            string refid,
            string peId,
            string templateId,
            string tmId,
            int retryIndex = 0,
            IDictionary<string, object>? additionalParameters = null
        )
        {
            int sequenceNo = -1;
            byte sourceAddressTon = 5;
            byte sourceAddressNpi = 9;
            string sourceAddress;
            byte destinationAddressTon = 1;
            byte destinationAddressNpi = 1;
            string destinationAddress;
            byte registeredDelivery;
            int maxLength = SmsEncoding.MaxTextLength[(int)dataCoding];
            int splitSize = SmsEncoding.SplitSize[(int)dataCoding];//maxLength;

            sourceAddress = Utility.GetString(from, 20, "");

            destinationAddress = Utility.GetString(to, 20, "");

            registeredDelivery = askDeliveryReceipt;

            byte protocolId = 0;
            //byte priorityFlag = PriorityFlags.VeryUrgent;
            DateTime sheduleDeliveryTime = DateTime.MinValue;
            DateTime validityPeriod = DateTime.MinValue;
            byte replaceIfPresentFlag = ReplaceIfPresentFlags.DoNotReplace;
            byte smDefaultMsgId = 0;

            List<byte[]> messages = new List<byte[]>();
            string smsText = text;
            bool isLongMessage = smsText.Length > maxLength;
            int dstCoding = (int)(dataCoding == MessageEncoding.DEFAULT ? this.MC.DefaultEncoding : dataCoding);

            //splitSize = (maxLength / SmsEncoding.DataSize[(int)dstCoding]);

            //Logger.Write(LogType.Steps, String.Format("{0} Message with length {3} in Coding {1} {2}", isLongMessage ? "Extended" : "Normal", (int)dataCoding, dataCoding, smsText.Length));
            logMessage(LogType.Steps, String.Format("{0} Message with length {3} in Coding {1} {2}", isLongMessage ? "Extended" : "Normal", (int)dataCoding, dataCoding, smsText.Length));

            //if (isLongMessage)
            //    splitSize = SmsEncoding.SplitSize[(int)dstCoding];
            if (!isLongMessage)
                splitSize = maxLength;

            logMessage(LogType.Steps, String.Format("Max Length : {0}\tData Size : {1}\tSplit size : {2}", maxLength, SmsEncoding.DataSize[(int)dstCoding], splitSize));

            int index = 0;
            byte[] srcBytes;
            byte[] destBytes;
            while (index < smsText.Length)
            {
                int count =  splitSize > (smsText.Length - index) ? (smsText.Length - index) : splitSize;
                logMessage(LogType.Steps, String.Format("COUNT:{0}", count));
                byte[] messageBytes;
                do
                {
                    logMessage(LogType.Steps, String.Format("STR  :{0:000}:{1}", smsText.Substring(index, count).Length, smsText.Substring(index, count)));
                    //int srcCoding = dataCoding;

                    string str = smsText.Substring(index, count);
                    srcBytes = dataEncoding.GetBytes(str);

                    string encName = "";
                    if (SmsEncoding.Encodings[(int)dataCoding].GetType() == typeof(Mediaburst.Text.GSMEncoding))
                        encName = Mediaburst.Text.GSMEncoding.EncodingName;
                    else
                        encName = (SmsEncoding.Encodings[(int)dataCoding]).EncodingName;
                    if (dataEncoding.EncodingName != encName)
                    {
                        destBytes = Encoding.Convert(dataEncoding, SmsEncoding.Encodings[dstCoding], srcBytes);
                        //if (dstCoding == 0)
                        //    destBytes = PduBitPacker.PackBytes(destBytes);
                    }
                    else
                    {
                        destBytes = srcBytes;
                    }
                    messageBytes = destBytes;
                    if (messageBytes.Length <= (isLongMessage ? SmsEncoding.DataBufferSize : SmsEncoding.MaxTextLength)[(int)dataCoding])
                        break;
                    count--;
                } while (true);

                byte[] b = new byte[messageBytes.Length];
                Array.Copy(messageBytes, 0, b, 0, messageBytes.Length);
                messages.Add(b);
                index += count;
            }

            if (messages.Count == 1)
            {
                sequenceNo = SubmitSM(sourceAddressTon, sourceAddressNpi, sourceAddress,
                                            destinationAddressTon, destinationAddressNpi, destinationAddress,
                                            //esmClass, protocolId, priorityFlag,
                                            0x00, protocolId, priorityFlag,
                                            sheduleDeliveryTime, validityPeriod, registeredDelivery, replaceIfPresentFlag,
                                            (byte)dataCoding, smDefaultMsgId, messages[0], peId, templateId, tmId,
                                            refid, retryIndex, additionalParameters);


                Task.Run(() => this.OnSendSms(this, 
                    new SendSmsEventArgs { 
                        SourceAddress = from,
                        DestinationAddress = to,
                        MessageContent = text,
                        MessageCount = 1,
                        DataCoding = (MessageEncoding) dstCoding,
                        PEID = peId,
                        TMID = tmId,
                        TemplateID = templateId,
                        RefId = refid,
                        RetryIndex = retryIndex,
                        SentOn = DateTime.Now
                    }
                ));

                #region [ Database Entry ]
                ////For DataBase Entry
                //LocalStorage ls = new LocalStorage();
                //string sql = String.Format(
                //            @"INSERT INTO [SMSPush] (
                //                [Source], [Destination], [Message], [PeId], [TemplateId], [TmId]
                //                , [Vendor], [Connection], [SequenceNo], [RefID], [RetryIndex], [DataCoding], [CreationDate]
                //            ) 
                //            VALUES (
                //                '{0}', '{1}', '{2}', '{3}', '{4}', '{5}', 
                //                '{6}', '{7}', {8}, {9}, {10}, {11}, '{12:yyyy-MM-ddTHH:mm:ss.fff}'
                //            )"
                //            , sourceAddress
                //            , destinationAddress
                //            , Utility.RemoveApostropy(text)
                //            , peId
                //            , templateId
                //            , tmId
                //            , this.MC.Operator
                //            , $"{this.MC.Operator}_{ this.MC.Instance}"
                //            , sequenceNo
                //            , id
                //            , retryIndex
                //            , (byte)dataCoding
                //            , DateTime.Now
                //        );
                //LocalStorage.ExecuteNonQuery(sql);
                #endregion
            }
            else
            {
                byte messageIdentification = this.MC.MessageIdentificationNumber; //(byte) new Random(255).Next();
                logMessage(LogType.Steps, String.Format("Message split in {0} blocks", messages.Count));
                logMessage(LogType.Steps, String.Format("Message Identificaiton Number is {0}", messageIdentification));

                for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
                {
                    string strMessage = Utility.GetString((byte)dataCoding, messages[messageIndex], messages[messageIndex].Length);
                    logMessage(LogType.Steps, String.Format("Sending {0}/{1} - {2}", messageIndex + 1, messages.Count, strMessage));

                    sequenceNo = SubmitSMExtended(sourceAddressTon, sourceAddressNpi, sourceAddress,
                                            destinationAddressTon, destinationAddressNpi, destinationAddress,
                                            //esmClass, protocolId, priorityFlag,
                                            0x40, protocolId, priorityFlag,
                                            sheduleDeliveryTime, validityPeriod, registeredDelivery, replaceIfPresentFlag,
                                            (byte)dataCoding, smDefaultMsgId, messages[messageIndex], messageIdentification, 
                                            (byte)(messageIndex + 1), (byte)(messages.Count), peId, templateId, tmId, 
                                            refid, retryIndex, additionalParameters);

                    #region [ Database Entry ]
                    ////For DataBase Entry
                    //LocalStorage ls = new LocalStorage();
                    //string sql = String.Format(
                    //        @"INSERT INTO [SMSPush] (
                    //            [Source], [Destination], [Message], [PeId], [TemplateId], [TmId]
                    //            , [Vendor], [Connection], [SequenceNo], [RefID], [RetryIndex], [DataCoding], [CreationDate]
                    //        ) 
                    //        VALUES (
                    //            '{0}', '{1}', '{2}', '{3}', '{4}', '{5}', 
                    //            '{6}', '{7}', {8}, {9}, {10}, {11}, '{12:yyyy-MM-ddTHH:mm:ss.fff}'
                    //        )"
                    //        , sourceAddress
                    //        , destinationAddress
                    //        //, Utility.RemoveApostropy(textMessages[messageIndex])
                    //        , Utility.RemoveApostropy(strMessage)
                    //        , peId
                    //        , templateId
                    //        , tmId
                    //        , this.MC.Operator
                    //        , $"{this.MC.Operator}_{this.MC.Instance}"
                    //        , sequenceNo
                    //        , id
                    //        , retryIndex
                    //        , (byte)dataCoding
                    //        , DateTime.Now
                    //    );
                    //LocalStorage.ExecuteNonQuery(sql);
                    #endregion
                }

                Task.Run(() => this.OnSendSms(this,
                    new SendSmsEventArgs
                    {
                        SourceAddress = from,
                        DestinationAddress = to,
                        MessageContent = text,
                        MessageCount = messages.Count,
                        DataCoding = (MessageEncoding)dstCoding,
                        PEID = peId,
                        TMID = tmId,
                        TemplateID = templateId,
                        RefId = refid,
                        RetryIndex = retryIndex,
                        SentOn = DateTime.Now,
                        AdditionalParameters = additionalParameters
                    }
                ));
            }
            //return sequenceNo;
            return messages.Count;
        }//SendSms
        #endregion


        #region [ Submit SM - Send Message ]
        public int SubmitSM(byte sourceAddressTon, byte sourceAddressNpi, string sourceAddress,
                                byte destinationAddressTon, byte destinationAddressNpi, string destinationAddress,
                                byte esmClass, byte protocolId, byte priorityFlag,
                                DateTime sheduleDeliveryTime, DateTime validityPeriod, byte registeredDelivery,
                                byte replaceIfPresentFlag, byte dataCoding, byte smDefaultMsgId,
                                byte[] message, string peId, string templateId, string tmId, string? refid = null, 
                                int retryIndex = 0, IDictionary<string, object>? additionalParameters = null)
        {
            try
            {
                if (!CanSend)
                    return -1;

                byte[] _destination_addr;
                byte[] _source_addr;
                byte[] _SUBMIT_SM_PDU;
                byte[] _shedule_delivery_time;
                byte[] _validity_period;
                uint _sequence_number;
                int pos;
                byte _sm_length;


                _SUBMIT_SM_PDU = new byte[KernelParameters.MaxPduSize];

                ////////////////////////////////////////////////////////////////////////////////////////////////
                /// Start filling PDU						

                Utility.CopyIntToArray(0x00000004, _SUBMIT_SM_PDU, 4); //command_id
                _sequence_number = MC.SequenceNumber;
                Utility.CopyIntToArray(_sequence_number, _SUBMIT_SM_PDU, 12); //sequence_number
                pos = 16;
                _SUBMIT_SM_PDU[pos] = 0x00; //service_type
                pos += 1;
                _SUBMIT_SM_PDU[pos] = sourceAddressTon; //source_addr_ton
                pos += 1;
                _SUBMIT_SM_PDU[pos] = sourceAddressNpi; // source_addr_npi
                pos += 1;
                _source_addr = Utility.ConvertStringToByteArray(Utility.GetString(sourceAddress, 20, "")); //source_addr
                Array.Copy(_source_addr, 0, _SUBMIT_SM_PDU, pos, _source_addr.Length);
                pos += _source_addr.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = destinationAddressTon; // dest_addr_ton
                pos += 1;
                _SUBMIT_SM_PDU[pos] = destinationAddressNpi; // dest_addr_npi
                pos += 1;
                _destination_addr = Utility.ConvertStringToByteArray(Utility.GetString(destinationAddress, 20, "")); // destination_addr
                Array.Copy(_destination_addr, 0, _SUBMIT_SM_PDU, pos, _destination_addr.Length);
                pos += _destination_addr.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = esmClass; // esm_class
                pos += 1;
                _SUBMIT_SM_PDU[pos] = protocolId; // protocol_id
                pos += 1;
                _SUBMIT_SM_PDU[pos] = priorityFlag; // priority_flag
                pos += 1;
                _shedule_delivery_time = Utility.ConvertStringToByteArray(Utility.GetDateString(sheduleDeliveryTime)); // schedule_delivery_time
                Array.Copy(_shedule_delivery_time, 0, _SUBMIT_SM_PDU, pos, _shedule_delivery_time.Length);
                pos += _shedule_delivery_time.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _validity_period = Utility.ConvertStringToByteArray(Utility.GetDateString(validityPeriod)); // validity_period
                Array.Copy(_validity_period, 0, _SUBMIT_SM_PDU, pos, _validity_period.Length);
                pos += _validity_period.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = registeredDelivery; // registered_delivery
                pos += 1;
                _SUBMIT_SM_PDU[pos] = replaceIfPresentFlag; // replace_if_present_flag
                pos += 1;
                _SUBMIT_SM_PDU[pos] = dataCoding; // data_coding
                pos += 1;
                _SUBMIT_SM_PDU[pos] = smDefaultMsgId; // sm_default_msg_id
                pos += 1;

                _sm_length = message.Length > 254 ? (byte)254 : (byte)message.Length; // sm_length

                _SUBMIT_SM_PDU[pos] = _sm_length;
                pos += 1;
                Array.Copy(message, 0, _SUBMIT_SM_PDU, pos, _sm_length); // short_message
                pos += _sm_length;

                #region [ TLV parameters ]
                List<KeyValuePair<int, string>> tlvList = new List<KeyValuePair<int, string>>();
                if (!String.IsNullOrEmpty(peId))
                    tlvList.Add(new KeyValuePair<int, string>(0x1400, peId)); // PE_ID
                if (!String.IsNullOrEmpty(templateId))
                    tlvList.Add(new KeyValuePair<int, string>(0x1401, templateId)); // Template ID
                if (!string.IsNullOrEmpty(tmId))
                    tlvList.Add(new KeyValuePair<int, string>(0x1402, tmId)); // TM_ID

                foreach (KeyValuePair<int, string> item in tlvList)
                {
                    byte[] tag, length, value;

                    Utility.ConvertShortToArray((short)item.Key, out tag);
                    //logMessage(LogLevels.LogPdu, "Submit SM | TAG :: " + item.Key.ToString() + " :: " + BitConverter.ToString(tag));
                    Array.Copy(tag, 0, _SUBMIT_SM_PDU, pos, tag.Length);
                    pos += tag.Length;

                    int l = item.Value.Length + 1;
                    Utility.ConvertShortToArray((short)l, out length);
                    //logMessage(LogLevels.LogPdu, "Submit SM | LENGTH :: " + l.ToString() + " :: " + BitConverter.ToString(length));
                    Array.Copy(length, 0, _SUBMIT_SM_PDU, pos, length.Length);
                    pos += length.Length;

                    value = Utility.ConvertStringToByteArray(Utility.GetString(item.Value, ""));
                    //logMessage(LogLevels.LogPdu, "Submit SM | VALUE :: " + item.Value.ToString() + " :: " + BitConverter.ToString(value));
                    Array.Copy(value, 0, _SUBMIT_SM_PDU, pos, value.Length);
                    pos += value.Length;

                    pos += 1;
                }
                //logMessage(LogLevels.LogPdu, "SubmitSM |++ " + BitConverter.ToString(_SUBMIT_SM_PDU));
                #endregion


                Utility.CopyIntToArray(pos, _SUBMIT_SM_PDU, 0);


                Send(_SUBMIT_SM_PDU, pos);

                var submitSmEventArgs = new SubmitSmEventArgs(
                        new SmppEventArgs(_SUBMIT_SM_PDU, pos),
                        "",
                        sourceAddressTon,
                        sourceAddressNpi,
                        sourceAddress,
                        destinationAddressTon,
                        destinationAddressNpi,
                        destinationAddress,
                        esmClass,
                        protocolId,
                        priorityFlag,
                        sheduleDeliveryTime,
                        validityPeriod,
                        registeredDelivery,
                        replaceIfPresentFlag,
                        dataCoding,
                        smDefaultMsgId,
                        _sm_length,
                        message,
                        tlvList.Select(x => new OptionalParameter((ushort)x.Key, Utility.ConvertStringToByteArray(x.Value))).ToList(),
                        refid,
                        retryIndex
                    );
                submitSmEventArgs.AdditionalParameters = additionalParameters;


                lock (submittedMessages.SyncRoot)
                {
                    submittedMessages.Add(_sequence_number, submitSmEventArgs);
                }

                Task.Run(() => this.OnSubmitSm?.Invoke(this, submitSmEventArgs));
                return 0;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "SubmitSM | " + ex.ToString());
            }
            return -1;

        }//SubmitSM

        public int SubmitSMExtended(byte sourceAddressTon, byte sourceAddressNpi, string sourceAddress,
                            byte destinationAddressTon, byte destinationAddressNpi, string destinationAddress,
                            byte esmClass, byte protocolId, byte priorityFlag,
                            DateTime sheduleDeliveryTime, DateTime validityPeriod, byte registeredDelivery,
                            byte replaceIfPresentFlag, byte dataCoding, byte smDefaultMsgId,
                            byte[] message, byte messageIdentification, byte messageIndex, byte messageTotalIndex,
                            string peId, string templateId, string tmId, string? refid = null, 
                            int retryIndex = 0, IDictionary<string, object>? additionalParameters = null
                    )
        {
            int result = -1;
            try
            {
                if (!CanSend)
                    return -1;

                byte[] _destination_addr;
                byte[] _source_addr;
                byte[] _SUBMIT_SM_PDU;
                byte[] _shedule_delivery_time;
                byte[] _validity_period;
                uint _sequence_number;
                int pos;
                byte _sm_length;


                _SUBMIT_SM_PDU = new byte[KernelParameters.MaxPduSize];

                ////////////////////////////////////////////////////////////////////////////////////////////////
                /// Start filling PDU						

                Utility.CopyIntToArray(0x00000004, _SUBMIT_SM_PDU, 4);
                _sequence_number = MC.SequenceNumber;
                Utility.CopyIntToArray(_sequence_number, _SUBMIT_SM_PDU, 12);
                pos = 16;
                _SUBMIT_SM_PDU[pos] = 0x00; //service_type
                pos += 1;
                _SUBMIT_SM_PDU[pos] = sourceAddressTon;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = sourceAddressNpi;
                pos += 1;
                _source_addr = Utility.ConvertStringToByteArray(Utility.GetString(sourceAddress, 20, ""));
                Array.Copy(_source_addr, 0, _SUBMIT_SM_PDU, pos, _source_addr.Length);
                pos += _source_addr.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = destinationAddressTon;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = destinationAddressNpi;
                pos += 1;
                _destination_addr = Utility.ConvertStringToByteArray(Utility.GetString(destinationAddress, 20, ""));
                Array.Copy(_destination_addr, 0, _SUBMIT_SM_PDU, pos, _destination_addr.Length);
                pos += _destination_addr.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = esmClass;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = protocolId;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = priorityFlag;
                pos += 1;
                _shedule_delivery_time = Utility.ConvertStringToByteArray(Utility.GetDateString(sheduleDeliveryTime));
                Array.Copy(_shedule_delivery_time, 0, _SUBMIT_SM_PDU, pos, _shedule_delivery_time.Length);
                pos += _shedule_delivery_time.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _validity_period = Utility.ConvertStringToByteArray(Utility.GetDateString(validityPeriod));
                Array.Copy(_validity_period, 0, _SUBMIT_SM_PDU, pos, _validity_period.Length);
                pos += _validity_period.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = registeredDelivery;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = replaceIfPresentFlag;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = dataCoding;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = smDefaultMsgId;
                pos += 1;

                _sm_length = message.Length > 254 ? (byte)254 : (byte)message.Length;

                _SUBMIT_SM_PDU[pos] = (byte)(_sm_length + 6);
                pos += 1;

                // Begin UDH 
                // Length of UDH (5 bytes)
                _SUBMIT_SM_PDU[pos] = 0x05;
                pos += 1;
                // Indicator for concatenated message
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                // Subheader Length (3 bytes)
                _SUBMIT_SM_PDU[pos] = 0x03;
                pos += 1;
                // message identification - can be any hexadecimal
                // number but needs to match the UDH Reference Number of all  concatenated SMS
                _SUBMIT_SM_PDU[pos] = messageIdentification;
                pos += 1;
                // Number of pieces of the concatenated message
                _SUBMIT_SM_PDU[pos] = messageTotalIndex;
                pos += 1;
                // Sequence number (used by the mobile to concatenate the split messages)
                _SUBMIT_SM_PDU[pos] = messageIndex;
                pos += 1;
                // End UDH 


                Array.Copy(message, 0, _SUBMIT_SM_PDU, pos, _sm_length);
                pos += _sm_length;

                #region [ TLV parameters ]
                List<KeyValuePair<int, string>> tlvList = new List<KeyValuePair<int, string>>();
                tlvList.Add(new KeyValuePair<int, string>(0x1400, peId)); // PE_ID
                tlvList.Add(new KeyValuePair<int, string>(0x1401, templateId)); // Template ID
                tlvList.Add(new KeyValuePair<int, string>(0x1402, tmId)); // TM_ID

                foreach (KeyValuePair<int, string> item in tlvList)
                {
                    byte[] tag, length, value;

                    Utility.ConvertShortToArray((short)item.Key, out tag);
                    //logMessage(LogLevels.LogPdu, "Submit SM | TAG :: " + item.Key.ToString() + " :: " + BitConverter.ToString(tag));
                    Array.Copy(tag, 0, _SUBMIT_SM_PDU, pos, tag.Length);
                    pos += tag.Length;

                    int l = item.Value.Length + 1;
                    Utility.ConvertShortToArray((short)l, out length);
                    //logMessage(LogLevels.LogPdu, "Submit SM | LENGTH :: " + l.ToString() + " :: " + BitConverter.ToString(length));
                    Array.Copy(length, 0, _SUBMIT_SM_PDU, pos, length.Length);
                    pos += length.Length;

                    value = Utility.ConvertStringToByteArray(Utility.GetString(item.Value, ""));
                    //logMessage(LogLevels.LogPdu, "Submit SM | VALUE :: " + item.Value.ToString() + " :: " + BitConverter.ToString(value));
                    Array.Copy(value, 0, _SUBMIT_SM_PDU, pos, value.Length);
                    pos += value.Length;

                    pos += 1;
                }
                //logMessage(LogLevels.LogPdu, "SubmitSM |++ " + BitConverter.ToString(_SUBMIT_SM_PDU));
                #endregion

                Utility.CopyIntToArray(pos, _SUBMIT_SM_PDU, 0);

                Send(_SUBMIT_SM_PDU, pos);
                //undeliveredMessages++;
                var submitSmEventArgs = new SubmitSmEventArgs(
                        new SmppEventArgs(_SUBMIT_SM_PDU, pos),
                        "",
                        sourceAddressTon,
                        sourceAddressNpi,
                        sourceAddress,
                        destinationAddressTon,
                        destinationAddressNpi,
                        destinationAddress,
                        esmClass,
                        protocolId,
                        priorityFlag,
                        sheduleDeliveryTime,
                        validityPeriod,
                        registeredDelivery,
                        replaceIfPresentFlag,
                        dataCoding,
                        smDefaultMsgId,
                        _sm_length,
                        message,
                        tlvList.Select(x => new OptionalParameter((ushort)x.Key, Utility.ConvertStringToByteArray(x.Value))).ToList(),
                        refid,
                        retryIndex
                    );
                submitSmEventArgs.AdditionalParameters = additionalParameters;

                lock (submittedMessages.SyncRoot)
                {
                    submittedMessages.Add(_sequence_number, submitSmEventArgs);
                }

                Task.Run(() => this.OnSubmitSm?.Invoke(this, submitSmEventArgs));
                return 0;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "SubmitSM | " + ex.ToString());
            }
            return -1;

        }//SubmitSMExtended
        #endregion
        #endregion Public Functions

        #region Properties
        public SortedList ProcessingMessages;
        public int ConnectionState
        {
            get
            {
                return connectionState;
            }
            internal set
            {
                connectionState = value;
            }
        }
        public bool CanSend
        {
            get
            {
                try
                {
                    //if ((connectionState == ConnectionStates.SMPP_BINDED) && (undeliveredMessages <= KernelParameters.MaxUndeliverableMessages))
                    if (connectionState == ConnectionStates.SMPP_BINDED || connectionState == ConnectionStates.SMPP_UNBIND_PENDING)
                        return true;
                }
                catch (Exception ex)
                {
                    logMessage(LogLevels.LogExceptions, "CanSend | " + ex.ToString());
                }
                return false;
            }
        }//CanSend

        public bool CanTransmitSubmitSm
        {
            get
            {
                try
                {
                    //if ((connectionState == ConnectionStates.SMPP_BINDED) && (undeliveredMessages <= KernelParameters.MaxUndeliverableMessages))
                    if (connectionState == ConnectionStates.SMPP_BINDED)
                        return true;
                }
                catch (Exception ex)
                {
                    logMessage(LogLevels.LogExceptions, "CanReceiveSubmitSm | " + ex.ToString());
                }
                return false;
            }
        }

        public int LogLevel
        {
            get
            {
                return logLevel;
            }
            set
            {
                logLevel = value;
            }
        }//CanSend


        public byte AskDeliveryReceipt
        {
            get
            {
                return askDeliveryReceipt;
            }
            set
            {
                if ((value < 3) && (value >= 0))
                    askDeliveryReceipt = value;
            }
        }//AskDeliveryReceipt

        public bool SplitLongText
        {
            get
            {
                return splitLongText;
            }
            set
            {
                splitLongText = value;
            }
        }//SplitLongText
        public int NationalNumberLength
        {
            get
            {
                return nationalNumberLength;
            }
            set
            {
                if (value <= 12)
                    nationalNumberLength = value;
            }
        }//NationalNumberLength
        public bool UseEnquireLink
        {
            get
            {
                return useEnquireLink;
            }
            set
            {
                useEnquireLink = value;
            }
        }//UseEnquireLink
        public int EnquireLinkTimeout
        {
            get
            {
                return enquireLinkTimeout;
            }
            set
            {
                if (value > 1000)
                    enquireLinkTimeout = value;
            }
        }//EnquireLinkTimeout
        public int ReconnectTimeout
        {
            get
            {
                return reconnectTimeout;
            }
            set
            {
                if (value > 1000)
                    reconnectTimeout = value;
            }
        }//ReconnectTimeout
        //private int SendingDelivery
        //{
        //    get
        //    {
        //        lock (sendingDeliveryLock)
        //        {
        //            return sendingDelivery;
        //        }
        //    }
        //    set
        //    {
        //        lock (sendingDeliveryLock)
        //        {
        //            this.sendingDelivery = value;
        //        }
        //    }
        //}
        public Guid SessionId
        {
            get
            {
                return ReferenceEquals(this.Identifier, null) ? new Guid() : ((SmppSession)this.Identifier).Id;
            }
        }
        #endregion Properties

        #region Events
        public event SesionCreatedHandler OnClientSessionStart;
        public event SessionTerminatedHandler OnSessionEnd;
        public event PduRecievedHandler OnRecieved;
        public event PduSentHandler OnSent;
        public event BindEventHandler OnClientBind;
        public event UnbindEventHandler OnUnbind;
        public event SubmitSmEventHandler OnSubmitSm;
        public event SubmitSmRespEventHandler OnSubmitSmResp;
        public event DeliverSmTimerEventHandler OnDeliverSmTimerTick;
        public event DeliverSmSendEventHandler OnDeliverSmSend;
        public event DeliverSmEventHandler OnDeliverSm;
        public event LogEventHandler OnLog;
        public event BindEventHandler OnBind;
        public event SendSmsEventHandler OnSendSms;
        //public event Func<Task> OnSessionStart;
        //public event Func<Task> OnSessionEnd;
        //public event Func<Task> OnRecieved;
        //public event Func<Task> OnSent;
        //public event Func<Task> OnBindTransceiver;
        //public event Func<Task> OnUnbind;
        //public event Func<Task> OnSubmitSm;
        //public event Func<Task> OnSubmitSmResp;
        //public event Func<Task> OnDeliverSmTimerTick;
        //public event Func<Task> OnDeliverSmSend;
        //public event Func<Task> OnDeliverSm;
        //public event Func<Task> OnLog;

        #endregion Events

        #region Private functions

        #region [ Certificate Validation ]
        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // If the certificate is a valid, signed certificate, return true.
            if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
            {
                return true;
            }

            // If there are errors in the certificate chain, look at each error to determine the cause.
            if ((sslPolicyErrors & System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            {
                if (chain != null && chain.ChainStatus != null)
                {
                    foreach (System.Security.Cryptography.X509Certificates.X509ChainStatus status in chain.ChainStatus)
                    {
                        if ((certificate.Subject == certificate.Issuer) &&
                           (status.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot))
                        {
                            // Self-signed certificates with an untrusted root are valid. 
                            continue;
                        }
                        else
                        {
                            if (status.Status != System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NoError)
                            {
                                // If there are any other errors in the certificate chain, the certificate is invalid,
                                // so the method returns false.
                                return false;
                            }
                        }
                    }
                }

                // When processing reaches this line, the only errors in the certificate chain are 
                // untrusted root errors for self-signed certificates. These certificates are valid
                // for default Exchange server installations, so return true.
                return true;
            }
            else
            {
                // In all other cases, return false.
                return false;
            }
        }

        public static X509Certificate ValidateClientCertificate(
        object sender,
        string targetHost,
        X509CertificateCollection localCertificates,
        X509Certificate remoteCertificate,
        string[] acceptableIssuers)
        {
            //Console.WriteLine(" > Client is selecting a local certificate.");
            if (acceptableIssuers != null &&
                acceptableIssuers.Length > 0 &&
                localCertificates != null &&
                localCertificates.Count > 0)
            {
                // Use the first certificate that is from an acceptable issuer.
                foreach (X509Certificate certificate in localCertificates)
                {
                    string issuer = certificate.Issuer;
                    if (Array.IndexOf(acceptableIssuers, issuer) != -1)
                        return certificate;
                }
            }
            if (localCertificates != null &&
                localCertificates.Count > 0)
                return localCertificates[0];

            return null;
        }

        private static X509Certificate getServerCertificate()
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            X509CertificateCollection cert = store.Certificates.Find(X509FindType.FindBySubjectName, "IFB SMPP", true);
            return cert[0];
        }
        #endregion


        #region [ Send ]
        private void Send(SmppEventArgs args)
        {
            try
            {
                if (ReferenceEquals(args, null) || ReferenceEquals(sslStream, null))
                    return;

                logMessage(LogLevels.LogPdu, "Sending PDU : " + Utility.ConvertArrayToHexString(args.PDU, (int)args.CommandLength));
                if (!ReferenceEquals(args.PDU, null) && args.CommandLength > 0)
                {
                    //sslStream.Write(data, 0, n);
                    if (!sslStream.CanWrite)
                        new NotSupportedException("Unable to write to stream!");

                    sslStream.WriteAsync(args.PDU, 0, (int)args.CommandLength);
                    lastPacketSentTime = DateTime.Now;

                    if (!ReferenceEquals(OnSent, null))
                    {
                        //SmppEventArgs args = new SmppEventArgs(
                        //    (uint)data[0] << 24 | (uint)data[1] << 16 | (uint)data[2] << 8 | (uint)data[3],
                        //    (uint)data[4] << 24 | (uint)data[5] << 16 | (uint)data[6] << 8 | (uint)data[7],
                        //    (uint)data[8] << 24 | (uint)data[9] << 16 | (uint)data[10] << 8 | (uint)data[11],
                        //    (uint)data[12] << 24 | (uint)data[13] << 16 | (uint)data[14] << 8 | (uint)data[15],
                        //    data.Take(n).ToArray()
                        //);

                        SmppEventArgs smppEventArgs = new SmppEventArgs(args);
                        args.Dispose();
                        //OnSent.BeginInvoke(this, smppEventArgs, OnSentCallbackComplete, smppEventArgs);

                        Task.Run(() => OnSent.Invoke(this, smppEventArgs))
                            .ContinueWith(task => OnSentCallbackComplete(task, smppEventArgs));
                    }
                    else
                    {
                        args.Dispose();
                    }
                }
            }
            catch (ObjectDisposedException ex)
            {

            }
            catch (IOException ex)
            {
                logMessage(LogLevels.LogExceptions, "Send | Sending failed | " + ex.StackTrace);
            }
            catch (NotSupportedException ex)
            {
                args.Dispose();
            }
            catch (Exception ex)
            {
                args.Dispose();
                logMessage(LogLevels.LogExceptions, "Send | " + ex.ToString());

                if (
                   connectionState == ConnectionStates.SMPP_SOCKET_CONNECT_SENT
                    || connectionState == ConnectionStates.SMPP_SOCKET_CONNECTED
                    || connectionState == ConnectionStates.SMPP_BIND_SENT
                    || connectionState == ConnectionStates.SMPP_BINDED
                )
                {
                    this.Disconnect();
                    this.Dispose();
                }
            }
            finally
            {
                try
                {
                    args.Dispose();
                }
                catch { }
            }
        }
        private void Send(byte[] data, int n)
        {
            try
            {
                //lastPacketSentTime = DateTime.Now;
                //logMessage(LogLevels.LogPdu, "Sending PDU : " + Utility.ConvertArrayToHexString(data, n));
                if (!ReferenceEquals(data, null) && data.Length > 0)
                {
                    SmppEventArgs args = new SmppEventArgs(
                        (uint)data[0] << 24 | (uint)data[1] << 16 | (uint)data[2] << 8 | (uint)data[3],
                        (uint)data[4] << 24 | (uint)data[5] << 16 | (uint)data[6] << 8 | (uint)data[7],
                        (uint)data[8] << 24 | (uint)data[9] << 16 | (uint)data[10] << 8 | (uint)data[11],
                        (uint)data[12] << 24 | (uint)data[13] << 16 | (uint)data[14] << 8 | (uint)data[15],
                        data.Take(n).ToArray()
                    );

                    Send(args);
                    //sslStream.Write(data, 0, n);
                    //sslStream.WriteAsync(data, 0, n);

                    //if (!ReferenceEquals(OnSent, null))
                    //{


                    //    OnSent.BeginInvoke(this, args, OnSentCallbackComplete, args);
                    //}
                }
            }
            //catch (NotSupportedException ex)
            //{

            //}
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "Send | " + ex.ToString());
            }
        }//Send


        private void OnSentCallbackComplete(Task task, SmppEventArgs args)
        {
            try
            {
                logMessage(LogLevels.LogPdu, "Sent PDU : " + Utility.ConvertArrayToHexString(args.PDU, (int)args.CommandLength));
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "onSentCallbackComplete | " + ex.ToString());
            }

            args.Dispose();
        }
        //private void OnSentCallbackComplete(IAsyncResult iar)
        //{
        //    var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //    var invokedMethod = (PduSentHandler)ar.AsyncDelegate;
        //    var args = (SmppEventArgs)iar.AsyncState;

        //    try
        //    {
        //        invokedMethod.EndInvoke(iar);
        //        logMessage(LogLevels.LogPdu, "Sent PDU : " + Utility.ConvertArrayToHexString(args.PDU, (int)args.CommandLength));
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "onSentCallbackComplete | " + ex.ToString());
        //    }

        //    args.Dispose();
        //}

        #endregion

        private void tryToDisconnect()
        {
            try
            {
                if (clientSocket != null)
                {
                    if (clientSocket.Connected)
                    {
                        //clientSocket.Shutdown(SocketShutdown.Both);
                        //sslStream.Close();
                        clientSocket.Client.Shutdown(SocketShutdown.Both);
                    }
                    clientSocket = null;
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "tryToDisconnect | " + ex.ToString());
            }
        }//tryToDisconnect

        #region [ Disconnect ]
        private void disconnectSocket()
        {
            try
            {
                logMessage(LogLevels.LogInfo, "Disconnected");
                connectionState = ConnectionStates.SMPP_SOCKET_DISCONNECTED;
                //clientSocket.Shutdown(SocketShutdown.Both);
                //sslStream.Close();
                clientSocket.Client.Shutdown(SocketShutdown.Both);
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "disconnectSocket | " + ex.ToString());
            }
        }//disconnectSocket


        private void disconnect(TcpClient client)
        {
            try
            {
                logMessage(LogLevels.LogInfo, "Disconnected");
                connectionState = ConnectionStates.SMPP_SOCKET_DISCONNECTED;
                //client.Shutdown(SocketShutdown.Both);
                //sslStream.Close();
                clientSocket.Client.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "disconnect | " + ex.ToString());
            }
        }//Disconnect
        #endregion

        #region [ Log ]
        private void processLog(LogEventArgs e)
        {
            try
            {
                if (OnLog != null)
                {
                    //OnLog(e);
                    //this.OnLog.BeginInvoke(e, onLogCallback, e);

                    Task.Run(() => this.OnLog(this, e));
                }
            }
            catch
            {
            }

        }//processLog

        //private void onLogCallback(IAsyncResult iar)
        //{
        //    var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //    var invokedMethod = (LogEventHandler)ar.AsyncDelegate;
        //    var args = (LogEventArgs)iar.AsyncState;

        //    try
        //    {
        //        invokedMethod.EndInvoke(iar);
        //    }
        //    catch (Exception ex)
        //    {

        //    }
        //}

        public void logMessage(LogType logLevel, string pMessage)
        {
            logMessage((int)logLevel, pMessage);
        }

        public void logMessage(int logLevel, string pMessage)
        {
            try
            {
                if ((logLevel) > 0)
                {
                    //MyLog.WriteLogFile("Log", "Log level-" + logLevel, pMessage);
                    //if (ReferenceEquals(this.Identifier, null) || this.Identifier.GetType() != typeof(SmppSession))
                    //{
                    //    Logger.Write((LogType)logLevel, pMessage);
                    //}
                    //else
                    //{
                    //    Logger.Write((LogType)logLevel, "[" + ((SmppSession)this.Identifier).Id.ToString() + "] " + pMessage);
                    //}
                    LogEventArgs evArg = new LogEventArgs((LogType)logLevel, pMessage);
                    processLog(evArg);
                    //if (OnLog != null)
                    //{
                    //    //OnLog(e);
                    //    this.OnLog.BeginInvoke(evArg, onLogCallback, evArg);
                    //}
                }
            }
            catch (Exception ex)
            {
                // DO NOT USE LOG INSIDE LOG FUNCTION !!! logMessage(LogLevels.LogExceptions, "logMessage | " +ex.ToString());
            }
        }//logMessage
        #endregion

        #region [ Client Connection ]


        private void connectToSMSC()
        {
            try
            {
                if (ReferenceEquals(this.MC, null))
                {
                    logMessage(LogLevels.LogErrors, "Connect | ERROR #1011 : No SMSC defined. Please ddd SMSC definition first.");
                    return;
                }
                initParameters();
                //IPAddress ipAddress = IPAddress.Parse(smscArray.currentSMSC.Host);
                //IPEndPoint remoteEP = new IPEndPoint(ipAddress, smscArray.currentSMSC.Port);
                ////  Create a TCP/IP  socket.
                ////Try to disconnect if connected
                //tryToDisconnect();


                //clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                //logMessage(LogLevels.LogInfo, "Trying to connect to " + smscArray.currentSMSC.Description + "[" + smscArray.currentSMSC.Host + ":" + smscArray.currentSMSC.Port + "] Username " + smscArray.currentSMSC.SystemId + "[" + smscArray.currentSMSC.Password + "]");
                //clientSocket.BeginConnect(remoteEP, new AsyncCallback(connectCallback), clientSocket);
                //connectionState = ConnectionStates.SMPP_SOCKET_CONNECT_SENT;
                tryToDisconnect();
                this.Identifier = new SmppSession { 
                    Id = Guid.NewGuid(), 
                    Address = this.MC.Host, 
                    ValidForm = DateTime.Now, 
                    ValidTo = DateTime.MaxValue
                };
                logMessage(LogLevels.LogInfo, "Trying to connect to " + this.MC.Operator + "[" + this.MC.Host + ":" + this.MC.Port + "]");
                logMessage(LogLevels.LogInfo, "Username " + this.MC.SystemId + "[" + this.MC.Password + "]");
                logMessage(LogLevels.LogInfo, "Default encoding : " + this.MC.DefaultEncoding.ToString() + " [" + ((int)this.MC.DefaultEncoding).ToString() + "]");
                //logMessage(LogLevels.LogInfo, "Message Id Type : " + this.MC.MessageIdType.ToString() + " [" + ((int)this.MC.MessageIdType).ToString() + "]");
                //clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket = new TcpClient();
                clientSocket.BeginConnect(this.MC.Host, this.MC.Port, new AsyncCallback(connectCallback), clientSocket);
                //IPAddress ipAddress;
                //if (IPAddress.TryParse(smscArray.currentSMSC.Host, out ipAddress))
                //{
                //    IPEndPoint remoteEP = new IPEndPoint(ipAddress, smscArray.currentSMSC.Port);
                //    clientSocket.BeginConnect(remoteEP, new AsyncCallback(connectCallback), clientSocket);
                //}
                //else
                //{
                //    clientSocket.BeginConnect(smscArray.currentSMSC.Host, smscArray.currentSMSC.Port, new AsyncCallback(connectCallback), clientSocket);
                //}
                //clientSocket.BeginConnect(smscArray.currentSMSC.Host, smscArray.currentSMSC.Port, new AsyncCallback(connectCallback), clientSocket);
                connectionState = ConnectionStates.SMPP_SOCKET_CONNECT_SENT;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "connectToSMSC | " + ex.ToString());
            }

        }//connectToSMSC
        private void connectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                TcpClient client = (TcpClient) ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);
                clientSocket = client;
                sslStream = clientSocket.GetStream();
                clientSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);
                connectionState = ConnectionStates.SMPP_SOCKET_CONNECTED;
                logMessage(LogLevels.LogInfo, "Connected");
                lastSeenConnected = DateTime.Now;
                bind();
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "connectCallback | " + ex.ToString());
                tryToReconnect();
            }
        }//connectCallback


        private void tryToReconnect()
        {
            try
            {
                disconnectSocket();
                Thread.Sleep(reconnectTimeout);
                if (mustBeConnected)
                {
                    //smscArray.NextSMSC();
                    connectToSMSC();
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "tryToReconnect | " + ex.ToString());
            }

        }//tryToReconnect
        #endregion

        #region [ Recieve ]

        private void receive()
        {
            try
            {
                // Create the state object.
                ConnectionStateObject state = new ConnectionStateObject();
                state.workSocket = clientSocket;

                // Begin receiving the data from the remote device.
                //clientSocket.BeginReceive(state.buffer, 0, SecuredStateObject.BufferSize, 0, new AsyncCallback(receiveCallback), state);
                sslStream.BeginRead(state.buffer, 0, ConnectionStateObject.BufferSize, new AsyncCallback(receiveCallback), state);

            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "receive | " + ex.ToString());
                this.Disconnect();
                this.Dispose();
            }

        }//receive
        private void receiveCallback(IAsyncResult ar)
        {
            try
            {
                int _command_length;
                uint _command_id;
                uint _command_status;
                uint _sequence_number;
                int _body_length;
                byte[] _PDU_body = new byte[0];
                byte[] _RESPONSE_PDU = new byte[KernelParameters.MaxPduSize];
                int i, x;
                bool _exit_flag;
                string unbindStr = "";
                // Retrieve the state object and the client socket 
                // from the async state object.
                ConnectionStateObject state = (ConnectionStateObject)ar.AsyncState;
                TcpClient client = state.workSocket;
                // Read data from the remote device.
                int bytesRead = sslStream.EndRead(ar);
                //logMessage(LogLevels.LogSteps, "Received " + Utility.ConvertIntToHexString(bytesRead) + " bytes");
                if (bytesRead > 0)
                {
                    //test line
                    //logMessage(LogLevels.LogPdu, "Received Binary Data " + Utility.ConvertArrayToHexString(buffer, bytesRead));
                    // logMessage(LogLevels.LogPdu, "Received Binary Data to string " + Utility.ConvertArrayToString(buffer, bytesRead));
                    // There might be more data, so store the data received so far.

                    //if ((LogLevel & LogLevels.LogPdu) > 0)
                    //    logMessage(LogLevels.LogPdu, "Received Binary Data " + Utility.ConvertArrayToHexString(buffer, bytesRead));
                    //////////////////////////////
                    /// Begin processing SMPP messages
                    /// 
                    mLen = mPos + bytesRead;
                    if (mLen > KernelParameters.MaxBufferSize)
                    {
                        mPos = 0;
                        mLen = 0;
                        mbResponse = new byte[KernelParameters.MaxBufferSize];
                    }
                    else
                    {
                        Array.Copy(state.buffer, 0, mbResponse, mPos, bytesRead);
                        mPos = mLen;
                        _exit_flag = false;
                        x = 0;
                        while (((mLen - x) >= 16) && (_exit_flag == false))
                        {
                            _command_length = mbResponse[x + 0];
                            for (i = x + 1; i < x + 4; i++)
                            {
                                _command_length <<= 8;
                                _command_length = _command_length | mbResponse[i];
                            }

                            _command_id = mbResponse[x + 4];
                            for (i = x + 5; i < x + 8; i++)
                            {
                                _command_id <<= 8;
                                _command_id = _command_id | mbResponse[i];
                            }

                            _command_status = mbResponse[x + 8];
                            for (i = x + 9; i < x + 12; i++)
                            {
                                _command_status <<= 8;
                                _command_status = _command_status | mbResponse[i];
                            }

                            _sequence_number = mbResponse[x + 12];
                            for (i = x + 13; i < x + 16; i++)
                            {
                                _sequence_number <<= 8;
                                _sequence_number = _sequence_number | mbResponse[i];
                            }
                            if ((_command_length <= (mLen - x)) && (_command_length >= 16))
                            {
                                if (_command_length == 16)
                                    _body_length = 0;
                                else
                                {
                                    _body_length = _command_length - 16;
                                    _PDU_body = new byte[_body_length];
                                    Array.Copy(mbResponse, x + 16, _PDU_body, 0, _body_length);
                                }
                                //////////////////////////////////////////////////////////////////////////////////////////
                                ///SMPP Command parsing
                                ///

                                byte[] _pdu = new byte[_command_length];
                                Array.Copy(mbResponse, x, _pdu, 0, _command_length);

                                SmppEventArgs args = new SmppEventArgs(
                                    (uint)_command_length,
                                    _command_id,
                                    _command_status,
                                    _sequence_number,
                                    _pdu
                                );

                                SmppEventArgs callbackArgs = new SmppEventArgs(args);

                                if (!ReferenceEquals(OnRecieved, null))
                                {
                                    //OnRecieved.BeginInvoke(this, callbackArgs, OnRecievedCallbackComplete, callbackArgs);
                                    Task.Run(() => this.OnRecieved(this, callbackArgs))
                                        .ContinueWith(task => OnRecievedCallbackComplete(task, callbackArgs));
                                }


                                switch (_command_id)
                                {
                                    case Command.BIND_TRANSMITTER:
                                        logMessage(LogLevels.LogSteps, "Bind_Transmitter");
                                        //BindTransceiverEventArgs btrxArg = new BindTransceiverEventArgs(_sequence_number, _command_status, _PDU_body.Take(_body_length).ToArray());
                                        //decodeAndprocessBind(_sequence_number, _command_status, _PDU_body.Take(_body_length).ToArray());
                                        decodeAndProcessBind(args);
                                        break;
                                    case Command.BIND_RECIEVER:
                                        logMessage(LogLevels.LogSteps, "Bind_Receiver");
                                        //if (!ReferenceEquals(deliveryTimer, null))
                                        //    deliveryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                                        //if (!ReferenceEquals(deliveryReportTimer, null))
                                        //    deliveryReportTimer.Change(Timeout.Infinite, Timeout.Infinite);
                                        //BindTransceiverEventArgs btrxArg = new BindTransceiverEventArgs(_sequence_number, _command_status, _PDU_body.Take(_body_length).ToArray());
                                        //decodeAndprocessBind(_sequence_number, _command_status, _PDU_body.Take(_body_length).ToArray());
                                        decodeAndProcessBind(args);
                                        break;
                                    case Command.BIND_TRANSCEIVER:
                                        logMessage(LogLevels.LogSteps, "Bind_Transiver");
                                        //BindTransceiverEventArgs btrxArg = new BindTransceiverEventArgs(_sequence_number, _command_status, _PDU_body.Take(_body_length).ToArray());
                                        //decodeAndprocessBind(_sequence_number, _command_status, _PDU_body.Take(_body_length).ToArray());
                                        decodeAndProcessBind(args);
                                        break;

                                    case Command.BIND_TRANSCEIVER_RESP: // 0x80000009:
                                        logMessage(LogLevels.LogSteps, "Bind_Transiver_Resp");

                                        if (connectionState == ConnectionStates.SMPP_BIND_SENT)
                                        {
                                            if (_command_status == 0)
                                            {
                                                connectionState = ConnectionStates.SMPP_BINDED;
                                                logMessage(LogLevels.LogInfo, $"Binded with {this.MC.Operator} {this.MC.Instance}");
                                                if (!ReferenceEquals(this.OnBind, null))
                                                {
                                                    Task.Run(() => this.OnBind.Invoke(this, null));
                                                }
                                            }
                                            else
                                            {
                                                //logMessage(LogLevels.LogSteps | LogLevels.LogErrors, "SMPP BIND ERROR : " + Utility.ConvertUIntToHexString(_command_status));
                                                logMessage(LogLevels.LogInfo, $"Binded with {this.MC.Operator} {this.MC.Instance} failed with error {Utility.ConvertUIntToHexString(_command_status)}");
                                                //disconnect(client);
                                                //tryToReconnect();
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "ERROR #3011 : Unexpected Bind_Transiver_Resp");
                                        }
                                        break;
                                    case Command.UNBIND:
                                        logMessage(LogLevels.LogSteps, "Unbind");
                                        decodeAndProcessUnbind(args);
                                        break;
                                    case Command.UNBIND_RESP: // 0x80000006:
                                        logMessage(LogLevels.LogSteps, "Unbind_Resp");
                                        //connectionState = ConnectionStates.SMPP_UNBINDED;
                                        //disconnect(client);
                                        unbindStr = "Unbind_Resp";
                                        decodeAndProcessUnbindResp(args);
                                        break;
                                    case Command.SUBMIT_SM:
                                        logMessage(LogLevels.LogSteps, "Submit_Sm");
                                        //decodeAndProcessSubmitSm(_sequence_number, _command_status, _PDU_body.Take(_body_length).ToArray());
                                        if (ConnectionType == ConnectionType.Receiver || !this.CanTransmitSubmitSm)
                                        {
                                            sendSubmitSmResp(MC.SequenceNumber, StatusCodes.ESME_RINVCMDID, "");
                                        }
                                        else
                                        {
                                            decodeAndProcessSubmitSm(args);
                                        }
                                        break;
                                    case Command.SUBMIT_SM_RESP: // 0x80000004:
                                        //logMessage(LogLevels.LogSteps, "Submit_Sm_Resp");                                          
                                        //SubmitSmRespEventArgs evArg = new SubmitSmRespEventArgs(_sequence_number, _command_status, Utility.ConvertArrayToString(_PDU_body, _body_length - 1));
                                        processSubmitSmResp(args);
                                        break;
                                    case Command.DATA_SM: //0x00000103:
                                        logMessage(LogLevels.LogSteps, "Data_Sm");
                                        decodeAndProcessDataSm(_sequence_number, _PDU_body, _body_length);
                                        break;
                                    case Command.DATA_SM_RESP: //0x80000103:
                                        logMessage(LogLevels.LogSteps, "Data_Sm_Resp");
                                        //evArg = new SubmitSmRespEventArgs(_sequence_number, _command_status, Utility.ConvertArrayToString(_PDU_body, _body_length - 1));
                                        processSubmitSmResp(args);
                                        break;
                                    case Command.ENQUIRE_LINK: //0x00000015:
                                        logMessage(LogLevels.LogSteps, "Enquire_Link");
                                        sendEnquireLinkResp(_sequence_number);
                                        args.Dispose();
                                        break;
                                    case Command.ENQUIRE_LINK_RESP: //0x80000015:
                                        logMessage(LogLevels.LogSteps, "Enquire_Link_Resp");
                                        enquireLinkResponseTime = DateTime.Now;
                                        args.Dispose();
                                        break;

                                    case Command.DELIVER_SM: //0x00000005:
                                        //logMessage(LogLevels.LogSteps, "Deliver_Sm");
                                        decodeAndProcessDeliverSm(_sequence_number, _PDU_body, _body_length);
                                        args.Dispose();
                                        //logMessage(LogLevels.LogPdu, "Received Binary Data to string " + Utility.ConvertArrayToString(_PDU_body, _body_length));
                                        break;
                                    case Command.DELIVER_SM_RESP:
                                        decodeAndProcessDeliverSmResp(args);
                                        break;
                                    default:
                                        sendGenericNack(_sequence_number, StatusCodes.ESME_RINVCMDID);
                                        args.Dispose();
                                        logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "Unknown SMPP PDU type" + Utility.ConvertUIntToHexString(_command_id));
                                        break;
                                }
                                ///////////////////////////////////////////////////////////////////////////////////////////
                                ///END SMPP Command parsing
                                ///////////////////////////////////////////////////////////////////////////////////////////

                                if (_command_length == (mLen - x))
                                {
                                    mLen = 0;
                                    mPos = 0;
                                    x = 0;
                                    _exit_flag = true;
                                }
                                else
                                {
                                    x += _command_length;
                                }
                            }
                            else
                            {
                                //Disable due to Adobe issue by Anirban Seth on 25-Mar-2022
                                //Dont send NACK on incomplete message
                                //sendGenericNack(_sequence_number, StatusCodes.ESME_RINVMSGLEN);
                                mLen -= x;
                                mPos = mLen;
                                Array.Copy(mbResponse, x, mbResponse, 0, mLen);
                                _exit_flag = true;
                                logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "Invalid PDU Length");
                            }
                            if (x < mLen)
                                logMessage(LogLevels.LogPdu, "NEXT PDU STEP IN POS " + Convert.ToString(x) + " FROM " + Convert.ToString(mLen));
                        }
                    }
                    //////////////////////////////
                    /// End processing SMPP messages
                    //  Get the rest of the data. 
                    if (unbindStr != "Unbind_Resp")
                    {
                        sslStream.BeginRead(state.buffer, 0, ConnectionStateObject.BufferSize, new AsyncCallback(receiveCallback), state);
                    }

                }
                else
                {
                    logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "Incoming network buffer from SMSC is empty.");
                    /*					if (client.Poll(0,SelectMode.SelectError)&&client.Poll(0,SelectMode.SelectRead)&&client.Poll(0,SelectMode.SelectWrite))
                                        {
                                            logMessage(LogLevels.LogSteps|LogLevels.LogExceptions, "Socket Error");
                                            unBind();
                                        }
                    */

                    //if (mustBeConnected)
                    //    tryToReconnect();
                }
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "receiveCallback | " + ex.ToString());

                //deliveryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                //deliveryReportTimer.Change(Timeout.Infinite, Timeout.Infinite);

                //unBind();
                if (mustBeConnected)
                    tryToReconnect();
                else
                {
                    Disconnect();
                    this.Dispose();
                }
            }

        }//receiveCallback


        private void OnRecievedCallbackComplete(Task task, SmppEventArgs args)
        {
            args.Dispose();
        }
        //private void OnRecievedCallbackComplete(IAsyncResult iar)
        //{
        //    var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //    var invokedMethod = (PduRecievedHandler)ar.AsyncDelegate;
        //    var args = (SmppEventArgs)iar.AsyncState;

        //    try
        //    {
        //        invokedMethod.EndInvoke(iar);
        //        args.Dispose();
        //        //switch(args.CommandId)
        //        //{
        //        //    case Command.BIND_TRANSMITTER:
        //        //        break;
        //        //    case Command.BIND_RECIEVER:
        //        //        break;
        //        //    case Command.BIND_TRANSCEIVER:
        //        //        break;
        //        //    case Command.BIND_TRANSCEIVER_RESP: // 0x80000009:
        //        //        args.Dispose();
        //        //        break;
        //        //    case Command.UNBIND:
        //        //        break;
        //        //    case Command.UNBIND_RESP: // 0x80000006:
        //        //        break;
        //        //    case Command.SUBMIT_SM:
        //        //        break;
        //        //    case Command.SUBMIT_SM_RESP: // 0x80000004:
        //        //        break;
        //        //    case Command.DATA_SM: //0x00000103:
        //        //        args.Dispose();
        //        //        break;
        //        //    case Command.DATA_SM_RESP: //0x80000103:
        //        //        break;
        //        //    case Command.ENQUIRE_LINK: //0x00000015:
        //        //        args.Dispose();
        //        //        break;
        //        //    case Command.ENQUIRE_LINK_RESP: //0x80000015:
        //        //        args.Dispose();
        //        //        break;

        //        //    case Command.DELIVER_SM: //0x00000005:
        //        //        args.Dispose();
        //        //        break;
        //        //    case Command.DELIVER_SM_RESP:
        //        //        args.Dispose();
        //        //        break;
        //        //    default:
        //        //        args.Dispose();
        //        //        break;
        //        //}
        //    }
        //    catch (ObjectDisposedException ex)
        //    {
        //        return;
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "onRecievedCallbackComplete | " + ex.ToString());
        //    }
        //}
        #endregion

        #region [ Bind Server ]
        private void bind()
        {

            logMessage(LogLevels.LogInfo, $"Binding to {this.MC.Operator} {this.MC.Instance} [{this.MC.Host}:{this.MC.Port}] with {this.MC.SystemId}:{this.MC.Password}, encoding {this.MC.DefaultEncoding.ToString()} [{((int)this.MC.DefaultEncoding).ToString()}]");
            try
            {
                byte[] Bind_PDU = new byte[1024];
                int pos, i, n;

                //pos = 7;
                //Bind_PDU[pos] = 0x09;
                pos = 4;
                Utility.CopyIntToArray(Command.BIND_TRANSCEIVER, Bind_PDU, pos);

                //pos = 8;
                //Utility.CopyIntToArray(StatusCodes.ESME_ROK, Bind_PDU, pos);

                pos = 12;
                Utility.CopyIntToArray(MC.SequenceNumber, Bind_PDU, pos);
                pos = 15;

                pos++;
                n = MC.SystemId.Length;
                for (i = 0; i < n; i++, pos++)
                    Bind_PDU[pos] = (byte)MC.SystemId[i];
                Bind_PDU[pos] = 0;

                pos++;
                n = MC.Password.Length;
                for (i = 0; i < n; i++, pos++)
                    Bind_PDU[pos] = (byte)MC.Password[i];
                Bind_PDU[pos] = 0;

                pos++;
                n = MC.SystemType.Length;
                for (i = 0; i < n; i++, pos++)
                    Bind_PDU[pos] = (byte)MC.SystemType[i];
                Bind_PDU[pos] = 0;

                Bind_PDU[++pos] = 0x34; //interface version
                Bind_PDU[++pos] = (byte)MC.AddrTon; //addr_ton
                Bind_PDU[++pos] = (byte)MC.AddrNpi; //addr_npi

                //address_range
                pos++;
                n = MC.AddressRange.Length;
                for (i = 0; i < n; i++, pos++)
                    Bind_PDU[pos] = (byte)MC.AddressRange[i];
                Bind_PDU[pos] = 0x00;

                pos++;
                Bind_PDU[3] = Convert.ToByte(pos & 0x00FF);
                Bind_PDU[2] = Convert.ToByte((pos >> 8) & 0x00FF);

                // Begin sending the data to the remote device.
                logMessage(LogLevels.LogSteps, "BindSent");
                Send(Bind_PDU, pos);
                connectionState = ConnectionStates.SMPP_BIND_SENT;
                receive();
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "bind | " + ex.ToString());
            }

        }//bind
        #endregion

        #region [ Bind Transceiver Response ]      
        //private void decodeAndprocessBind(uint sequence_number, uint command_status, byte[] _body)
        private void decodeAndProcessBind(SmppEventArgs args)//, CancellationToken cancellationToken)
        {
            connectionState = ConnectionStates.SMPP_BIND_SENT;
            try
            {
                uint sequence_number = args.Sequence;
                uint command_status = args.CommandStatus;
                byte[] _body = args.PDU.Skip(16).ToArray();
                uint response_command_id = 0;

                if (args.CommandId == Command.BIND_RECIEVER)
                {
                    ConnectionType = ConnectionType.Receiver;
                    response_command_id = Command.BIND_RECIEVER_RESP;
                }
                else if (args.CommandId == Command.BIND_TRANSMITTER)
                {
                    ConnectionType = ConnectionType.Transmitter;
                    response_command_id = Command.BIND_TRANSMITTER_RESP;
                }
                else if (args.CommandId == Command.BIND_TRANSCEIVER)
                {
                    ConnectionType = ConnectionType.Transceiver;
                    response_command_id = Command.BIND_TRANSCEIVER_RESP;
                }

                int pos = 0;
                int _system_id_length = 0;
                int _password_length = 0;
                int _system_type_length = 0;
                int _address_range_length = 0;

                /////////////////////////////////////
                /// Bind Params
                /// 
                byte[] _system_id = new byte[16];
                byte[] _password = new byte[9];
                byte[] _system_type = new byte[13];
                byte _interface_version;
                byte _addr_ton;
                byte _addr_npi;
                byte[] _address_range = new byte[41];

                // System Id
                while (_system_id_length < _system_id.Length && _body[pos] != 0x00)
                {
                    _system_id[_system_id_length++] = _body[pos];
                    pos++;
                }

                if (_body[pos] != 0x00)
                {
                    sendBindResp(response_command_id, sequence_number, StatusCodes.ESME_RINVSYSID, String.Empty);
                    logMessage(LogLevels.LogExceptions, "processBind returned UNKNOWNERR on 0x01");
                    return;
                }
                pos++;

                // Password
                while (_password_length < _password.Length && _body[pos] != 0x00)
                {
                    _password[_password_length++] = _body[pos];
                    pos++;
                }

                if (_body[pos] != 0x00)
                {
                    sendBindResp(response_command_id, sequence_number, StatusCodes.ESME_RINVPASWD, String.Empty);
                    logMessage(LogLevels.LogExceptions, "processBind returned UNKNOWNERR on 0x01");
                    return;
                }
                pos++;

                // System Type
                while (_system_type_length < _system_type.Length && _body[pos] != 0x00)
                {
                    _system_type[_system_type_length++] = _body[pos];
                    pos++;
                }

                if (_body[pos] != 0x00)
                {
                    sendBindResp(response_command_id, sequence_number, StatusCodes.ESME_RINVSYSTYP, String.Empty);
                    logMessage(LogLevels.LogExceptions, "processBind returned UNKNOWNERR on 0x01");
                    return;
                }
                pos++;

                // Interface Version
                _interface_version = _body[pos++];

                // Addr Ton
                _addr_ton = _body[pos++];

                // Addr Ton
                _addr_npi = _body[pos++];

                // Address Range
                while (_address_range_length < 41 && _body[pos] != 0x00)
                {
                    _address_range[_address_range_length++] = _body[pos];
                    pos++;
                }

                //if (_body[pos] != 0x00)
                //{
                //    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                //    logMessage(LogLevels.LogExceptions, "processBind returned UNKNOWNERR on 0x01");
                //    return;
                //}
                pos++;

                BindEventArgs btrxArg = new BindEventArgs(
                    args,
                    Utility.ConvertArrayToString(_system_id, _system_id_length),
                    Utility.ConvertArrayToString(_password, _password_length),
                    Utility.ConvertArrayToString(_system_type, _system_type_length),
                    _interface_version,
                    _addr_ton,
                    _addr_npi,
                    Utility.ConvertArrayToString(_address_range, _address_range_length)
                );

                esme = new SMSC(
                    0,
                    btrxArg.SystemId,
                    btrxArg.Password,
                    btrxArg.SystemType,
                    MC.Secured,
                    btrxArg.AddrTon,
                    btrxArg.AddrNpi,
                    btrxArg.AddressRange
                );

                //int bindStatus = StatusCodes.ESME_ROK;
                if (OnClientBind != null)
                {
                    //bindStatus = OnBindTransceiver(this, btrxArg);
                    //OnBindTransceiver.BeginInvoke(this, btrxArg, onBindEventComplete, btrxArg);

                    //foreach (Func<Task> handler in OnBindTransceiver.GetInvocationList())
                    //{
                    //    await handler.Invoke();
                    //}
                    //Task taskA = Task.Run(() => OnBindTransceiver.Invoke(this, btrxArg));
                    //Task taskB = taskA.ContinueWith(task => onBindEventComplete(task, btrxArg)); //, cancellationToken


                    Task.Run<int>(() => OnClientBind.Invoke(this, btrxArg))
                        .ContinueWith(task => onBindEventComplete(task, btrxArg));
                }
                else
                {

                    connectionState = ConnectionStates.SMPP_BINDED;
                    sendBindResp(response_command_id, sequence_number, StatusCodes.ESME_ROK, MC.SystemId);

                    logMessage(LogLevels.LogInfo, "processBind / Bind " + ConnectionType.ToString() + " Request Success:" + "System Id -" + btrxArg.SystemId + " System Type-" + btrxArg.SystemType);
                }
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "processBind | " + ex.ToString());
                logMessage(LogLevels.LogExceptions, "processBind | PDU | " + Utility.ConvertArrayToHexString(args.PDU, args.PDU.Length));

                this.Dispose();
            }
            finally
            {
                args.Dispose();
            }
        } // processBind

        private async Task onBindEventComplete(Task<int> task, BindEventArgs btrxArg) //, CancellationToken cancellationToken)
        {
            try
            {
                if (task.IsFaulted)
                {
                    throw new InvalidOperationException("Bind event handler failed");
                }


                int bindStatus = StatusCodes.ESME_ROK;
                uint response_command_id = 0;

                if (btrxArg.CommandId == Command.BIND_RECIEVER)
                {
                    response_command_id = Command.BIND_RECIEVER_RESP;
                }
                else if (btrxArg.CommandId == Command.BIND_TRANSMITTER)
                {
                    response_command_id = Command.BIND_TRANSMITTER_RESP;
                }
                else if (btrxArg.CommandId == Command.BIND_TRANSCEIVER)
                {
                    response_command_id = Command.BIND_TRANSCEIVER_RESP;
                }

                //bindStatus = invokedMethod.EndInvoke(iar);
                bindStatus = task.Result;

                if (bindStatus == StatusCodes.ESME_ROK)
                {
                    connectionState = ConnectionStates.SMPP_BINDED;
                    sendBindResp(response_command_id, btrxArg.Sequence, StatusCodes.ESME_ROK, MC.SystemId);

                    logMessage(LogLevels.LogInfo, "processBind / Bind " + ConnectionType.ToString() + " Request Success:" + "System Id -" + btrxArg.SystemId + " System Type-" + btrxArg.SystemType);

                    enquireLinkTimer.Change(enquireLinkTimeout, enquireLinkTimeout);
                    if (
                        ConnectionType == ConnectionType.Transceiver
                        || ConnectionType == ConnectionType.Receiver
                    )
                    {
                        deliveryTimer.Change(deliveryLoadTimeout, deliveryLoadTimeout);
                        deliveryReportTimer.Change(deliverySendTimeout, deliverySendTimeout);
                        deliveryReportTimeoutTimer.Change(deliveryPurgeTimeout, deliveryPurgeTimeout);
                    }
                }
                else
                {
                    sendBindResp(response_command_id, btrxArg.Sequence, StatusCodes.ESME_RBINDFAIL, MC.SystemId);
                    //sslStream.Close();
                    //clientSocket.Close();

                    logMessage(LogLevels.LogInfo, "processBind / Bind " + ConnectionType.ToString() + " Request Failed:" + "System Id -" + btrxArg.SystemId + " System Type-" + btrxArg.SystemType);
                    logMessage(LogLevels.LogSteps, String.Format("Terminating Connection {0} :: Instance {1}", ((SmppSession)this.Identifier).Id, ConnectionNumber));
                    this.Dispose();
                }

                btrxArg.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                //sendSubmitSmResp(btrxArg.Sequence, StatusCodes.ESME_RSYSERR, String.Empty);

                logMessage(LogLevels.LogExceptions, "onBindEventComplete | " + ex.ToString());
                logMessage(LogLevels.LogSteps, String.Format("Terminating Connection {0} :: Instance {1}", ((SmppSession)this.Identifier).Id, ConnectionNumber));
                this.Dispose();
            }
        }
        //private void onBindEventComplete(IAsyncResult iar)
        //{
        //    try
        //    {
        //        var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //        var invokedMethod = (BindEventHandler)ar.AsyncDelegate;
        //        var args = (BindEventArgs)iar.AsyncState;
        //        int bindStatus = StatusCodes.ESME_ROK;
        //        uint response_command_id = 0;

        //        if (args.CommandId == Command.BIND_RECIEVER)
        //        {
        //            response_command_id = Command.BIND_RECIEVER_RESP;
        //        }
        //        else if (args.CommandId == Command.BIND_TRANSMITTER)
        //        {
        //            response_command_id = Command.BIND_TRANSMITTER_RESP;
        //        }
        //        else if (args.CommandId == Command.BIND_TRANSCEIVER)
        //        {
        //            response_command_id = Command.BIND_TRANSCEIVER_RESP;
        //        }

        //        bindStatus = invokedMethod.EndInvoke(iar);

        //        if (bindStatus == StatusCodes.ESME_ROK)
        //        {
        //            connectionState = ConnectionStates.SMPP_BINDED;
        //            sendBindResp(response_command_id, args.Sequence, StatusCodes.ESME_ROK, MC.SystemId);

        //            logMessage(LogLevels.LogInfo, "processBind / Bind " + ConnectionType.ToString() + " Request Success:" + "System Id -" + args.SystemId + " System Type-" + args.SystemType);

        //            enquireLinkTimer.Change(enquireLinkTimeout, enquireLinkTimeout);
        //            if (
        //                ConnectionType == ConnectionType.Transceiver
        //                || ConnectionType == ConnectionType.Receiver
        //            )
        //            {
        //                deliveryTimer.Change(deliveryLoadTimeout, deliveryLoadTimeout);
        //                deliveryReportTimer.Change(deliverySendTimeout, deliverySendTimeout);
        //                deliveryReportTimeoutTimer.Change(deliveryPurgeTimeout, deliveryPurgeTimeout);
        //            }
        //        }
        //        else
        //        {
        //            sendBindResp(response_command_id, args.Sequence, StatusCodes.ESME_RBINDFAIL, MC.SystemId);
        //            //sslStream.Close();
        //            //clientSocket.Close();

        //            logMessage(LogLevels.LogInfo, "processBind / Bind " + ConnectionType.ToString() + " Request Failed:" + "System Id -" + args.SystemId + " System Type-" + args.SystemType);
        //            logMessage(LogLevels.LogSteps, String.Format("Terminating Connection {0} :: Instance {1}", ((SmppSession)this.Identifier).Id, ConnectionNumber));
        //            this.Dispose();
        //        }

        //        args.Dispose();
        //    }
        //    catch (ObjectDisposedException ex)
        //    {
        //        return;
        //    }
        //    catch (Exception ex)
        //    {
        //        //sendSubmitSmResp(args.Sequence, StatusCodes.ESME_RSYSERR, String.Empty);

        //        logMessage(LogLevels.LogExceptions, "onBindEventComplete | " + ex.ToString());
        //        logMessage(LogLevels.LogSteps, String.Format("Terminating Connection {0} :: Instance {1}", ((SmppSession)this.Identifier).Id, ConnectionNumber));
        //        this.Dispose();
        //    }
        //}

        public void sendBindResp(uint command_id, uint sequence_number, uint command_status, string system_id)
        {
            byte[] _system_id = Utility.ConvertStringToByteArray(Utility.GetString(system_id, 16, ""));
            sendBindResp(command_id, sequence_number, command_status, _system_id);
        }

        public void sendBindResp(uint command_id, uint sequence_number, uint command_status, byte[] _system_id)
        {
            try
            {
                int pos = 0;
                byte[] _PDU = new byte[KernelParameters.MaxPduSize];



                //Utility.CopyIntToArray(17, _PDU, 0);

                Utility.CopyIntToArray(command_id, _PDU, 4);

                Utility.CopyIntToArray(command_status, _PDU, 8);

                Utility.CopyIntToArray(sequence_number, _PDU, 12);

                pos = 16;
                Array.Copy(_system_id, 0, _PDU, pos, _system_id.Length);

                pos += _system_id.Length;
                pos += 1;

                Utility.CopyIntToArray(pos, _PDU, 0);

                Send(_PDU, pos);

            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendDeliverSmResp | " + ex.ToString());
            }
        }//sendDeliverSmResp
        #endregion

        #region [ Enquire Link ]
        private void sendEnquireLink(uint sequence_number)
        {
            try
            {
                byte[] _PDU = new byte[16];
                Utility.CopyIntToArray(16, _PDU, 0);

                Utility.CopyIntToArray(Command.ENQUIRE_LINK, _PDU, 4);

                Utility.CopyIntToArray(0x00000000, _PDU, 8);

                Utility.CopyIntToArray(sequence_number, _PDU, 12);

                Send(_PDU, 16);
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendEnquireLink | " + ex.ToString());
            }
        }//sendEnquireLink

        private void sendEnquireLinkResp(uint sequence_number)
        {
            try
            {
                byte[] _PDU = new byte[16];
                Utility.CopyIntToArray(16, _PDU, 0);

                Utility.CopyIntToArray(Command.ENQUIRE_LINK_RESP, _PDU, 4);

                Utility.CopyIntToArray(0x00000000, _PDU, 8);

                Utility.CopyIntToArray(sequence_number, _PDU, 12);

                Send(_PDU, 16);
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendEnquireLinkResp | " + ex.ToString());
            }
        }//sendEnquireLink
        #endregion

        #region [ Submit SM Resp]

        //private void processSubmitSmResp(SubmitSmRespEventArgs e)
        private void processSubmitSmResp(SmppEventArgs args)
        {
            //SubmitSmRespEventArgs evArg = new SubmitSmRespEventArgs(_sequence_number, _command_status, Utility.ConvertArrayToString(_PDU_body, _body_length - 1));
            SubmitSmEventArgs submitSmEventArgs = null;
            string messageId = String.Empty;
            List<OptionalParameter> tlvParams = new List<OptionalParameter>();

            // skip base part
            int pos = 16;
            // Message Id
            while (args.PDU[pos] != '\0' && pos < args.PDU.Length)
            {
                messageId += (char) args.PDU[pos++];
            }
            if (args.PDU[pos] == '\0' && pos < args.PDU.Length)
                pos++;
            // read tlv parameters
            if (pos < args.PDU.Length)
                tlvParams = decodeTlvParams(args.PDU, pos);

            SubmitSmRespEventArgs submitSmRespEventArgs = new SubmitSmRespEventArgs(args, messageId, tlvParams);
            try
            {
                undeliveredMessages--;
                lock (submittedMessages.SyncRoot)
                {
                    if (submittedMessages.ContainsKey(args.Sequence))
                    {
                        submitSmEventArgs = (SubmitSmEventArgs) submittedMessages[args.Sequence];
                        
                    }
                }

                Task.Run(() => this.OnSubmitSmResp?.Invoke(this, submitSmEventArgs, submitSmRespEventArgs))
                    .ContinueWith(task => {
                        lock (submittedMessages.SyncRoot)
                        {
                            if (submittedMessages.ContainsKey(args.Sequence))
                            {
                                submittedMessages.Remove(args.Sequence);

                            }
                        }
                    });


                #region [ Update Database ]
                //sql = String.Format(
                //            @"INSERT INTO [SMSSent] ([SequenceNo], [MessageId]) VALUES ({0}, '{1}')"
                //            , e.Sequence
                //            , e.MessageID
                //        );
                //LocalStorage.ExecuteNonQuery(sql);
                //logMessage(LogLevels.LogInfo, "processSubmitSmResp/ Delivery Record:Seq No-" + e.Sequence + ", Meassage ID-" + e.MessageID);
                #endregion
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "processSubmitSmResp | " + ex.ToString());
            }
            finally
            {
                args.Dispose();
            }
        }//processSubmitSmResp
        #endregion
        private void initParameters()
        {
            mbResponse = new byte[KernelParameters.MaxBufferSize];
            mPos = 0;
            mLen = 0;

            enquireLinkResponseTime = DateTime.Now;

            undeliveredMessages = 0;
        }//initClientParameters

        private void checkSystemIntegrity(Object state)
        {
            try
            {
                if (mustBeConnected)
                {
                    if (connectionState == ConnectionStates.SMPP_BINDED)
                    {
                        if (enquireLinkSendTime <= enquireLinkResponseTime)
                        {
                            enquireLinkSendTime = DateTime.Now;
                            sendEnquireLink(MC.SequenceNumber);
                            lastSeenConnected = DateTime.Now;
                        }
                        else
                        {
                            logMessage(LogLevels.LogSteps | LogLevels.LogErrors, "checkSystemIntegrity | ERROR #9001 - no response to Enquire Link");
                            tryToReconnect();

                            // disconnect here
                            //this.Disconnect();
                            //this.Dispose();
                        }
                    }
                    else
                    {
                        if (((TimeSpan)(DateTime.Now - lastSeenConnected)).TotalSeconds > KernelParameters.CanBeDisconnected)
                        {
                            logMessage(LogLevels.LogSteps | LogLevels.LogErrors, "checkSystemIntegrity | ERROR #9002 - diconnected more than " + Convert.ToString(KernelParameters.CanBeDisconnected) + " seconds");
                            lastSeenConnected = DateTime.Now.AddSeconds(KernelParameters.CanBeDisconnected);
                            tryToReconnect();
                        }
                        //this.Disconnect();
                        //this.Dispose();
                    }
                }
                else
                {
                    if (connectionState == ConnectionStates.SMPP_UNBIND_SENT)
                    {
                        if (((TimeSpan)(DateTime.Now - lastPacketSentTime)).TotalSeconds > KernelParameters.WaitPacketResponse)
                        {
                            //disconnectSocket();
                            this.Disconnect();
                            this.Dispose();
                        }
                    }
                }
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "checkSystemIntegrity | " + ex.ToString());

                this.Disconnect();
                this.Dispose();
            }

        }//checkSystemIntegrity


        #region [ Delivery Load Timer Event ]
        private void deliveryTimerTick(Object state)
        {
            try
            {
                if (
                    //connectionState == ConnectionStates.SMPP_BINDED
                    this.CanSend
                    && !ReferenceEquals(OnDeliverSmTimerTick, null)
                )
                {
                    if (CanSend)
                    {
                        deliveryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        //OnDeliverSmTimerTick.BeginInvoke(this, onDeliveryTimerCallback, this);
                        Task.Run(() => this.OnDeliverSmTimerTick(this))
                            .ContinueWith(task => onDeliveryTimerCallback(task));
                    }
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "deliveryTimerTick | " + ex.ToString());
            }
        }

        private void onDeliveryTimerCallback(Task task)
        {
            //try
            //{
            //    invokedMethod.EndInvoke(iar);
            //}
            //catch (Exception ex)
            //{
            //    logMessage(LogLevels.LogExceptions, "onDeliveryTimerCallback | " + ex.ToString());
            //}
            //finally
            //{
                deliveryTimer.Change(deliveryLoadTimeout, deliveryLoadTimeout);
            //}
        }

        //private void onDeliveryTimerCallback(IAsyncResult iar)
        //{
        //    var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //    var invokedMethod = (DeliverSmTimerEventHandler)ar.AsyncDelegate;
        //    //var args = (SmppEventArgs)iar.AsyncState;

        //    try
        //    {
        //        invokedMethod.EndInvoke(iar);
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "onDeliveryTimerCallback | " + ex.ToString());
        //    }
        //    finally
        //    {
        //        deliveryTimer.Change(deliveryLoadTimeout, deliveryLoadTimeout);
        //    }
        //}

        #endregion

        #region [ Delivery Timeout ]
        private void deliveryReportTimeoutTimerCallback(object state)
        {
            try
            {
                if (ReferenceEquals(PendingDelivery, null))
                    return;
                if (!PendingDelivery.Any())
                    return;


                DateTime cutOffTime = DateTime.Now.AddSeconds(-KernelParameters.WaitPacketResponse);
                uint[] keys = new uint[0];
                //lock (this.PendingDeliveryLock)
                //{
                //    keys = this.PendingDelivery.Where(x => ReferenceEquals(x.Value, null) || x.Value.SentOn < cutOffTime).Select(x => x.Key).ToArray();
                //}
                keys = this.PendingDelivery.Where(x => ReferenceEquals(x.Value, null) || x.Value.SentOn < cutOffTime).Select(x => x.Key).ToArray();
                foreach (uint key in keys)
                {
                    try
                    {
                        if (!this.PendingDelivery.ContainsKey(key))
                            return;

                        SmppDelivery smppDelivery = this.PendingDelivery[key];
                        smppDelivery.SentOn = null;
                        DeliverSmSentEventArgs e = new DeliverSmSentEventArgs(new SmppEventArgs(16, Command.DELIVER_SM_RESP, 0, 0))
                        {
                            Data = smppDelivery,
                            SentStatus = -1
                        };
                        //this.OnDeliverSmSend.BeginInvoke(this, e, deliveryReportTimerCallbackComplete, e);
                        Task.Run(() => OnDeliverSmSend.Invoke(this, e))
                            .ContinueWith(task => deliveryReportTimerCallbackComplete(task, e));
                        //lock (PendingDeliveryLock)
                        //{
                        //    this.PendingDelivery.Remove(key);
                        //}
                        this.PendingDelivery.TryRemove(key, out smppDelivery);
                    }
                    catch (Exception ex)
                    {
                        //logMessage(LogLevels.LogExceptions, "deliveryReportTimeoutTimerCallback | " + ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "deliveryReportTimeoutTimerCallback | " + ex.ToString());
            }
        }
        #endregion

        #region [ Delivery Send ]
        private void deliveryReportTimerCallback(object state)
        {
            SmppDelivery smppDelivery = null;
            uint sequenceNumber = 0;
            try
            {
                if (ReferenceEquals(DeliveryQueue, null))
                {
                    //logMessage(LogLevels.LogSteps, String.Format("[{0}] deliveryReportTimerCallback | Queue is null", SessionId));
                    return;
                }

                if (ReferenceEquals(OnDeliverSmSend, null))
                {
                    //logMessage(LogLevels.LogSteps, String.Format("[{0}] deliveryReportTimerCallback | Event Handler is null", SessionId));
                    return;
                }

                if (ReferenceEquals(this.DeliveryQueue, null) || this.DeliveryQueue.Count() == 0)
                {
                    //logMessage(LogLevels.LogSteps, String.Format("[{0}] deliveryReportTimerCallback | Queue is empty", SessionId));
                    return;
                }

                if (connectionState != ConnectionStates.SMPP_BINDED)
                {
                    //logMessage(LogLevels.LogSteps, String.Format("[{0}] deliveryReportTimerCallback | Connection not binded", SessionId));
                    return;
                }
                //if (this.SendingDelivery > SMPPConnection.MaxDeliverySendingQueue)
                if (this.PendingDelivery.Count > SMPPConnection.MaxDeliverySendingQueue)

                {
                    //logMessage(LogLevels.LogSteps, String.Format("[{0}] deliveryReportTimerCallback | Too many pending messages", SessionId));
                    return;
                }

                //if (!Monitor.TryEnter(DeliveryQueue))
                //    return;

                if (!CanSend)
                {
                    //logMessage(LogLevels.LogSteps, String.Format("[{0}] deliveryReportTimerCallback | Connection broken", SessionId));
                    return;
                }

                //lock (this.DeliveryQueue.SyncRoot)
                //{
                //    smppDelivery = (SmppDelivery)this.DeliveryQueue.Dequeue();
                //}
                while (!this.DeliveryQueue.TryDequeue(out smppDelivery)) ;
                //this.SendingDelivery++;

                SmppEventArgs smppEventArgs = null;

                //int index = 0;
                //Dictionary<int, string> keyValuePairs = new Dictionary<int, string>();
                List<KeyValuePair<string, string>> keyValuePairs = new List<KeyValuePair<string, string>>();
                //ConfigurationManager.AppSettings["DeliverySmDateFormat"]
                keyValuePairs.Add(new KeyValuePair<string, string>("id", smppDelivery.MessageId));
                keyValuePairs.Add(new KeyValuePair<string, string>("sub", ReferenceEquals(smppDelivery.SubmitTime, null) ? "000" : "001"));
                keyValuePairs.Add(new KeyValuePair<string, string>("dlvrd", ReferenceEquals(smppDelivery.DeliveryTime, null) ? "000" : "001"));
                //keyValuePairs.Add(new KeyValuePair<string, string>("submit date", String.Format("{0:yyyyMMddHHmmss}", smppDelivery.SubmitTime)));
                keyValuePairs.Add(new KeyValuePair<string, string>("submit date", String.Format("{0:ddMMyyHHmm}", smppDelivery.SubmitTime)));
                //keyValuePairs.Add(new KeyValuePair<string, string>("done date", String.Format("{0:yyyyMMddHHmmss}", smppDelivery.DeliveryTime)));
                keyValuePairs.Add(new KeyValuePair<string, string>("done date", String.Format("{0:ddMMyyHHmm}", smppDelivery.DeliveryTime)));
                keyValuePairs.Add(new KeyValuePair<string, string>("stat", ((DeliveryStatus)smppDelivery.DeliveryStatus).ToString()));
                keyValuePairs.Add(new KeyValuePair<string, string>("err", smppDelivery.ErrorCode));
                keyValuePairs.Add(new KeyValuePair<string, string>("text", smppDelivery.ShortMessage.Substring(0, smppDelivery.ShortMessage.Length > 50 ? 49 : smppDelivery.ShortMessage.Length - 1)));

                String messageText = String.Join(" ",
                    keyValuePairs
                    .Where(x => !String.IsNullOrEmpty(x.Value))
                    .Select(x => String.Format("{0}:{1}", x.Key, x.Value))
                    );

                if (messageText.Length > 160)
                    messageText = messageText.Substring(0, 159);
                //string text = String.Format(
                //    "id:{0} sub:{1} dlvrd:{2} submit date:{3} done date:{4} stat:{5} err:{6} text:{7}",
                //    smppDelivery.MessageId,
                //    "000",
                //    "000",
                //    (smppDelivery.SentOn ?? DateTime.Now),
                //    (smppDelivery.DeliveryTime ?? DateTime.Now),
                //    (DeliveryStatus)smppDelivery.DeliveryStatus,
                //    "0000",
                //    smppDelivery.ShortMessage
                //    );



                //int result = this.SendDeliveyReport(
                //    smppDelivery.SourceAddress,
                //    smppDelivery.DestAddress,
                //    messageText.Substring(0, messageText.Length > 160 ? 159 : messageText.Length - 1),
                //    smppDelivery.MessageId,
                //    (DeliveryStatus)smppDelivery.DeliveryStatus,
                //    smppDelivery.DeliveryTime,
                //    ref smppEventArgs
                //);

                //if (CanSend)
                //{ 
                //int sequenceNo = -1;
                byte sourceAddressTon = 5;
                byte sourceAddressNpi = 9;
                string sourceAddress;
                byte destinationAddressTon = 1;
                byte destinationAddressNpi = 1;
                string destinationAddress;
                byte registeredDelivery;

                sourceAddress = Utility.GetString(smppDelivery.SourceAddress, 20, "");

                destinationAddress = Utility.GetString(smppDelivery.DestAddress, 20, "");

                registeredDelivery = askDeliveryReceipt;

                byte protocolId = 0;
                byte priorityFlag = PriorityFlags.VeryUrgent;
                //DateTime sheduleDeliveryTime = DateTime.MinValue;
                //DateTime validityPeriod = DateTime.MinValue;
                byte replaceIfPresentFlag = ReplaceIfPresentFlags.DoNotReplace;
                byte smDefaultMsgId = 0;

                //byte[] message = Utility.ConvertStringToByteArray(messageText.Length > 255 ? messageText.Substring(0, 255) : messageText);
                //string smsText;

                List<OptionalParameter> parameters = new List<OptionalParameter>();


                parameters.Add(new OptionalParameter(TagCodes.RECEIPTED_MESSAGE_ID, Utility.ConvertStringToByteArray(smppDelivery.MessageId)));
                parameters.Add(new OptionalParameter(TagCodes.MESSAGE_STATE, new byte[1] { (byte)smppDelivery.DeliveryStatus }));


                string sServiceType = Utility.GetString("", 5, "");
                byte[] _service_type = new byte[sServiceType.Length + 1];
                Array.Copy(Utility.ConvertStringToByteArray(sServiceType), 0, _service_type, 0, sServiceType.Length);

                string sSourceAddress = Utility.GetString(sourceAddress, 20, "");
                byte[] _source_addr = new byte[sSourceAddress.Length + 1];
                Array.Copy(Utility.ConvertStringToByteArray(sSourceAddress), 0, _source_addr, 0, sSourceAddress.Length);

                string sDestAddr = Utility.GetString(destinationAddress, 20, "");
                byte[] _dest_addr = new byte[sDestAddr.Length + 1];
                Array.Copy(Utility.ConvertStringToByteArray(sDestAddr), 0, _dest_addr, 0, sDestAddr.Length);

                string sScheduledDeliveryTime = ReferenceEquals(smppDelivery.DeliveryTime, null) ? "" : Utility.GetDateString((DateTime)smppDelivery.DeliveryTime);
                byte[] _scheduled_delivery_time = new byte[sScheduledDeliveryTime.Length + 1];
                Array.Copy(Utility.ConvertStringToByteArray(sScheduledDeliveryTime), 0, _scheduled_delivery_time, 0, sScheduledDeliveryTime.Length);

                string sValidityPeriod = ReferenceEquals(smppDelivery.DeliveryTime, null) ? "" : Utility.GetDateString((DateTime)smppDelivery.DeliveryTime);
                byte[] _validity_period = new byte[sValidityPeriod.Length + 1];
                Array.Copy(Utility.ConvertStringToByteArray(sValidityPeriod), 0, _validity_period, 0, sValidityPeriod.Length);

                string sShortMessage = Utility.GetString(messageText, 254, "");
                byte[] _short_message = new byte[sShortMessage.Length + 1];
                Array.Copy(Utility.ConvertStringToByteArray(sShortMessage), 0, _short_message, 0, sShortMessage.Length);

                sequenceNumber = MC.SequenceNumber;
                //smppDelivery.Id = smppEventArgs.Id;
                smppDelivery.SentOn = DateTime.Now;
                //lock (PendingDeliveryLock)
                //{
                //    this.PendingDelivery.Add(sequenceNumber, smppDelivery);
                //}
                this.PendingDelivery.TryAdd(sequenceNumber, smppDelivery);
                int result = sendDeliverSm(
                    sequenceNumber,
                    _service_type,
                    sourceAddressTon,
                    sourceAddressNpi,
                    _source_addr,
                    destinationAddressTon,
                    destinationAddressNpi,
                    _dest_addr,
                    0x04, // esm-class
                    protocolId,
                    priorityFlag,
                    _scheduled_delivery_time,
                    _validity_period,
                    registeredDelivery,
                    replaceIfPresentFlag,
                    0x00,
                    smDefaultMsgId,
                    (byte)_short_message.Length,
                    _short_message,
                    parameters,
                    ref smppEventArgs
                );

                if (result != 0)
                    throw new SmppException(StatusCodes.ESME_RSYSERR);

                smppDelivery.CommandId = smppEventArgs.Id;
                //lock (this.PendingDeliveryLock)
                //{
                //    if (this.PendingDelivery.ContainsKey(sequenceNumber))
                //        this.PendingDelivery[sequenceNumber] = smppDelivery;
                //    else
                //        this.PendingDelivery.Add(sequenceNumber, smppDelivery);
                //}

                if (this.PendingDelivery.ContainsKey(sequenceNumber))
                {
                    SmppDelivery currentSmppDelivery;
                    this.PendingDelivery.TryGetValue(sequenceNumber, out currentSmppDelivery);
                    this.PendingDelivery.TryUpdate(sequenceNumber, smppDelivery, currentSmppDelivery);
                }
                else
                    this.PendingDelivery.TryAdd(sequenceNumber, smppDelivery);

                //DeliverSmSentEventArgs e = new DeliverSmSentEventArgs(smppEventArgs)
                //{
                //    Data = smppDelivery,
                //    SentStatus = result
                //};
                //this.OnDeliverSmSend.BeginInvoke(this, e, deliveryReportTimerCallbackComplete, e);

                //}

                //catch (Exception ex)
                //{
                //    this.DeliveryQueue.Enqueue(smppDelivery);
                //    this.SendingDelivery--;
                //    logMessage(LogLevels.LogExceptions, "deliveryReportTimerCallback | " + ex.ToString());
                //}
                //finally
                //{
                //    //Monitor.Exit(DeliveryQueue);
                //}

            }
            catch (Exception ex)
            {
                if (!ReferenceEquals(smppDelivery, null))
                {
                    //lock (DeliveryQueue.SyncRoot)
                    //{
                    this.DeliveryQueue.Enqueue(smppDelivery);
                    //}
                }
                if (this.PendingDelivery.ContainsKey(sequenceNumber))
                {
                    //lock(PendingDeliveryLock) {
                    //    this.PendingDelivery.Remove(sequenceNumber);
                    //}
                    SmppDelivery smppDeliveryToRemove;
                    this.PendingDelivery.TryRemove(sequenceNumber, out smppDeliveryToRemove);
                }
                //this.SendingDelivery--;
                logMessage(LogLevels.LogExceptions, "deliveryReportTimerCallback | " + ex.ToString());
            }
        }

        private void deliveryReportTimerCallbackComplete(Task task, DeliverSmSentEventArgs args)
        {
            args.Dispose();
        }
        //private void deliveryReportTimerCallbackComplete(IAsyncResult iar)
        //{
        //    var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //    var invokedMethod = (DeliverSmSendEventHandler)ar.AsyncDelegate;
        //    var args = (DeliverSmSentEventArgs)iar.AsyncState;

        //    try
        //    {
        //        invokedMethod.EndInvoke(iar);
        //        //SmppDelivery smppDelivery;
        //        //this.PendingDelivery.TryRemove(args.Sequence, out smppDelivery);
        //        //this.SendingDelivery--;
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "deliveryReportTimerCallbackComplete | " + ex.ToString());
        //    }

        //    args.Dispose();
        //}
        #endregion

        #region [ Message Parts Timers ]
        public void messagePartsTimerCallback(object state)
        {
            try
            {
                CleanMessageParts(900);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "messagePartsTimerCallback | " + ex.ToString());
            }
        }
        #endregion


        #region [ NACK ]
        private void sendGenericNack(uint sequence_number, int command_status)
        {
            try
            {
                byte[] _PDU = new byte[16];

                Utility.CopyIntToArray(16, _PDU, 0);

                Utility.CopyIntToArray(Command.GENERIC_NACK, _PDU, 4);

                Utility.CopyIntToArray(command_status, _PDU, 8);

                Utility.CopyIntToArray(sequence_number, _PDU, 12);

                Send(_PDU, 16);
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendGenericNack | " + ex.ToString());
            }

        }//sendGenericNack
        #endregion

        #region [ Submit SM ]
        //private void decodeAndProcessSubmitSm(uint sequence_number, uint command_status, byte[] _body)
        private void decodeAndProcessSubmitSm(SmppEventArgs args)
        {
            uint sequence_number = args.Sequence;
            uint command_status = args.CommandStatus;
            byte[] _body = args.PDU.Skip(16).ToArray();

            try
            {
                int pos = 0;
                List<OptionalParameter> tlvParams = new List<OptionalParameter>();

                byte[] service_type = new byte[6];
                int service_type_length = 0;
                byte source_addr_ton;
                byte source_addr_npi;
                byte[] source_addr = new byte[21];
                byte source_addr_length = 0;
                byte dest_addr_ton;
                byte dest_addr_npi;
                byte[] dest_addr = new byte[21];
                int dest_addr_length = 0;
                byte esm_class;
                byte protocol_id;
                byte priority_flag;
                byte[] scheduled_delivery_time = new byte[17];
                int scheduled_delivery_time_length = 0;
                byte[] validity_period = new byte[17];
                int validity_period_length = 0;
                byte registered_delivery;
                byte replace_if_present_flag;
                byte data_coding;
                byte sm_default_msg_id;
                byte sm_length;
                byte[] short_message = new byte[255];
                string respMessageId = String.Empty;

                // service type - varchar 6
                while (service_type_length < service_type.Length && _body[pos] != 0x00)
                {
                    service_type[service_type_length++] = _body[pos++];
                }

                if (_body[pos] != 0x00)
                {
                    //sendBindTransceiverResp(sequence_number, StatusCodes.ESME_RINVPASWD, String.Empty);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x01");
                    return;
                }
                pos++;

                source_addr_ton = _body[pos++];
                source_addr_npi = _body[pos++];

                // source address - varchar 21
                while (source_addr_length < source_addr.Length && _body[pos] != 0x00)
                {
                    source_addr[source_addr_length++] = _body[pos++];
                }

                if (_body[pos] != 0x00)
                {
                    //sendBindTransceiverResp(sequence_number, StatusCodes.ESME_RINVPASWD, String.Empty);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x01");
                    return;
                }
                pos++;

                dest_addr_ton = _body[pos];
                pos++;

                dest_addr_npi = _body[pos];
                pos++;

                // dest address - varchar 21
                while (dest_addr_length < dest_addr.Length && _body[pos] != 0x00)
                {
                    dest_addr[dest_addr_length++] = _body[pos++];
                }

                if (_body[pos] != 0x00)
                {
                    //sendBindTransceiverResp(sequence_number, StatusCodes.ESME_RINVPASWD, String.Empty);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x01");
                    return;
                }
                pos++;


                esm_class = _body[pos];
                pos++;

                protocol_id = _body[pos];
                pos++;

                priority_flag = _body[pos];
                pos++;

                // scheduled_delivery_time - 1 or 17
                if (_body[pos] == 0x00)
                {
                    scheduled_delivery_time_length = 1;
                }
                else
                {
                    Array.Copy(_body, pos, scheduled_delivery_time, 0, 17);
                    scheduled_delivery_time_length = 17;
                }
                pos += scheduled_delivery_time_length;

                // validity_period - 1 or 17
                if (_body[pos] == 0x00)
                {
                    validity_period_length = 1;
                }
                else
                {
                    Array.Copy(_body, pos, validity_period, 0, 17);
                    validity_period_length = 17;
                }
                pos += validity_period_length;

                registered_delivery = _body[pos++];
                replace_if_present_flag = _body[pos++];
                data_coding = _body[pos++];
                sm_default_msg_id = _body[pos++];
                sm_length = _body[pos++];

                Array.Copy(_body, pos, short_message, 0, sm_length);
                pos += sm_length;


                if (pos < _body.Length)
                    tlvParams = decodeTlvParams(_body, pos);

                if (OnSubmitSm != null)
                {
                    SubmitSmEventArgs e = new SubmitSmEventArgs(
                        args,
                        Utility.ConvertArrayToString(service_type, service_type_length),
                        source_addr_ton,
                        source_addr_npi,
                        Utility.ConvertArrayToString(source_addr, source_addr_length),
                        dest_addr_ton,
                        dest_addr_npi,
                        Utility.ConvertArrayToString(dest_addr, dest_addr_length),
                        esm_class,
                        protocol_id,
                        priority_flag,
                        Utility.ParseDateString(Utility.ConvertArrayToString(scheduled_delivery_time, scheduled_delivery_time.Length)),
                        Utility.ParseDateString(Utility.ConvertArrayToString(validity_period, validity_period_length)),
                        registered_delivery,
                        replace_if_present_flag,
                        data_coding,
                        sm_default_msg_id,
                        sm_length,
                        short_message,
                        tlvParams
                    );
                    //respMessageId = await OnSubmitSm(this, .e);

                    logMessage(LogLevels.LogSteps, "Submit_Sm Invoked");
                    //OnSubmitSm.BeginInvoke(this, e, onSubmitSmEventComplete, e);
                    Task.Run(() => OnSubmitSm.Invoke(this, e))
                        .ContinueWith(task => onSubmitSmEventComplete(task, e));
                }
                //sendSubmitSmResp(sequence_number, command_status, respMessageId);
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (SmppException ex)
            {
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | Error decoding Submit_Sm");
                logMessage(LogLevels.LogPdu, "decodeAndProcessDeliverSm | PDU : " + Utility.ConvertArrayToHexString(args.PDU, (int)args.CommandLength));
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | " + ex.ToString());
                sendSubmitSmResp(sequence_number, ex.ErrorCode, String.Empty);
                args.Dispose();
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | PDU : " + Utility.ConvertArrayToHexString(args.PDU, (int)args.CommandLength));
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | " + ex.ToString());
                args.Dispose();
            }
        }
        private void onSubmitSmEventComplete(Task<string> task, SubmitSmEventArgs args)
        {
            logMessage(LogLevels.LogSteps, String.Format("Submit_Sm complete | Message Id {0}", args.Id));
            try
            {
                string respMessageId = task.Result;

                sendSubmitSmResp(args.Sequence, StatusCodes.ESME_ROK, respMessageId);
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                sendSubmitSmResp(args.Sequence, StatusCodes.ESME_RSYSERR, String.Empty);

                logMessage(LogLevels.LogExceptions, "onSubmitSmEventComplete | " + ex.ToString());
            }
            args.Dispose();
        }
        //private void onSubmitSmEventComplete(IAsyncResult iar)
        //{

        //    var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //    var invokedMethod = (SubmitSmEventHandler)ar.AsyncDelegate;
        //    var args = (SubmitSmEventArgs)iar.AsyncState;
        //    logMessage(LogLevels.LogSteps, String.Format("Submit_Sm complete | Message Id {0}", args.Id));
        //    try
        //    {
        //        string respMessageId = invokedMethod.EndInvoke(iar);

        //        sendSubmitSmResp(args.Sequence, StatusCodes.ESME_ROK, respMessageId);
        //    }
        //    catch (ObjectDisposedException ex)
        //    {
        //        return;
        //    }
        //    catch (Exception ex)
        //    {
        //        sendSubmitSmResp(args.Sequence, StatusCodes.ESME_RSYSERR, String.Empty);

        //        logMessage(LogLevels.LogExceptions, "onSubmitSmEventComplete | " + ex.ToString());
        //    }
        //    args.Dispose();
        //}
        #endregion

        #region [ Submit SM Resp ]
        public void sendSubmitSmResp(uint sequence_number, uint command_status, string messageId)
        {

            byte[] message_id = Utility.ConvertStringToByteArray(messageId);
            sendSubmitSmResp(sequence_number, command_status, message_id);
        }

        private void sendSubmitSmResp(uint sequence_number, uint command_status, byte[] message_id)
        {
            try
            {
                byte[] _PDU = new byte[KernelParameters.MaxPduSize];
                int pos = 0;

                pos += 4;

                Utility.CopyIntToArray(Command.SUBMIT_SM_RESP, _PDU, pos);
                pos += 4;

                Utility.CopyIntToArray(command_status, _PDU, pos);
                pos += 4;

                Utility.CopyIntToArray(sequence_number, _PDU, pos);
                pos += 4;

                Array.Copy(message_id, 0, _PDU, 16, message_id.Length);
                pos += message_id.Length;

                _PDU[pos] = 0;
                pos += 1;

                Utility.CopyIntToArray(pos, _PDU, 0);

                Send(_PDU, pos);

            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendSubmitSmResp | " + ex.ToString());
            }
        } // sendSubmitSmResp
        #endregion

        #region [ Deliver SM ]
        //private int sendDeliverSm(
        //    string service_type,
        //    byte source_addr_ton,
        //    byte source_addr_npi,
        //    string source_addr,
        //    byte dest_addr_ton,
        //    byte dest_addr_npi,
        //    string dest_addr,
        //    byte esm_class,
        //    byte protocol_id,
        //    byte priority_flag,
        //    DateTime? scheduled_delivery_time,
        //    DateTime? validity_period,
        //    byte registered_delivery,
        //    byte replace_if_present_flag,
        //    byte data_coding,
        //    byte sm_default_msg_id,
        //    byte sm_length,
        //    string short_message,
        //    List<OptionalParameter> optionalParameters = null
        //)
        //{
        //    string sServiceType = Utility.GetString(service_type, 5, "");
        //    byte[] _service_type = new byte[sServiceType.Length + 1];
        //    Array.Copy(Utility.ConvertStringToByteArray(sServiceType), 0, _service_type, 0, sServiceType.Length);

        //    string sSourceAddress = Utility.GetString(source_addr, 20, "");
        //    byte[] _source_addr = new byte[sSourceAddress.Length + 1];
        //    Array.Copy(Utility.ConvertStringToByteArray(sSourceAddress), 0, _source_addr, 0, sSourceAddress.Length);

        //    string sDestAddr = Utility.GetString(dest_addr, 20, "");
        //    byte[] _dest_addr = new byte[sDestAddr.Length + 1];
        //    Array.Copy(Utility.ConvertStringToByteArray(sDestAddr), 0, _dest_addr, 0, sDestAddr.Length);

        //    string sScheduledDeliveryTime = ReferenceEquals(scheduled_delivery_time, null) ? "" : Utility.GetDateString((DateTime)scheduled_delivery_time);
        //    byte[] _scheduled_delivery_time = new byte[sScheduledDeliveryTime.Length + 1];
        //    Array.Copy(Utility.ConvertStringToByteArray(sScheduledDeliveryTime), 0, _scheduled_delivery_time, 0, sScheduledDeliveryTime.Length);

        //    string sValidityPeriod = ReferenceEquals(validity_period, null) ? "" : Utility.GetDateString((DateTime)validity_period);
        //    byte[] _validity_period = new byte[sValidityPeriod.Length + 1];
        //    Array.Copy(Utility.ConvertStringToByteArray(sValidityPeriod), 0, _validity_period, 0, sValidityPeriod.Length);

        //    string sShortMessage = Utility.GetString(short_message, 254, "");
        //    byte[] _short_message = new byte[sShortMessage.Length + 1];
        //    Array.Copy(Utility.ConvertStringToByteArray(sShortMessage), 0, _short_message, 0, sShortMessage.Length);

        //    //byte[] _service_type = Utility.ConvertStringToByteArray(Utility.GetString(service_type, 5, ""));
        //    //byte[] _source_addr = Utility.ConvertStringToByteArray(Utility.GetString(source_addr, 20, ""));
        //    //byte[] _dest_addr = Utility.ConvertStringToByteArray(Utility.GetString(dest_addr, 20, ""));
        //    //byte[] _scheduled_delivery_time = Utility.ConvertStringToByteArray(Utility.GetDateString(scheduled_delivery_time));
        //    //byte[] _validity_period = Utility.ConvertStringToByteArray(Utility.GetDateString(validity_period));
        //    //byte[] _short_message = Utility.ConvertStringToByteArray(Utility.GetString(short_message, 254, ""));

        //    return sendDeliverSm(
        //        _service_type,
        //        source_addr_ton,
        //        source_addr_npi,
        //        _source_addr,
        //        dest_addr_ton,
        //        dest_addr_npi,
        //        _dest_addr,
        //        esm_class,
        //        protocol_id,
        //        priority_flag,
        //        _scheduled_delivery_time,
        //        _validity_period,
        //        registered_delivery,
        //        replace_if_present_flag,
        //        data_coding,
        //        sm_default_msg_id,
        //        sm_length,
        //        _short_message,
        //        optionalParameters
        //    );
        //}

        private int sendDeliverSm(
            uint sequence_number,
            byte[] service_type,
            byte source_addr_ton,
            byte source_addr_npi,
            byte[] source_addr,
            byte dest_addr_ton,
            byte dest_addr_npi,
            byte[] dest_addr,
            byte esm_class,
            byte protocol_id,
            byte priority_flag,
            byte[] scheduled_delivery_time,
            byte[] validity_period,
            byte registered_delivery,
            byte replace_if_present_flag,
            byte data_coding,
            byte sm_default_msg_id,
            byte sm_length,
            byte[] short_message,
            List<OptionalParameter> optionalParameters,
            ref SmppEventArgs smppEventArgs
        )
        {
            try
            {


                int pos;
                uint _sequence_number;
                byte[] _DELIVER_SM_PDU = new byte[KernelParameters.MaxPduSize];
                int scheduled_delivery_time_length = 0;
                int validity_period_length = 0;

                ////////////////////////////////////////////////////////////////////////////////////////////////
                /// Start filling PDU						

                Utility.CopyIntToArray(Command.DELIVER_SM, _DELIVER_SM_PDU, 4); //command_id
                //_sequence_number = smscArray.currentSMSC.SequenceNumber;
                _sequence_number = sequence_number;
                Utility.CopyIntToArray(_sequence_number, _DELIVER_SM_PDU, 12); //sequence_number
                pos = 16;
                _DELIVER_SM_PDU[pos] = 0x00; //service_type
                pos += 1;
                _DELIVER_SM_PDU[pos] = source_addr_ton; //source_addr_ton
                pos += 1;
                _DELIVER_SM_PDU[pos] = source_addr_npi; // source_addr_npi
                pos += 1;
                for (int source_addr_length = 0; source_addr_length < 20 && source_addr_length < source_addr.Length && source_addr[source_addr_length] != 0x00; source_addr_length++, pos++)
                {
                    _DELIVER_SM_PDU[pos] = source_addr[source_addr_length];
                }
                _DELIVER_SM_PDU[pos] = 0x00;
                pos += 1;
                _DELIVER_SM_PDU[pos] = dest_addr_ton; // dest_addr_ton
                pos += 1;
                _DELIVER_SM_PDU[pos] = dest_addr_npi; // dest_addr_npi
                pos += 1;
                for (int dest_addr_length = 0; dest_addr_length < 20 && dest_addr_length < dest_addr.Length && dest_addr[dest_addr_length] != 0x00; dest_addr_length++, pos++)
                {
                    _DELIVER_SM_PDU[pos] = dest_addr[dest_addr_length];
                }
                _DELIVER_SM_PDU[pos] = 0x00;
                pos += 1;
                _DELIVER_SM_PDU[pos] = esm_class; // esm_class
                pos += 1;
                _DELIVER_SM_PDU[pos] = protocol_id; // protocol_id
                pos += 1;
                _DELIVER_SM_PDU[pos] = priority_flag; // priority_flag
                pos += 1;

                scheduled_delivery_time_length = scheduled_delivery_time[0] == 0x00 ? 1 : 17;
                Array.Copy(scheduled_delivery_time, 0, _DELIVER_SM_PDU, pos, scheduled_delivery_time_length);
                pos += scheduled_delivery_time_length;

                validity_period_length = validity_period[0] == 0x00 ? 1 : 17;
                Array.Copy(validity_period, 0, _DELIVER_SM_PDU, pos, validity_period_length);
                pos += validity_period_length;

                //_DELIVER_SM_PDU[pos] = 0x00;
                //pos += 1;
                _DELIVER_SM_PDU[pos] = registered_delivery; // registered_delivery
                pos += 1;
                _DELIVER_SM_PDU[pos] = replace_if_present_flag; // replace_if_present_flag
                pos += 1;
                _DELIVER_SM_PDU[pos] = data_coding; // data_coding
                pos += 1;
                _DELIVER_SM_PDU[pos] = sm_default_msg_id; // sm_default_msg_id
                pos += 1;

                sm_length = short_message.Length > 254 ? (byte)254 : (byte)short_message.Length; // sm_length

                _DELIVER_SM_PDU[pos] = sm_length;
                pos += 1;
                Array.Copy(short_message, 0, _DELIVER_SM_PDU, pos, sm_length); // short_message
                pos += sm_length;

                //_DELIVER_SM_PDU[pos] = 0x00;

                //pos++;

                if (!ReferenceEquals(optionalParameters, null) && optionalParameters.Any())
                {
                    byte[] tlv = encodeTlvParams(optionalParameters);

                    Array.Copy(tlv, 0, _DELIVER_SM_PDU, pos, tlv.Length);
                    //pos += 4;
                    pos += tlv.Length;
                }

                Utility.CopyIntToArray(pos, _DELIVER_SM_PDU, 0);

                //Send(_DELIVER_SM_PDU, pos);


                using (smppEventArgs = new SmppEventArgs(_DELIVER_SM_PDU, pos))
                {
                    Send(smppEventArgs);
                }
                return 0;
            }
            catch (ObjectDisposedException ex)
            {
                return -1;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendDeliverSm | " + ex.ToString());
            }
            return -1;
        }

        private void decodeAndProcessDeliverSmResp(SmppEventArgs args)
        {
            try
            {
                if (!this.PendingDelivery.ContainsKey(args.Sequence))
                    return;

                SmppDelivery smppDelivery = this.PendingDelivery[args.Sequence];
                if (!ReferenceEquals(args, null))
                //SmppDelivery smppDelivery;
                //if (!ReferenceEquals(args, null) && this.PendingDelivery.TryGetValue(args.Sequence, out smppDelivery))
                {

                    //DeliverSmSentEventArgs e = new DeliverSmSentEventArgs(args)
                    //{
                    //    Data = smppDelivery,
                    //    SentStatus = 0
                    //};
                    //byte[] pdu = new byte[args.PDU.Length];
                    //Array.Copy(args.PDU, pdu, pdu.Length);
                    DeliverSmSentEventArgs e = new DeliverSmSentEventArgs(args.CommandLength, args.CommandId, args.CommandStatus, args.Sequence, args.PDU)
                    {
                        Data = smppDelivery,
                        SentStatus = 0
                    };
                    //this.OnDeliverSmSend.BeginInvoke(this, e, deliveryReportTimerCallbackComplete, e);
                    Task.Run(() => OnDeliverSmSend.Invoke(this, e))
                            .ContinueWith(task => deliveryReportTimerCallbackComplete(task, e));
                    this.PendingDelivery.TryRemove(args.Sequence, out smppDelivery);
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSmResp | " + ex.ToString());
            }
            finally
            {
                args.Dispose();
            }
        }
        #endregion

        #region [ Deliver SM Resp ]
        private void processDeliverSm(DeliverSmEventArgs e)
        {
            try
            {
                if (OnDeliverSm != null)
                {
                    Task.Run<uint>(() => this.OnDeliverSm(this, e))
                        .ContinueWith(task => {
                            if (!task.IsFaulted)
                                sendDeliverSmResp(e.SequenceNumber, task.Result);
                            else
                                sendDeliverSmResp(e.SequenceNumber, StatusCodes.ESME_RDELIVERYFAILURE);
                        });
                    logMessage(LogLevels.LogInfo, "processDeliverSm/ Delivery Record:" + "To-" + e.To + " From-" + e.From + " Meassage ID-" + e.ReceiptedMessageID + " HexString-" + e.TextString);
                }
                else
                    sendDeliverSmResp(e.SequenceNumber, StatusCodes.ESME_ROK);
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "processDeliverSm | " + ex.ToString());
            }
        }//processDeliverSm


        private void sendDeliverSmResp(uint sequence_number, uint command_status)
        {
            try
            {
                byte[] _PDU = new byte[17];

                Utility.CopyIntToArray(17, _PDU, 0);

                Utility.CopyIntToArray(0x80000005, _PDU, 4);

                Utility.CopyIntToArray(command_status, _PDU, 8);

                Utility.CopyIntToArray(sequence_number, _PDU, 12);

                _PDU[16] = 0;

                Send(_PDU, 17);

            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendDeliverSmResp | " + ex.ToString());
            }
        }//sendDeliverSmResp

        private void decodeAndProcessDeliverSm(uint sequence_number, byte[] _body, int _length)
        {
            //logMessage(LogLevels.LogDebug,
            //    "sequence_number : " + sequence_number.ToString()
            //    + "\r\n_body : " + BitConverter.ToString(_body)
            //    + "\r\nlength : " + _length.ToString());
            if (_length < 17)
            {
                sendDeliverSmResp(sequence_number, StatusCodes.ESME_RINVCMDLEN);
                return;
            }
            try
            {
                bool isDeliveryReceipt = false;
                bool isUdhiSet = false;
                byte _source_addr_ton, _source_addr_npi, _dest_addr_ton, _dest_addr_npi;
                byte _priority_flag, _data_coding, _esm_class;
                byte[] _source_addr = new byte[21];
                byte[] _dest_addr = new byte[21];
                byte saLength, daLength;
                byte[] _short_message = new byte[0];
                int _sm_length;
                int pos;

                /////////////////////////////////////
                /// Message Delivery Params
                /// 
                byte[] _receipted_message_id = new byte[254];
                byte _receipted_message_id_len = 0;
                byte _message_state = 255;
                Int16 sar_msg_ref_num = 0;
                byte sar_total_segments = 0;
                byte sar_segment_seqnum = 0;
                #region [ Added by Anirban Seth on 16 Jan 2019 - TLV Processing ]
                Dictionary<string, string> shortMessageValueList = null;// = new List<KeyValuePair<string, string>>();
                #endregion

                pos = 0;
                while ((pos < 5) && (_body[pos] != 0x00))
                    pos++;
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x01");
                    return;
                }
                _source_addr_ton = _body[++pos];
                _source_addr_npi = _body[++pos];
                pos++;
                saLength = 0;
                while ((saLength < 20) && (_body[pos] != 0x00))
                {
                    _source_addr[saLength] = _body[pos];
                    pos++;
                    saLength++;
                }
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x02");
                    return;
                }
                _dest_addr_ton = _body[++pos];
                _dest_addr_npi = _body[++pos];
                pos++;
                daLength = 0;
                while ((daLength < 20) && (_body[pos] != 0x00))
                {
                    _dest_addr[daLength] = _body[pos];
                    pos++;
                    daLength++;
                }
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x03");
                    return;
                }
                _esm_class = _body[++pos];
                switch (_esm_class)
                {
                    case 0x00:
                        break;
                    case 0x04:
                        logMessage(LogLevels.LogSteps, "Delivery Receipt Received");
                        //logMessage(LogLevels.LogSteps, "Delivery Receipt "+Utility.ConvertArrayToString(_body,_length));
                        isDeliveryReceipt = true;
                        break;
                    case 0x40:
                        logMessage(LogLevels.LogSteps, "UDHI Indicator set");
                        isUdhiSet = true;
                        break;
                    default:
                        logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "Unknown esm_class for DELIVER_SM : " + Utility.GetHexFromByte(_esm_class));
                        break;
                }
                pos += 1;
                //pos += 5;
                _priority_flag = _body[++pos];
                pos += 4;
                _data_coding = _body[++pos];
                pos += 1;
                _sm_length = _body[++pos];
                pos += 1;
                //logMessage(LogLevels.LogDebug, "_source_addr_ton : " + Convert.ToString(_source_addr_ton));
                //logMessage(LogLevels.LogDebug, "_source_addr_npi : " + Convert.ToString(_source_addr_npi));
                //logMessage(LogLevels.LogDebug, "_dest_addr_ton : " + Convert.ToString(_dest_addr_ton));
                //logMessage(LogLevels.LogDebug, "_dest_addr_npi : " + Convert.ToString(_dest_addr_npi));
                //logMessage(LogLevels.LogDebug, "_esm_class : " + Convert.ToString(_esm_class));
                //logMessage(LogLevels.LogDebug, "_priority_flag : " + Convert.ToString(_priority_flag));
                //logMessage(LogLevels.LogDebug, "_data_coding : " + Convert.ToString(_data_coding));
                //logMessage(LogLevels.LogDebug, "_sm_length : " + Convert.ToString(_sm_length));
                if (_sm_length > 0)
                {
                    _short_message = new byte[_sm_length];
                    Array.Copy(_body, pos, _short_message, 0, _sm_length);
                    //logMessage(LogLevels.LogDebug, "_short_message : " + BitConverter.ToString(_short_message));
                    //logMessage(LogLevels.LogSteps, "_short_message : " + Encoding.UTF8.GetString(_short_message));
                    #region [ Added by Anirban Seth on 16 Jan 2019 - TLV Processing ]
                    Utility.ParseDelivertReportShortMessage(_short_message, _sm_length, out shortMessageValueList);
                    #endregion

                    pos += _sm_length;
                }
                if ((isDeliveryReceipt) || (isUdhiSet))
                {
                    int _par_tag, _par_tag_length;
                    bool exit = false;
                    if (_sm_length > 0)
                    {
                        //pos += 1;// _sm_length;
                    }
                    while ((pos < _length) && (exit == false))
                    {
                        if (Utility.Get2ByteIntFromArray(_body, pos, _length, out _par_tag) == false)
                        {
                            exit = true;
                            break;
                        }
                        pos += 2;
                        if (Utility.Get2ByteIntFromArray(_body, pos, _length, out _par_tag_length) == false)
                        {
                            exit = true;
                            break;
                        }
                        pos += 2;
                        switch (_par_tag)
                        {
                            case 0x020C: // 524
                                if (((pos + _par_tag_length - 1) <= _length) && (_par_tag_length == 2))
                                {
                                    byte[] temp = new byte[_par_tag_length];
                                    Array.Copy(_body, pos, temp, 0, _par_tag_length);
                                    pos += _par_tag_length;
                                    sar_msg_ref_num = BitConverter.ToInt16(temp, 0);
                                    logMessage(LogLevels.LogSteps, "sar_msg_ref_num : " + sar_msg_ref_num);
                                }
                                else
                                    exit = true;

                                break;
                            case 0x020E: // 526
                                if ((pos <= _length) && (_par_tag_length == 1))
                                {
                                    sar_total_segments = _body[pos];
                                    logMessage(LogLevels.LogSteps, "sar_total_segments : " + Convert.ToString(sar_total_segments));
                                    pos++;
                                }
                                else
                                    exit = true;

                                break;
                            case 0x020F: // 527
                                if ((pos <= _length) && (_par_tag_length == 1))
                                {
                                    sar_segment_seqnum = _body[pos];
                                    logMessage(LogLevels.LogSteps, "sar_segment_seqnum : " + Convert.ToString(sar_segment_seqnum));
                                    pos++;
                                }
                                else
                                    exit = true;

                                break;
                            case 0x0427: // 1063
                                if ((pos <= _length) && (_par_tag_length == 1))
                                {
                                    _message_state = _body[pos];
                                    logMessage(LogLevels.LogSteps, "Message state : " + Convert.ToString(_message_state));
                                    pos++;
                                }
                                else
                                    exit = true;

                                break;
                            case 0x001E: // 30
                                if ((pos + _par_tag_length - 1) <= _length)
                                {
                                    _receipted_message_id = new byte[_par_tag_length];
                                    Array.Copy(_body, pos, _receipted_message_id, 0, _par_tag_length);
                                    _receipted_message_id_len = Convert.ToByte(_par_tag_length);
                                    pos += _par_tag_length;
                                    logMessage(LogLevels.LogSteps, "Delivered message id : " + Utility.ConvertArrayToString(_receipted_message_id, _receipted_message_id_len));
                                }
                                else
                                    exit = true;
                                break;
                            default:
                                if ((pos + _par_tag_length - 1) <= _length)
                                    pos += _par_tag_length;
                                else
                                    exit = true;
                                logMessage(LogLevels.LogInfo, "_par_tag : " + Convert.ToString(_par_tag));
                                logMessage(LogLevels.LogInfo, "_par_tag_length : " + Convert.ToString(_par_tag_length));
                                break;

                        }
                    }
                    //logMessage(LogLevels.LogDebug, "Delivery Receipt Processing Exit value - " + Convert.ToString(exit));
                    if (exit)
                        isDeliveryReceipt = false;
                }

                if ((sar_msg_ref_num > 0) && ((sar_total_segments > 0) && ((sar_segment_seqnum > 0) && (isUdhiSet))))
                {
                    lock (sarMessages.SyncRoot)
                    {
                        SortedList tArr = new SortedList();
                        if (sarMessages.ContainsKey(sar_msg_ref_num))
                        {
                            tArr = (SortedList)sarMessages[sar_msg_ref_num];
                            if (tArr.ContainsKey(sar_segment_seqnum))
                                tArr[sar_segment_seqnum] = _short_message;
                            else
                                tArr.Add(sar_segment_seqnum, _short_message);
                            bool isFull = true;
                            byte i;
                            for (i = 1; i <= sar_total_segments; i++)
                            {
                                if (!tArr.ContainsKey(i))
                                {
                                    isFull = false;
                                    break;
                                }//if
                            }//for
                            if (!isFull)
                            {
                                sarMessages[sar_msg_ref_num] = tArr;
                                sendDeliverSmResp(sequence_number, StatusCodes.ESME_ROK);
                                return;
                            }
                            else
                            {
                                _sm_length = 0;
                                for (i = 1; i <= sar_total_segments; i++)
                                {
                                    _sm_length += ((byte[])tArr[i]).Length;
                                }
                                _short_message = new byte[_sm_length + 100];
                                _sm_length = 0;
                                for (i = 1; i <= sar_total_segments; i++)
                                {
                                    Array.Copy(((byte[])tArr[i]), 0, _short_message, _sm_length, ((byte[])tArr[i]).Length);
                                    _sm_length += ((byte[])tArr[i]).Length;
                                }
                                sarMessages.Remove(sar_msg_ref_num);
                            }
                        }//if
                        else
                        {
                            tArr.Add(sar_segment_seqnum, _short_message);
                            sarMessages.Add(sar_msg_ref_num, tArr);
                            sendDeliverSmResp(sequence_number, StatusCodes.ESME_ROK);
                            return;
                        }
                    }//lock
                }
                string to;
                string from;
                string textString;

                string hexString = "";

                byte messageState = 0;
                string receiptedMessageID = "";

                to = Encoding.ASCII.GetString(_dest_addr, 0, daLength);
                from = Encoding.ASCII.GetString(_source_addr, 0, saLength);
                if (_data_coding == 8) //USC2
                    textString = Encoding.BigEndianUnicode.GetString(_short_message, 0, _sm_length);
                else
                    textString = Encoding.UTF8.GetString(_short_message, 0, _sm_length);
                textString = textString
                    .Replace("\0", "");
                hexString = Utility.ConvertArrayToHexString(_short_message, _sm_length);


                
                DateTime? sd_dt = null, dd_dt = null;
                if (isDeliveryReceipt)
                {
                    isDeliveryReceipt = true;
                    try
                    {
                        if (_message_state == 255 || _receipted_message_id_len == 0)
                            throw new InvalidDataException("Invalid Message State or message id");
                        messageState = _message_state;
                        if (_receipted_message_id_len > 0)
                            receiptedMessageID = Encoding.ASCII.GetString(_receipted_message_id, 0, _receipted_message_id_len - 1); ;
                    }
                    catch (Exception ex)
                    {
                        Dictionary<string, string> dictionaryText = Utility.ParseDeliveryMessageText(textString);
                        messageState = Utility.MessageDeliveryStatus(dictionaryText["stat"]);
                        if (dictionaryText.ContainsKey("id"))
                            receiptedMessageID = dictionaryText["id"];
                    }

                    

                    //if (!ReferenceEquals(tlvList, null))
                    //{
                    //    tlvList.TryGetValue("id", out receiptedMessageID);
                    //    receiptedMessageID = receiptedMessageID.Replace(" ", "");
                    //    //receiptedMessageID = Convert.ToInt32(receiptedMessageID, 16).ToString();
                    //}
                    String dd = String.Empty, sd = String.Empty;
                    if (!ReferenceEquals(shortMessageValueList, null))
                    {
                        try
                        {
                            if (String.IsNullOrEmpty(receiptedMessageID))
                            {
                                if (shortMessageValueList.ContainsKey("id"))
                                {
                                    receiptedMessageID = shortMessageValueList["id"];
                                }
                                else if (shortMessageValueList.ContainsKey("oid"))
                                {
                                    receiptedMessageID = shortMessageValueList["oid"];
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | Error parsing message id" + ex.ToString());
                        }


                        try
                        {
                            if (shortMessageValueList.ContainsKey("submit date"))
                            {
                                string submitDate = shortMessageValueList["submit date"];
                                sd_dt = DateTime.ParseExact(submitDate, DeliverySmDateFormat, System.Globalization.CultureInfo.InvariantCulture);
                                //sd = sd_dt.ToString("dd-MMM-yyyy HH:mm:ss");
                            }
                        }
                        catch (Exception ex)
                        {
                            logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | Error parsing submit date" + ex.ToString());
                        }

                        try
                        {
                            if (shortMessageValueList.ContainsKey("done date"))
                            {
                                string doneDate = shortMessageValueList["done date"];
                                dd_dt = DateTime.ParseExact(doneDate, DeliverySmDateFormat, System.Globalization.CultureInfo.InvariantCulture);
                                //dd = dd_dt.ToString("dd-MMM-yyyy HH:mm:ss");
                            }
                        }
                        catch (Exception ex)
                        {
                            logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | Error parsing done date" + ex.ToString());
                        }
                    }
                    #region [ Update Data ]
                    //string sql = String.Format(
                    //    @"INSERT INTO [DeliveryReports] ([MessageId], [SubmitDate], [DoneDate], [Status])
                    //            VALUES ('{0}', '{1}', '{2}', '{3}')"
                    //    , receiptedMessageID
                    //    , sd
                    //    , dd
                    //    , Utility.MessageDeliveryStatus(messageState) //, tlvList["stat"]
                    //);
                    //LocalStorage.ExecuteNonQuery(sql);
                    #endregion
                }
                DeliverSmEventArgs evArg = new DeliverSmEventArgs(sequence_number, to, from, textString, hexString, _data_coding, _esm_class, isDeliveryReceipt, messageState, sd_dt, dd_dt, receiptedMessageID);
                processDeliverSm(evArg);
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | " + ex.ToString());
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | PDU |" + BitConverter.ToString(_body));
                //sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
            }

        }//decodeAndProcessDeliverSm

        #endregion

        private void decodeAndProcessDataSm(uint sequence_number, byte[] _body, int _length)
        {
            if (_length < 17)
            {
                sendDeliverSmResp(sequence_number, StatusCodes.ESME_RINVCMDLEN);
                return;
            }
            try
            {
                bool isDeliveryReceipt = false;
                byte _source_addr_ton, _source_addr_npi, _dest_addr_ton, _dest_addr_npi;
                byte _priority_flag, _data_coding, _esm_class;
                byte[] _source_addr = new byte[21];
                byte[] _dest_addr = new byte[21];
                byte saLength, daLength;
                byte[] _short_message = new byte[254];
                int pos;
                ////
                /// For Body Print
                /////////////////////////////////////
                /// Message Delivery Params
                /// 
                byte[] _receipted_message_id = new byte[254];
                byte _receipted_message_id_len = 0;
                byte _message_state = 0;


                pos = 0;
                while ((pos < 5) && (_body[pos] != 0x00))
                    pos++;
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x04");
                    return;
                }
                _source_addr_ton = _body[++pos];
                _source_addr_npi = _body[++pos];
                pos++;
                saLength = 0;
                while ((saLength < 20) && (_body[pos] != 0x00))
                {
                    _source_addr[saLength] = _body[pos];
                    pos++;
                    saLength++;
                }
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x05");
                    return;
                }
                _dest_addr_ton = _body[++pos];
                _dest_addr_npi = _body[++pos];
                pos++;
                daLength = 0;
                while ((daLength < 20) && (_body[pos] != 0x00))
                {
                    _dest_addr[daLength] = _body[pos];
                    pos++;
                    daLength++;
                }
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x06");
                    return;
                }
                _esm_class = _body[++pos];
                switch (_esm_class)
                {
                    case 0x00:
                        break;
                    case 0x04:
                        //logMessage(LogLevels.LogSteps, "Delivery Receipt Received");
                        isDeliveryReceipt = true;
                        break;
                    default:
                        logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "Unknown esm_class for DATA_SM : " + Utility.GetHexFromByte(_esm_class));
                        break;
                }
                pos += 1;
                _priority_flag = _body[++pos];
                pos += 4;
                _data_coding = _body[++pos];
                pos += 1;
                if (isDeliveryReceipt)
                {
                    int _par_tag, _par_tag_length;
                    bool exit = false;
                    while ((pos < _length) && (exit == false))
                    {
                        if (Utility.Get2ByteIntFromArray(_body, pos, _length, out _par_tag) == false)
                        {
                            exit = true;
                            break;
                        }
                        pos += 2;
                        if (Utility.Get2ByteIntFromArray(_body, pos, _length, out _par_tag_length) == false)
                        {
                            exit = true;
                            break;
                        }
                        pos += 2;
                        switch (_par_tag)
                        {
                            case 0x0427:
                                if ((pos <= _length) && (_par_tag_length == 1))
                                {
                                    _message_state = _body[pos];
                                    //logMessage(LogLevels.LogSteps, "Message state : " + Convert.ToString(_message_state));
                                    pos++;
                                }
                                else
                                    exit = true;

                                break;
                            case 0x001E:
                                if ((pos + _par_tag_length - 1) <= _length)
                                {
                                    _receipted_message_id = new byte[_par_tag_length];
                                    Array.Copy(_body, pos, _receipted_message_id, 0, _par_tag_length);
                                    _receipted_message_id_len = Convert.ToByte(_par_tag_length);
                                    pos += _par_tag_length;
                                    //logMessage(LogLevels.LogSteps, "Delivered message id : " + Utility.ConvertArrayToString(_receipted_message_id, _receipted_message_id_len - 1));
                                }
                                else
                                    exit = true;
                                break;
                            default:
                                if ((pos + _par_tag_length - 1) <= _length)
                                    pos += _par_tag_length;
                                else
                                    exit = true;
                                logMessage(LogLevels.LogInfo, "_par_tag : " + Convert.ToString(_par_tag));
                                logMessage(LogLevels.LogInfo, "_par_tag_length : " + Convert.ToString(_par_tag_length));
                                break;

                        }
                    }
                    //logMessage(LogLevels.LogDebug, "Delivery Receipt Processing Exit value - " + Convert.ToString(exit));
                    if (exit)
                        isDeliveryReceipt = false;
                }

                string to;
                string from;
                string textString = "";

                string hexString = "";

                byte messageState = 0;
                string receiptedMessageID = "";

                to = Encoding.ASCII.GetString(_dest_addr, 0, daLength);
                from = Encoding.ASCII.GetString(_source_addr, 0, saLength);

                if (isDeliveryReceipt)
                {
                    isDeliveryReceipt = true;
                    messageState = _message_state;
                    receiptedMessageID = Encoding.ASCII.GetString(_receipted_message_id, 0, _receipted_message_id_len - 1); ;
                }

                DeliverSmEventArgs evArg = new DeliverSmEventArgs(sequence_number, to, from, textString, hexString, _data_coding, _esm_class, isDeliveryReceipt, messageState, receiptedMessageID);
                processDeliverSm(evArg);
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | " + ex.ToString());
                sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
            }

        }//decodeAndProcessDataSm

        #region [ Unbind ]
        public void unBind()
        {
            if (connectionState == ConnectionStates.SMPP_BINDED || connectionState == ConnectionStates.SMPP_UNBIND_PENDING)
            {
                try
                {
                    byte[] _PDU = new byte[16];

                    Utility.CopyIntToArray(16, _PDU, 0);

                    Utility.CopyIntToArray(Command.UNBIND, _PDU, 4);

                    Utility.CopyIntToArray(MC.SequenceNumber, _PDU, 12);

                    logMessage(LogLevels.LogSteps, "Unbind sent.");
                    connectionState = ConnectionStates.SMPP_UNBIND_SENT;

                    Send(_PDU, 16);


                    if (OnUnbind != null)
                    {
                        SmppEventArgs args = new SmppEventArgs(_PDU, 16);
                        //OnUnbind.BeginInvoke(this, onServerUnbindEventComplete, args.Sequence);
                        Task.Run(() => this.OnUnbind.Invoke(this))
                            .ContinueWith(task => onServerUnbindEventComplete(task, args.Sequence));
                        //this.OnUnbind(this);
                        args.Dispose();
                    }
                }
                catch (ObjectDisposedException ex)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logMessage(LogLevels.LogExceptions, "unBind | " + ex.ToString());
                }
            }

        }//unBind

        private void onServerUnbindEventComplete(Task task, uint args)
        {
            try
            {
                connectionState = ConnectionStates.SMPP_UNBINDED;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "onServerUnbindEventComplete | " + ex.ToString());
            }
        }
        //private void onServerUnbindEventComplete(IAsyncResult iar)
        //{
        //    var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //    var invokedMethod = (UnbindEventHandler)ar.AsyncDelegate;
        //    var args = (uint)iar.AsyncState;

        //    try
        //    {
        //        invokedMethod.EndInvoke(iar);
        //        connectionState = ConnectionStates.SMPP_UNBINDED;
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "onServerUnbindEventComplete | " + ex.ToString());
        //    }
        //}
        #endregion

        #region [ Unbind Response ]
        private void decodeAndProcessUnbindResp(SmppEventArgs args)
        {
            try
            {
                logMessage(LogLevels.LogSteps, String.Format("Unbind complete, terminated Connection {0} sucessfully :: Instance {1}", ReferenceEquals(this.Identifier, null) ? new Guid() : ((SmppSession)this.Identifier).Id, ConnectionNumber));

                disconnectTokenSource.Cancel();

                args.Dispose();
                this.Dispose();
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "decodeAndProcessUnbindResp | " + ex.ToString());
            }
        }
        #endregion

        #region [ Unbind from Client ]

        //private void decodeAndProcessUnbindTransceiver(uint _sequence_number)
        private void decodeAndProcessUnbind(SmppEventArgs args)
        {
            if (connectionState == ConnectionStates.SMPP_BINDED)
            {
                connectionState = ConnectionStates.SMPP_UNBIND_SENT;
                if (OnUnbind != null)
                {
                    //OnUnbind.BeginInvoke(this, onClientUnbindEventComplete, args.Sequence);
                    Task.Run<uint>(() => OnUnbind.Invoke(this))
                        .ContinueWith(task => onClientUnbindEventComplete(task, args.Sequence));
                }
                else
                {
                    args.Dispose();
                }
            }
            else
            {
                args.Dispose();
            }
        }

        private void onClientUnbindEventComplete(Task<uint> task, uint args)
        {
            try
            {
                uint _command_status = task.Result;

                sendUnbindResp(args, _command_status);

                if (_command_status == StatusCodes.ESME_ROK)
                {
                    connectionState = ConnectionStates.SMPP_UNBINDED;
                    logMessage(LogLevels.LogSteps, String.Format("Unbind complete, terminated Connection {0} sucessfully :: Instance {1}", ReferenceEquals(this.Identifier, null) ? new Guid() : ((SmppSession)this.Identifier).Id, ConnectionNumber));
                }
                else
                {
                    logMessage(LogLevels.LogSteps, String.Format("Unbind complete, terminated Connection {0} with error 0x{2:X} :: Instance {1}", ReferenceEquals(this.Identifier, null) ? new Guid() : ((SmppSession)this.Identifier).Id, ConnectionNumber, _command_status));
                }
                this.Dispose();
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                sendSubmitSmResp(args, StatusCodes.ESME_RSYSERR, String.Empty);

                logMessage(LogLevels.LogExceptions, "onClientUnbindEventComplete | " + ex.ToString());
                this.Dispose();
            }
        }
        //private void onClientUnbindEventComplete(IAsyncResult iar)
        //{
        //    var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //    var invokedMethod = (UnbindEventHandler)ar.AsyncDelegate;
        //    var args = (uint)iar.AsyncState;

        //    try
        //    {
        //        uint _command_status = invokedMethod.EndInvoke(iar);

        //        sendUnbindResp(args, _command_status);

        //        if (_command_status == StatusCodes.ESME_ROK)
        //        {
        //            connectionState = ConnectionStates.SMPP_UNBINDED;
        //            logMessage(LogLevels.LogSteps, String.Format("Unbind complete, terminated Connection {0} sucessfully :: Instance {1}", ReferenceEquals(this.Identifier, null) ? new Guid() : ((SmppSession)this.Identifier).Id, ConnectionNumber));
        //        }
        //        else
        //        {
        //            logMessage(LogLevels.LogSteps, String.Format("Unbind complete, terminated Connection {0} with error 0x{2:X} :: Instance {1}", ReferenceEquals(this.Identifier, null) ? new Guid() : ((SmppSession)this.Identifier).Id, ConnectionNumber, _command_status));
        //        }
        //        this.Dispose();
        //    }
        //    catch (ObjectDisposedException ex)
        //    {
        //        return;
        //    }
        //    catch (Exception ex)
        //    {
        //        sendSubmitSmResp(args, StatusCodes.ESME_RSYSERR, String.Empty);

        //        logMessage(LogLevels.LogExceptions, "onClientUnbindEventComplete | " + ex.ToString());
        //        this.Dispose();
        //    }
        //}

        private void sendUnbindResp(uint sequence_number, uint command_status)
        {
            try
            {
                byte[] _PDU = new byte[16];

                Utility.CopyIntToArray(16, _PDU, 0);

                Utility.CopyIntToArray(Command.UNBIND_RESP, _PDU, 4);

                Utility.CopyIntToArray(command_status, _PDU, 8);

                Utility.CopyIntToArray(sequence_number, _PDU, 12);

                Send(_PDU, 16);
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendUnbindResp | " + ex.ToString());
            }
        }
        #endregion

        #region [ TLV Params ]
        private List<OptionalParameter> decodeTlvParams(byte[] _body, int pos = 0)
        {
            List<OptionalParameter> tlvList = new List<OptionalParameter>();

            //int pos = 0;
            ushort _par_tag, _par_tag_length;

            while (pos < _body.Length)
            {
                try
                {
                    _par_tag = (ushort)(_body[pos++] << 8);
                    _par_tag |= _body[pos++];

                    _par_tag_length = (ushort)(_body[pos++] << 8);
                    _par_tag_length |= _body[pos++];

                    //TlvParam param = new TlvParam(_par_tag);
                    ////param.Tag = _par_tag;
                    ////param.Length = _par_tag_length;

                    int min_length = 0;
                    int max_length = ushort.MaxValue;

                    switch (_par_tag)
                    {
                        case TagCodes.ADDITIONAL_STATUS_INFO_TEXT:
                            min_length = 1;
                            max_length = 256;
                            break;
                        case TagCodes.ALERT_ON_MESSAGE_DELIVERY:
                            min_length = 0;
                            max_length = 1;
                            break;
                        case TagCodes.BILLING_IDENTIFICATION:
                            min_length = 1;
                            max_length = 1024;
                            break;
                        case TagCodes.BROADCAST_AREA_IDENTIFIER:
                            //min_length = 1;
                            //max_length = 1024;
                            break;
                        case TagCodes.BROADCAST_AREA_SUCCESS:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.BROADCAST_CONTENT_TYPE_INFO:
                            min_length = 1;
                            max_length = 255;
                            break;
                        case TagCodes.BROADCAST_CHANNEL_INDICATOR:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.BROADCAST_CONTENT_TYPE:
                            min_length = 3;
                            max_length = 3;
                            break;
                        case TagCodes.BROADCAST_END_TIME:
                            min_length = 16;
                            max_length = 16;
                            break;
                        case TagCodes.BROADCAST_ERROR_STATUS:
                            min_length = 4;
                            max_length = 4;
                            break;
                        case TagCodes.BROADCAST_FREQUENCY_INTERVAL:
                            min_length = 3;
                            max_length = 3;
                            break;
                        case TagCodes.BROADCAST_MESSAGE_CLASS:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.BROADCAST_REP_NUM:
                            min_length = 2;
                            max_length = 2;
                            break;
                        case TagCodes.BROADCAST_SERVICE_GROUP:
                            min_length = 1;
                            max_length = 255;
                            break;
                        case TagCodes.CALLBACK_NUM:
                            min_length = 4;
                            max_length = 19;
                            break;
                        case TagCodes.CALLBACK_NUM_ATAG:
                            min_length = 1;
                            max_length = 65;
                            break;
                        case TagCodes.CALLBACK_NUM_PRES_IND:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.CONGESTION_STATE:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.DELIVERY_FAILURE_REASON:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.DEST_ADDR_NP_COUNTRY:
                            min_length = 1;
                            max_length = 5;
                            break;
                        case TagCodes.DEST_ADDR_NP_INFORMATION:
                            min_length = 10;
                            max_length = 10;
                            break;
                        case TagCodes.DEST_ADDR_NP_RESOLUTION:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.DEST_ADDR_SUBUNIT:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.DEST_BEARER_TYPE:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.DEST_NETWORK_ID:
                            min_length = 7;
                            max_length = 65;
                            break;
                        case TagCodes.DEST_NETWORK_TYPE:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.DEST_NODE_ID:
                            min_length = 6;
                            max_length = 6;
                            break;
                        case TagCodes.DEST_SUBADDRESS:
                            min_length = 2;
                            max_length = 23;
                            break;
                        case TagCodes.DEST_TELEMATICS_ID:
                            min_length = 2;
                            max_length = 2;
                            break;
                        case TagCodes.DEST_PORT:
                            min_length = 2;
                            max_length = 2;
                            break;
                        case TagCodes.DISPLAY_TIME:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.DPF_RESULT:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.ITS_REPLY_TYPE:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.ITS_SESSION_INFO:
                            min_length = 2;
                            max_length = 2;
                            break;
                        case TagCodes.LANGUAGE_INDICATOR:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.MESSAGE_PAYLOAD:
                            //min_length = 3;
                            //max_length = 3;
                            break;
                        case TagCodes.MESSAGE_STATE:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.MORE_MESSAGES_TO_SEND:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.MS_AVAILABILITY_STATUS:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.MS_MSG_WAIT_FACILITIES:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.MS_VALIDITY:
                            min_length = 3;
                            max_length = 3;
                            break;
                        case TagCodes.NETWORK_ERROR_CODE:
                            min_length = 3;
                            max_length = 3;
                            break;
                        case TagCodes.NUMBER_OF_MESSAGES:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.PAYLOAD_TYPE:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.PRIVACY_INDICATOR:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.QOS_TIME_TO_LIVE:
                            min_length = 4;
                            max_length = 4;
                            break;
                        case TagCodes.RECEIPTED_MESSAGE_ID:
                            min_length = 1;
                            max_length = 65;
                            break;
                        case TagCodes.SAR_MSG_REF_NUM:
                            min_length = 2;
                            max_length = 2;
                            break;
                        case TagCodes.SAR_SEGMENT_SEQNUM:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.SAR_TOTAL_SEGMENTS:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.SC_INTERFACE_VERSION:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.SET_DPF:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.SMS_SIGNAL:
                            min_length = 2;
                            max_length = 2;
                            break;
                        case TagCodes.SOURCE_ADDR_SUBUNIT:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.SOURCE_BEARER_TYPE:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.SOURCE_NETWORK_ID:
                            min_length = 7;
                            max_length = 65;
                            break;
                        case TagCodes.SOURCE_NETWORK_TYPE:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.SOURCE_NODE_ID:
                            min_length = 6;
                            max_length = 6;
                            break;
                        case TagCodes.SOURCE_PORT:
                            min_length = 2;
                            max_length = 2;
                            break;
                        case TagCodes.SOURCE_SUBADDRESS:
                            min_length = 2;
                            max_length = 23;
                            break;
                        case TagCodes.SOURCE_TELEMATICS_ID:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.USER_MESSAGE_REFERENCE:
                            min_length = 2;
                            max_length = 2;
                            break;
                        case TagCodes.USER_RESPONSE_CODE:
                            min_length = 1;
                            max_length = 1;
                            break;
                        case TagCodes.USSD_SERVICE_OP:
                            min_length = 1;
                            max_length = 1;
                            break;
                        default:
                            break;
                    }


                    byte[] value;
                    int index = 0;

                    if (min_length == 0 && _par_tag_length == 0)
                        value = new byte[0];
                    else if (_par_tag_length == 0)
                        throw new TlvException(StatusCodes.ESME_RINVOPTPARAMVAL);
                    else if (_par_tag_length > max_length)
                        throw new TlvException(StatusCodes.ESME_RINVOPTPARAMVAL);
                    else if (_par_tag_length < min_length || _par_tag_length > max_length)
                        throw new TlvException(StatusCodes.ESME_RINVOPTPARAMVAL);
                    else
                    {
                        int length = Math.Min(max_length, _par_tag_length);

                        value = new byte[length];

                        while (index < length)
                            value[index++] = _body[pos++];
                    }
                    tlvList.Add(new OptionalParameter(_par_tag, value));
                }
                catch (IndexOutOfRangeException ex)
                {
                    break;
                }
            }
            return tlvList;
        }

        private byte[] encodeTlvParams(List<OptionalParameter> optionalParameters)
        {
            byte[] _body = new byte[KernelParameters.MaxPduSize];
            int pos = 0;

            //for (int index = 0; index < optionalParameters.Count; index++)
            foreach (OptionalParameter op in optionalParameters)
            {
                //byte[] _tag = new byte[2];
                //byte[] _length = new byte[2];

                //Utility.ConvertShortToArray((short)op.Tag, out _tag);
                //Utility.ConvertShortToArray((short)op.Value.Length, out _length);

                //Array.Copy(_tag, 0, _body, pos, 2);
                //pos += 2;

                //Array.Copy(_length, 0, _body, pos, 2);
                //pos += 2;

                Utility.CopyShortToArray((short)op.Tag, _body, pos);
                pos += 2;
                Utility.CopyShortToArray((short)op.Value.Length, _body, pos);
                pos += 2;

                Array.Copy(op.Value, 0, _body, pos, op.Length);
                pos += op.Length;
                //pos++;
            }
            //pos--;

            return _body.Take(pos).ToArray();
        }

        #endregion

        #region [ Garbage Collection ]
        protected void GarbageCollect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        #endregion

        private void CleanMessageParts(int timeout)
        {
            try
            {
                logMessage(LogLevels.LogSteps, "CleanMessageParts | Cleaning incomplete messages");

                List<ulong> keysToRemove = new List<ulong>();
                lock (this.MessageBuilder.SyncRoot)
                {
                    keysToRemove = this.MessageBuilderHash
                        .Where(x => x.Value < DateTime.Now.AddSeconds(-timeout))
                        .Select(x => x.Key)
                        .ToList();
                }

                logMessage(LogLevels.LogSteps, String.Format("CleanMessageParts | Found {0} items to clean", keysToRemove.Count));

                foreach (var key in keysToRemove)
                {
                    logMessage(LogLevels.LogSteps, String.Format("CleanMessageParts | Removing key - {0}", key));

                    lock (this.MessageBuilder.SyncRoot)
                    {
                        if (this.MessageBuilder.ContainsKey(key))
                            this.MessageBuilder.Remove(key);

                        //if (!ReferenceEquals(this.MessageBuilderHash[key], null))
                        if (this.MessageBuilderHash.ContainsKey(key))
                            this.MessageBuilderHash.Remove(key);
                    }

                    logMessage(LogLevels.LogSteps, String.Format("CleanMessageParts | Removed key - {0}", key));
                }
            }
            catch (InvalidOperationException ex)
            {
                logMessage(LogLevels.LogSteps, "CleanMessageParts | Locked");
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "CleanMessageParts | " + ex.ToString());
            }
        }
        #endregion

        #region [ Dispose ]
        public void Dispose()
        {
            try
            {
                SessionEventArgs e = new SessionEventArgs
                {
                    Id = ((SmppSession)this.Identifier).Id,
                    //Address = clientSocket.Client.RemoteEndPoint.ToString(),
                };

                this.OnClientSessionStart = null;
                this.OnSent = null;
                this.OnRecieved = null;
                this.OnClientBind = null;
                this.OnUnbind = null;
                this.OnSubmitSm = null;
                this.OnDeliverSm = null;
                this.OnSubmitSmResp = null;

                //clientSocket.Close();
                disconnectSocket();
                if (clientSocket.Connected)
                {
                    clientSocket.GetStream().Close();
                }
                clientSocket.Close();

                if (!ReferenceEquals(sslStream, null) && (sslStream.CanRead || sslStream.CanWrite))
                {
                    sslStream.Close();
                    sslStream.Dispose();
                }

                this.sarMessages = null;
                this.mbResponse = null;
                this.PendingDelivery = null;
                this.DeliveryQueue = null;

                if (OnSessionEnd != null)
                {
                    OnSessionEnd(this, e);
                    //OnSessionEnd.BeginInvoke(this, e, onSessionEndEventComplete, e);
                }
                //this.Identifier = null;
                //this.esme = null;
                //this.enquireLinkTimer = null;
                //this.deliveryTimer = null;
                //this.deliveryReportTimer = null;

                this.OnSessionEnd = null;
                Instance--;
            }
            catch (ObjectDisposedException ex)
            {
            }
            catch (Exception ex)
            {

            }
            GarbageCollect();
        }

        //private void onSessionEndEventComplete(IAsyncResult iar)
        //{
        //    var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
        //    var invokedMethod = (SessionTerminatedHandler)ar.AsyncDelegate;
        //    var args = (SessionEventArgs)iar.AsyncState;

        //    try
        //    {
        //        invokedMethod.EndInvoke(iar);
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "onSentCallbackComplete | " + ex.ToString());
        //    }
        //}
        #endregion

        public async Task<SmppConnectionStatistic> GetStatistics()
        {
            SmppSession smppSession = ((SmppSession)this.Identifier);

            SmppConnectionStatistic s = new SmppConnectionStatistic();
            s.Instance = this.ConnectionNumber;
            s.ConnectionType = this.ConnectionType.ToString();
            s.Id = this.SessionId;
            s.QueuedDeliveries = this.DeliveryQueue.Count();
            s.QueuedDeliveriesMemorySize = await Utility.GetMemorySizeAsync(this.DeliveryQueue);

            s.PendingDeliveries = this.PendingDelivery.Count;
            s.PendingDeliveriesMemorySize = await Utility.GetMemorySizeAsync(this.PendingDelivery);

            s.StartTime = ReferenceEquals(smppSession, null) ? null : (smppSession.ValidForm > DateTime.Now ? null : (DateTime?)smppSession.ValidForm);
            s.EndTime = ReferenceEquals(smppSession, null) ? null : (smppSession.ValidTo > DateTime.Now ? null : (DateTime?)smppSession.ValidTo);
            s.StartTime = ReferenceEquals(smppSession, null) ? null : (smppSession.ValidForm > DateTime.Now ? null : (DateTime?)smppSession.ValidForm);
            s.EndTime = ReferenceEquals(smppSession, null) ? null : (smppSession.ValidTo > DateTime.Now ? null : (DateTime?)smppSession.ValidTo);

            s.PendingMessageParts = this.MessageBuilder.Count;
            s.PendingMessagePartsMemorySize = await Utility.GetMemorySizeAsync(this.MessageBuilder);


            s.TotalMemory = await Utility.GetMemorySizeAsync(this);
            return s;
        }
    }
}
