using SMSGateway.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using SMSGateway.DataManager;
using SMSGateway.Entity;
using SMSGateway.SMPPClient;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using MySqlX.XDevAPI.Relational;
using MySqlX.XDevAPI;

namespace SMSGateway.SMSCClient
{
    public class SmppServer : IDisposable
    {

        #region Private variables
        //private int port;
        private TcpListener listener;
        private bool active;

        AsyncCallback onAcceptConnection;
        private readonly SMSC smsc;
        //private int sequence;
        private static object deliveryLoaderLock = new object();
        private int logLevel;
        private int deliveryMaxRetry = 4;
        private static ConcurrentDictionary<ulong, uint> userTps = new ConcurrentDictionary<ulong, uint>();

        #endregion Private variables

        #region [ Connection Manager ]
        private static List<SMPPConnection> connections;
        private System.Timers.Timer connectionManager;
        private System.Timers.Timer tpsManager;
        private static System.Timers.Timer userBalanceManager;
        private static ConcurrentDictionary<long, decimal> userBalance;

        #region[ TPS Manager - Elapsed ]
        private void TpsManager_Elapsed(object? sender, ElapsedEventArgs e)
        {
            //if (!Monitor.TryEnter(tpsManager))
            //    return;

            try
            {
                ulong[] userTpsList = userTps.Keys.ToArray();
                for (int i = 0; i < userTpsList.Length; i++)
                    userTps[userTpsList[i]] = 0;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "TpsManager_Elapsed | " + ex.ToString());
            }
            finally
            {
                //Monitor.Exit(connectionManager);
            }
        }

        #endregion

        #region[ Connection Manager - Elapsed ]

        private void ConnectionManager_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!Monitor.TryEnter(connectionManager))
                return;

            try
            {
                //List<int> validConnectionStates = new List<int> {
                //    ConnectionStates.SMPP_SOCKET_CONNECT_SENT,
                //    ConnectionStates.SMPP_SOCKET_CONNECTED,
                //    ConnectionStates.SMPP_BIND_SENT,
                //    ConnectionStates.SMPP_BINDED,
                //    ConnectionStates.SMPP_UNBIND_SENT,
                //    ConnectionStates.SMPP_UNBINDED,
                //    //ConnectionStates.SMPP_SOCKET_DISCONNECTED
                //};

                //List<SMPPConnection> closedConnections = connections
                //    .Where(x => !validConnectionStates.Contains(x.ConnectionState))
                //    .ToList();

                //for (int i = closedConnections.Count; i > 0; i--)
                //{
                //    int index = connections.IndexOf(closedConnections[i]);
                //    //closedConnections[i].Dispose();
                //    connections.RemoveAt(index);
                //}
                
                while (true)
                {
                    var cx = Connections
                            .Where(x => ReferenceEquals(x, null)
                                || x.ConnectionState == ConnectionStates.SMPP_UNBIND_SENT
                                || x.ConnectionState == ConnectionStates.SMPP_SOCKET_DISCONNECTED
                                || x.LastConnected.AddMilliseconds(x.EnquireLinkTimeout * 10) < DateTime.Now
                            );

                    if (!cx.Any())
                        break;

                    SMPPConnection? c = cx.FirstOrDefault();

                    if (c == null)
                        Connections.Remove(c);
                    else
                    {
                        Disconnect(c);
                        Connections.Remove(c);
                    }
                }
                //for (int index = Connections.Count - 1; active && index > 0; index--)
                //{
                //    if (ReferenceEquals(connections[index], null))
                //    {
                //        Connections.RemoveAt(index);
                //    }
                //    else if (Connections[index].ConnectionState == ConnectionStates.SMPP_SOCKET_DISCONNECTED)
                //    {
                //        //Connections[index].Disconnect();
                //        //Connections[index].Dispose();
                //        Disconnect(Connections[index]);
                //        Connections.RemoveAt(index);
                //    }
                //    else if (Connections[index].LastConnected.AddMilliseconds(Connections[index].EnquireLinkTimeout * 10) < DateTime.Now)
                //    {
                //        //Connections[index].Disconnect();
                //        //Connections[index].Dispose();
                //        Disconnect(Connections[index]);
                //        Connections.RemoveAt(index);
                //    }
                //}

                GC.Collect();
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "ConnectionManager_Elapsed | " + ex.ToString());
            }
            finally
            {
                Monitor.Exit(connectionManager);
            }
        }
        #endregion

        #region [ User Balance Manager  - Elapsed ]
        private static void UserBalanceManager_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try {
                new SmppServerManager().UpdateUserBalance().Wait();

                Dictionary<long, decimal> balance = new SmppServerManager()
                    .GetUserBalance(userBalance.Keys.ToArray()).Result;

                long[] keys = balance.Keys.ToArray();
                for (int i = 0; i < keys.Length; i++)
                {
                    if (userBalance.ContainsKey(keys[i]))
                        userBalance[keys[i]] = balance[keys[i]];
                    else
                        userBalance.TryAdd(keys[i], balance[keys[i]]);
                }
            }
            catch(Exception ex)
            {
                
            }
        }
        #endregion

        private static object connectionLock = new object();
        private static List<SMPPConnection> Connections
        {
            get
            {
                lock (connectionLock)
                {
                    return connections;
                }
            }
            set
            {
                lock (connectionLock)
                {
                    connections = value;
                }
            }
        }
        #endregion

        #region Public Functions
        static SmppServer()
        {
            userBalance = new ConcurrentDictionary<long, decimal>();
            connections = new List<SMPPConnection>();

            userBalanceManager = new System.Timers.Timer(10000);
            userBalanceManager.Elapsed += UserBalanceManager_Elapsed;
            userBalanceManager.Start();
        }

        public SmppServer(SMSC smsc)
        {
            this.smsc = smsc;
            connections = new List<SMPPConnection>();

            connectionManager = new System.Timers.Timer(KernelParameters.EnquireLinkTimeout * 2);
            connectionManager.Elapsed += ConnectionManager_Elapsed;


            tpsManager = new System.Timers.Timer(1000);
            tpsManager.Elapsed += TpsManager_Elapsed;
            tpsManager.Start();
            //this.OnDelivery += SMPPServer_OnDelivery;

            //deliveryManager = new System.Timers.Timer(100);
            //deliveryManager.Elapsed += DeliveryManager_Elapsed;
            //deliveryManager.Start();

            onAcceptConnection = new AsyncCallback(OnAcceptTcpClient);

            //if (Int32.TryParse(ConfigurationManager.AppSettings["LogLevel"], out logLevel) == false)
            //{
            //    logLevel = LogLevels.LogWarnings;
            //}

        }

        

        public SmppServer(SMSC smsc, int deliveryRetry)
            : this(smsc)
        {
            deliveryMaxRetry = deliveryRetry;

        }
        #region [ Start / Stop ]

        public void Start()
        {
            active = true;

            listener = new TcpListener(IPAddress.Any, this.smsc.Port);
            listener.Start();

            Thread th = new Thread(new ThreadStart(StartListen));
            th.Start();

            connectionManager.Start();
        }

        public void Stop()
        {
            try
            {
                active = false;
                //listener.Server.Shutdown(SocketShutdown.Both);
                //listener.Server.Close();
                listener.Stop();

                connectionManager.Stop();
                List<Task> closureTasks = new List<Task>();
                //for (int index = Connections.Count - 1; index >= 0; index--)
                for (int index = 0; index < Connections.Count; index++)
                {
                    if (!ReferenceEquals(Connections[index], null))
                    {
                        //Connections[index].Disconnect();
                        //Connections[index].Dispose();
                        Task t = Disconnect(Connections[index]);
                        closureTasks.Add(t);
                        //Connections.RemoveAt(index);
                    }
                }
                Task.WaitAll(closureTasks.ToArray(), 30000);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "Smpp Server Stop | " + ex.ToString());
            }
        }

        private async Task Disconnect(SMPPConnection c)
        {
            //return Task.Run(() =>
            //{
            //    c.Disconnect();
            //    c.Dispose();
            //}).ContinueWith((task) =>
            //{

            //    if (task.IsFaulted)
            //    {
            //        logMessage(LogLevels.LogExceptions, task.Exception.ToString());
            //    }
            //});


            await Task.Run(async () =>
            {
                c.Disconnect();
                c.Dispose();

            })
            .ContinueWith((task) =>
            {
                if (task.IsFaulted)
                {
                    logMessage(LogLevels.LogExceptions, "Disconnect Thread | " + task.Exception.ToString());
                }
            });

            //Action<object> action = async (object o) =>
            //{
            //    SMPPConnection sc = (SMPPConnection)o;
            //    //try
            //    //{
            //    sc.Disconnect();
            //    sc.Dispose();
            //    //}
            //    //catch (Exception ex) {
            //    //    sc.logMessage(LogLevels.LogExceptions, ex.ToString());
            //    //}
            //};

            //Task t = new Task(action, c);
            //t.ContinueWith((task) =>
            //{
            //    if (task.IsFaulted)
            //    {
            //        logMessage(LogLevels.LogExceptions, "Disconnect Thread | " + task.Exception.ToString());
            //        //throw task.Exception;
            //    }
            //    //if (task.IsCompleted)
            //    //{
            //    //    logMessage(LogLevels.LogInfo, "Closure task completed");
            //    //}
            //});
            //t.Start();
            //return t;
        }

        #region [ Dispose ]
        public void Dispose()
        {
            //Monitor.Enter(this);
            try
            {
                connectionManager.Stop();
                for (int index = connections.Count - 1; index >= 0; index--)
                {
                    if (!ReferenceEquals(connections[index], null))
                    {
                        try
                        {
                            connections[index].Disconnect();
                            connections[index].Dispose();
                        }
                        catch { }
                        connections.RemoveAt(index);
                    }
                }
                tpsManager.Stop();
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "SmppServer Dispose | " + ex.ToString());
            }
            //finally
            //{
            //    Monitor.Exit(this);
            //}
        }

        #endregion
        #endregion

        //public void SendDeliveryReport(string systemId, string from, string to, string message, string messageId, DeliveryStatus status, DateTime? reportTime)
        //{
        //    List<SMPPConnection> connection = Connections
        //        .Where(x => x.ESME.SystemId == systemId)
        //        .ToList();

        //    if (!ReferenceEquals(connection, null) && connection.Any())
        //    {
        //        int index = new Random().Next(0, connection.Count - 1);
        //        connection[index].SendDeliveyReport(from, to, message, messageId, status, reportTime);
        //    }
        //}

        #endregion Public Functions

        #region Events
        public event SesionCreatedHandler OnSessionStart;
        public event LogEventHandler OnLog;

        #region [ Session Management ]
        private void Connection_OnSessionStart(SMPPConnection connection, SessionEventArgs e)
        {
            try
            {
                Guid? id;
                SmppSession session = new SmppSession
                {
                    Id = e.Id,
                    Address = e.FullAddress,
                    LastRecieved = DateTime.Now,
                    ValidFrom = DateTime.Now,
                    ValidTo = DateTime.MaxValue.AddDays(-1),
                };

                //if (ReferenceEquals(id = new SmppServerManager().SaveSession(e.Id, null, null, e.Address, DateTime.Now, null, null, DateTime.Now, DateTime.Now).Result, null))
                //    throw new Exception("Session creation failed");

                SmppSessionData smppSessionData = session.ToSessionData();
                smppSessionData.AdditionalParamers.Add("address", e.Address);
                smppSessionData.AdditionalParamers.Add("port", e.Port);

                connection.Identifier = smppSessionData;

                if (ReferenceEquals(id = new SmppServerManager().SaveSession(session).Result, null))
                    throw new Exception("Session creation failed");
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, String.Format("[{0}] Connection_OnSessionStart | {1}", connection.SessionId, ex.ToString()));
            }
        }

        private void Connection_OnSessionEnd(SMPPConnection connection, SessionEventArgs e)
        {
            try
            {
                //SmppSession session = ((SmppSession)connection.Identifier);
                SmppSession session = new SmppServerManager()
                    .SearchSession(e.Id)
                    .Result;

                session.ValidTo = DateTime.Now;
                new SmppServerManager().SaveSession(session);

                if (
                    !ReferenceEquals(connection.DeliveryQueue, null)
                    && connection.DeliveryQueue.Count() > 0
                )
                {
                    do
                    {
                        //SmppDelivery smppDelivery = (SmppDelivery)connection.DeliveryQueue.Dequeue();
                        SmppDeliveryData smppDelivery;
                        while (!connection.DeliveryQueue.TryDequeue(out smppDelivery)) ;

                        SmppDelivery delivery = smppDelivery.ToSmppDelivery();
                        delivery.Status = "NEW";
                        new SmppServerManager().SaveDelivery(delivery);

                    } while (connection.DeliveryQueue.Count() > 0);
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, String.Format("[{0}] Connection_OnSessionEnd | {1}", connection.SessionId, ex.ToString()));
            }
        }
        #endregion

        #region [ Sent ]
        private void Connection_OnSent(SMPPConnection connection, SmppEventArgs e)
        {
            try
            {
                //SmppSession session = (SmppSession)connection.Identifier;

                SmppCommand command = new SmppCommand();
                command.Id = e.Id;
                command.CommandType = SmppCommandType.Sent;
                command.CommandLength = e.CommandLength;
                command.CommandId = e.CommandId;
                command.CommandStatus = e.CommandStatus;
                command.Sequence = e.Sequence;
                command.PDU = e.PDU;
                command.SessionId = connection.SessionId;
                command.CreatedOn = DateTime.Now;

                new SmppServerManager().SaveCommand(command);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, String.Format("[{0}] OnSent | {1}", connection.SessionId, ex.ToString()));
            }
        }
        #endregion

        #region [ Recieved ]
        private void Connection_OnRecieved(SMPPConnection connection, SmppEventArgs e)
        {
            try
            {
                SmppSessionData session = (SmppSessionData)connection.Identifier;

                SmppCommand command = new SmppCommand();
                command.Id = e.Id;
                command.CommandType = SmppCommandType.Recieved;
                command.CommandLength = e.CommandLength;
                command.CommandId = e.CommandId;
                command.CommandStatus = e.CommandStatus;
                command.Sequence = e.Sequence;
                command.PDU = e.PDU;
                command.SessionId = session.Id;
                command.CreatedOn = DateTime.Now;

                new SmppServerManager().SaveCommand(command);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, String.Format("[{0}] OnRecieved | ", connection.SessionId, ex.ToString()));
            }
        }
        #endregion

        #region [ Bind Transceiver ]
        private int Connection_OnBindTransceiver(SMPPConnection connection, BindEventArgs e)
        {
            try
            {
                SmppUserSearch smppUserSearch = new SmppUserSearch();
                smppUserSearch.SystemId = e.SystemId;
                smppUserSearch.Password = e.Password;
                smppUserSearch.SystemType = e.SystemType;
                smppUserSearch.Status = "ACT";

                SmppUser smppUser = new SmppServerManager()
                    .SearchUser(smppUserSearch)
                    .Result
                    .FirstOrDefault();

                SmppSessionData sessionData = (SmppSessionData)connection.Identifier;
                IPAddress address = (IPAddress)sessionData.AdditionalParamers["address"];

                if (ReferenceEquals(smppUser, null))
                    return BindEventArgs.Failed;

                if (!smppUser.SystemId.Equals(e.SystemId))
                    return BindEventArgs.InvalidSystemId;

                if (!smppUser.Password.Equals(e.Password))
                    return BindEventArgs.InvalidPassword;

                if (!smppUser.SystemType.Equals(e.SystemType))
                    return BindEventArgs.InvalidSystemType;

                if (!smppUser.IsValidAddress(address))
                    return BindEventArgs.Failed;

                int userSessions = Connections
                    .Where(x => ((SmppSessionData)x.Identifier)?.UserId == smppUser.UserId)
                    .Count();
                if (userSessions >= smppUser.ConcurrentSessions )
                    return BindEventArgs .Failed;

                SmppSession session = sessionData.ToSession();
                if (ReferenceEquals(session, null))
                    return BindEventArgs.UnknownError;

                ((SmppSessionData)connection.Identifier).AdditionalParamers["routes"] = JsonConvert.DeserializeObject<string[]>(smppUser.Routes);
                ((SmppSessionData)connection.Identifier).TPS = (uint) smppUser.TPS;
                ((SmppSessionData)connection.Identifier).UserId = (uint)smppUser.UserId;
                ((SmppSessionData)connection.Identifier).SmsCost = (decimal)smppUser.SmsCost;
                ((SmppSessionData)connection.Identifier).DltCharge = (decimal)smppUser.DltCharge;

                Dictionary<long, decimal> balance = new SmppServerManager()
                    .GetUserBalance(new long[] { smppUser.UserId })
                    .Result;

                if (userBalance.ContainsKey(smppUser.UserId))
                    userBalance[smppUser.UserId] = balance[smppUser.UserId];
                else
                    userBalance.TryAdd(smppUser.UserId, balance[smppUser.UserId]);

                session.UserId = smppUser.UserId;
                session.BindRequest = DateTime.Now;
                new SmppServerManager().SaveSession(session);


                return BindEventArgs.Success;
            }
            //catch(BindTransceiverException ex)
            //{

            //}
            catch (Exception ex)
            {
                return BindEventArgs.UnknownError;
            }
        }
        #endregion

        #region [ Submit SM ]
        private string Connection_OnSubmitSm(SMPPConnection connection, SubmitSmEventArgs e)
        {
            int pos = 0;
            byte? message_identification = null;
            byte? message_part_total = null;
            byte? message_part = null;
            long? gsm7udhvalue = null;
            string PEID = null;
            string TMID = null;
            string TemplateId = null;

            bool isMultiPartMessage = false;
            logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Begin processing", connection.SessionId, e.Sequence));
            try
            {
                SmppSessionData session = (SmppSessionData)connection.Identifier;
                string[] routes = (string[])session.AdditionalParamers["routes"];
                string shortMessage = string.Empty;
                string @operator = String.Empty;
                string status = "NEW";

                #region [ Check Balance ]
                if (ReferenceEquals(userBalance[session.UserId], null))
                    throw new SmppException(StatusCodes.ESME_RSYSERR);
                else if (userBalance[session.UserId] < session.SmsCost + session.DltCharge)
                    throw new SmppException(StatusCodes.ESME_RSYSERR);
                #endregion

                #region [ TPS Check ]
                //userTps.AddOrUpdate((ulong) session.UserId, userTps.ContainsKey((ulong)session.UserId) ? userTps[(ulong)session.UserId] + 1 :  1, )
                userTps.AddOrUpdate((ulong)session.UserId, 1, (k, v) => v + 1);
                
                if (userTps[(ulong)session.UserId] > session.TPS)
                    throw new SmppException(StatusCodes.ESME_RTHROTTLED);

                #endregion

                #region [ Check valid routes ]
                if (routes.Length == 0)
                    throw new SmppException(StatusCodes.ESME_RSYSERR);
                //string @operator = routes.Length == 1 ? routes[0] : routes[new Random().Next(0, routes.Length - 1)];
                #endregion

                #region [ Check operator is active ]
                string[] activeOperators = SmppConnectionManager
                    .Connections.Where(x => x.CanSend && x.MC.TPS > 0 && routes.Contains(x.MC.Operator))
                    .Select(x => x.MC.Operator)
                    .Distinct()
                    .ToArray();

                if (activeOperators.Length == 0)
                {
                    // throw new SmppException(StatusCodes.ESME_RSYSERR);
                    @operator = routes.Length == 1 ? routes[0] : routes[new Random().Next(0, routes.Length - 1)];
                }
                else
                {
                    status = "SUB";
                    @operator = activeOperators.Length == 1 ? activeOperators[0] : routes[new Random().Next(0, routes.Length - 1)];
                }
                #endregion

                #region [ Optional Parameters ]
                if (!ReferenceEquals(e.OptionalParams, null) && e.OptionalParams.Any())
                {
                    PEID = e.OptionalParams
                        .Where(x => x.Tag == 0x1400)
                        .Select(x => Utility.ConvertArrayToString(x.Value, x.Length < 1 ? 0 : x.Length - 1))
                        .FirstOrDefault();

                    TMID = e.OptionalParams
                        .Where(x => x.Tag == 0x1402)
                        .Select(x => Utility.ConvertArrayToString(x.Value, x.Length < 1 ? 0 : x.Length - 1))
                        .FirstOrDefault();

                    TemplateId = e.OptionalParams
                        .Where(x => x.Tag == 0x1401)
                        .Select(x => Utility.ConvertArrayToString(x.Value, x.Length < 1 ? 0 : x.Length - 1))
                        .FirstOrDefault();


                    //The message_payload parameter contains the user data.Its function is to provide an
                    //alternative means of carrying text lengths above the 255 octet limit of the short_message
                    //field.
                    //Applications, which need to send messages longer than 255 octets, should use the
                    //message_payload TLV.When used in the context of a submit_sm PDU, the sm_length field
                    //should be set to zero.
                    if (e.SmLength == 0)
                    {
                        string messagePayload = e.OptionalParams
                            .Where(x => x.Tag == TagCodes.MESSAGE_PAYLOAD)
                            .Select(x => getShortMessage(e.DataCoding, x.Value, x.Length < 1 ? 0 : x.Length - 1))
                            .FirstOrDefault();

                        if (!String.IsNullOrEmpty(messagePayload))
                        {
                            shortMessage = messagePayload;
                        }
                    }

                }
                #endregion

                #region [ Multi Page SMS ESM Class 0x40 / 64 ]

                //switch (e.EsmClass)
                //{
                //    case EsmClass.MessagingMode.Default | EsmClass.MessageType.Default | EsmClass.GSM.Default:
                //        //shortMessage = e.DataCoding == 0x00 || e.DataCoding == 0x01
                //        //    ? Utility.ConvertArrayToString(e.Message, e.SmLength)
                //        //    : Utility.ConvertArrayToUnicodeString(e.Message, e.SmLength);

                //        shortMessage = getShortMessage(e.DataCoding, e.Message, e.SmLength);
                //        break;
                //    case EsmClass.GSM.UDHIndicator:
                //        byte udh_length = e.Message[pos++];
                //        byte[] udh = new byte[udh_length];
                //        byte subheader_length = 0;
                //        Array.Copy(e.Message, 0, udh, 0, udh.Length);
                //        //pos += udh.Length;

                //        byte udh_code = e.Message[pos++];
                //        switch (udh_code)
                //        {
                //            case UDHIndicators.b00: // Concatenated short messages, 8-bit reference number
                //                                    // Subheader Length(3 bytes)
                //                subheader_length = e.Message[pos++];
                //                if (subheader_length != 0x03)
                //                    throw new SmppException(StatusCodes.ESME_RINVOPTPARSTREAM);

                //                // message identification - can be any hexadecimal
                //                // number but needs to match the UDH Reference Number of all  concatenated SMS
                //                message_identification = e.Message[pos++];
                //                // Number of pieces of the concatenated message
                //                message_part_total = e.Message[pos++];
                //                // Sequence number (used by the mobile to concatenate the split messages)
                //                message_part = e.Message[pos++];

                //                //shortMessage = e.DataCoding == 0x00 
                //                //    ? Utility.ConvertArrayToString(e.Message.Skip(6).ToArray(), e.SmLength - 6)
                //                //    : Utility.ConvertArrayToUnicodeString(e.Message.Skip(6).ToArray(), e.SmLength - 6);
                //                shortMessage = getShortMessage(e.DataCoding, e.Message.Skip(6).ToArray(), e.SmLength - 6);

                //                isMultiPartMessage = true;
                //                break;
                //            case UDHIndicators.b24:
                //            case UDHIndicators.b25:
                //                subheader_length = e.Message[pos++];
                //                if (subheader_length != 0x01)
                //                    throw new SmppException(StatusCodes.ESME_RINVOPTPARSTREAM);

                //                gsm7udhvalue = 0 | udh_code << 16 | subheader_length << 8 | e.Message[pos++];
                //                break;
                //        }
                //        break;
                //    default:
                //        break;
                //}
                switch (e.EsmClass & 0b00000011)
                {
                    case EsmClass.MessagingMode.Default:
                        break;
                    case EsmClass.MessagingMode.Datagram:
                        break;
                    case EsmClass.MessagingMode.Forward:
                        break;
                    case EsmClass.MessagingMode.StoreAndForward:
                        break;
                }
                switch(e.EsmClass & 0b00111100)
                {
                    case EsmClass.MessageType.Default:  // 0
                        break;
                    case EsmClass.MessageType.ContainsMCDeliveryAcknowledgement: // 2
                        break;
                    case EsmClass.MessageType.ContainsMCDeliveryReceipt: // 4
                        break;
                    
                }
                if ((e.EsmClass & 0b11000000) == EsmClass.GSM.UDHIndicator
                    || (e.EsmClass & 0b11000000) == EsmClass.GSM.SetEdhiAndReplyPath)
                {
                    byte udh_length = e.Message[pos++];
                    byte[] udh = new byte[udh_length];
                    byte subheader_length = 0;
                    Array.Copy(e.Message, 0, udh, 0, udh.Length);
                    //pos += udh.Length;

                    byte udh_code = e.Message[pos++];
                    switch (udh_code)
                    {
                        case UDHIndicators.b00: // Concatenated short messages, 8-bit reference number
                                                // Subheader Length(3 bytes)
                            subheader_length = e.Message[pos++];
                            if (subheader_length != 0x03)
                                throw new SmppException(StatusCodes.ESME_RINVOPTPARSTREAM);

                            // message identification - can be any hexadecimal
                            // number but needs to match the UDH Reference Number of all  concatenated SMS
                            message_identification = e.Message[pos++];
                            // Number of pieces of the concatenated message
                            message_part_total = e.Message[pos++];
                            // Sequence number (used by the mobile to concatenate the split messages)
                            message_part = e.Message[pos++];

                            //shortMessage = e.DataCoding == 0x00 
                            //    ? Utility.ConvertArrayToString(e.Message.Skip(6).ToArray(), e.SmLength - 6)
                            //    : Utility.ConvertArrayToUnicodeString(e.Message.Skip(6).ToArray(), e.SmLength - 6);
                            shortMessage = getShortMessage(e.DataCoding, e.Message.Skip(6).ToArray(), e.SmLength - 6);

                            isMultiPartMessage = true;
                            break;
                        case UDHIndicators.b24:
                        case UDHIndicators.b25:
                            subheader_length = e.Message[pos++];
                            if (subheader_length != 0x01)
                                throw new SmppException(StatusCodes.ESME_RINVOPTPARSTREAM);

                            gsm7udhvalue = 0 | udh_code << 16 | subheader_length << 8 | e.Message[pos++];
                            break;
                    }
                }
                else
                {
                    shortMessage = getShortMessage(e.DataCoding, e.Message, e.SmLength);
                }
                #endregion

                logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Begin saving", connection.SessionId, e.Sequence));
                Guid textId = Guid.NewGuid();

                SmppText c = new SmppText
                {
                    Id = textId,
                    SessionId = session.Id,
                    SmppUserId = session.UserId,
                    UserId = session.UserId,
                    CommandId = e.Id,
                    ServiceType = e.ServiceType,
                    SourceAddressNpi = e.SourceAddrNpi,
                    SourceAddressTon = e.SourceAddrTon,
                    SourceAddress = e.SourceAddress,
                    DestAddressNpi = e.DestAddrNpi,
                    DestAddressTon = e.DestAddrTon,
                    DestAddress = e.DestAddress,
                    EsmClass = e.EsmClass,
                    PriorityFlag = e.PriorityFlag,
                    ScheduledDeliveryTime = e.ScheduledDeliveryTime,
                    ValidityPeriod = e.ValidityPeriod,
                    RegisteredDelivery = e.RegisteredDelivery,
                    ReplaceIfPresentFlag = e.ReplaceIfPresentFlag,
                    DataCoding = e.DataCoding,
                    SmDefaultMsgId = e.DefaultMsgId,
                    ShortMessage = shortMessage,
                    MessageIdentification = message_identification,
                    TotalParts = message_part_total,
                    PartNumber = message_part,
                    PEID = PEID,
                    TMID = TMID,
                    TemplateId = TemplateId,
                    MessageId = textId.ToString("N").ToUpper(),
                    Status = "NEW",
                    MessageState = MessageState.SCHEDULED,
                    SmsCost = session.SmsCost,
                    DltCharge = session.DltCharge,
                    CreatedOn = DateTime.Now,
                    UpdatedOn = DateTime.Now

                };

                if (new SmppServerManager().SaveText(c).Result == 0)
                    throw new SmppException(StatusCodes.ESME_RSYSERR);

                Task.Run(async () => { await ProcessMessage(connection, e, c, isMultiPartMessage, @operator, status); }).Wait(100);

                logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Saved", connection.SessionId, e.Sequence));
                return c.MessageId;

            }
            catch (SmppException ex)
            {
                //if (ex.ErrorCode == StatusCodes.ESME_RTHROTTLED)
                //{
                //    connection.unBind();
                //    connection.Disconnect();
                //    connection.Dispose();
                //}
                throw ex;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, String.Format("[{0}] Connection_OnSubmitSm | {1} | {2}", connection.SessionId, e.Sequence, ex.ToString()));
            }

            return string.Empty;
        }

        #region [ Process Message ]
        protected async Task ProcessMessage(SMPPConnection connection, SubmitSmEventArgs e, SmppText c, bool isMultiPartMessage, string @operator, string status)
        {
            try
            {
                SmppSessionData session = (SmppSessionData)connection.Identifier;
                
                //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | ProcessMessage | Check for processing", connection.SessionId, e.Sequence));
                //if (!ReferenceEquals(c.MessageIdentification, null))
                //{
                //    while (connection.ProcessingMessages.Contains(c.MessageIdentification))
                //    {
                //        int delay = new Random().Next(20, 100);
                //        Task.Delay(delay).Wait();
                //    }

                //    lock (connection.ProcessingMessages.SyncRoot)
                //    {
                //        while (connection.ProcessingMessages.Contains(c.MessageIdentification))
                //        {
                //            Task.Delay(10).Wait();
                //        }
                //        connection.ProcessingMessages.Add(c.MessageIdentification, c.MessageIdentification);
                //    }
                //}

                if (isMultiPartMessage)
                {
                    logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | ProcessMessage | Begin processing parts", connection.SessionId, e.Sequence));
                    await ConsolidateMessage(connection, e, c, @operator, status);
                }
                else
                {
                    logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | ProcessMessage | Begin forwarding", connection.SessionId, e.Sequence));
                    long longValue;

                    #region [ SMPP SMS Record ]
                    //SmppSmsRecord smsRecord = new SmppSmsRecord();
                    //smsRecord.Id = Guid.NewGuid();
                    //smsRecord.SMSType = Int64.TryParse(e.SourceAddress, out longValue) ? "P" : "T";
                    //smsRecord.From = e.SourceAddress;
                    //smsRecord.To = e.DestAddress;
                    //smsRecord.Message = c.ShortMessage;
                    //smsRecord.Priority = e.PriorityFlag;
                    //smsRecord.DataCoding = e.DataCoding;
                    //smsRecord.Language = new int[] { 0, 1 }.Contains(e.DataCoding) ? "E" : "B";
                    //smsRecord.Status = "NEW";
                    //smsRecord.TemplateId = String.Empty;
                    //smsRecord.SessionId = session.Id;
                    //smsRecord.UserId = session.UserId;
                    //smsRecord.CreatedOn = DateTime.Now;
                    //smsRecord.DeliveryStatus = null;
                    //smsRecord.DeliveredOn = null;
                    //smsRecord.UpdatedOn = DateTime.Now;

                    //await new SmppServerManager().SaveSmsRecord(smsRecord);
                    #endregion

                    #region [ Save Send Sms ] 

                    ulong send_sms_id = await new BulksSmsManager().SaveSendSms(
                        //send_sms_id: 0,
                        sms_campaign_head_details_id: 0,
                        sms_campaign_details_id: 0,
                        smpp_user_details_id: (int)((SmppSessionData)connection.Identifier).UserId,
                        message: c.ShortMessage,
                        senderid: c.SourceAddress,
                        enitityid: c.PEID, //e.OptionalParams.Where(x => x.Tag == 0x1400).Select(x => x.Value).FirstOrDefault(),
                        templateid: c.TemplateId, // c.OptionalParams.Where(x => x.Tag == 0x1401).Select(x => x.Value).FirstOrDefault(),
                        destination: Convert.ToInt64(e.DestAddress),
                        piority: e.PriorityFlag,
                        message_id: "",
                        submit_sms_id: 0,
                        coding: (e.DataCoding == 8 ? 2 : 0),
                        smsc_details_id: 0,
                        create_date: DateTime.Now,
                        status: status,
                        dlt_cost: session.DltCharge,
                        sms_cost: session.SmsCost,
                        serial_number: 1,
                        @operator: @operator,
                        smpp: "",
                        from: "",
                        session_id: null,
                        retry_count: 1,
                        sms_cost_mode: '1',
                        source: "SRV"
                    ); ;

                    #endregion

                    c.SendSmsId = send_sms_id; // smsRecord.Id;
                    c.Status = "PRC";
                    await new SmppServerManager().SaveText(c);

                    #region [ Add SMS to connection queue ]
                    if (c.Status == "PRC")
                    {
                        //SmppConnectionManager
                        //    .Connections.Where(x => x.CanSend && x.MC.TPS > 0 && @operator.Equals(x.MC.Operator))
                        //    .FirstOrDefault()
                        //    .SendSms()
                        SmsMessage sms = new SmsMessage()
                        {
                            From = c.SourceAddress,
                            To = c.DestAddress,
                            Coding = (e.DataCoding == 8 ? 2 : 0),
                            Message = c.ShortMessage,
                            AskDeliveryReceipt = true,
                            Priority = c.PriorityFlag,
                            RefId = send_sms_id.ToString(),
                            PEID = c.PEID,
                            TMID = c.TMID,
                            TemplateId = c.TemplateId,
                            Operator = @operator,
                            RetryIndex = 0
                        };
                        sms.AdditionalData["sms_campaign_head_details_id"] = (long)0;
                        sms.AdditionalData["sms_campaign_details_id"] = (long)0;
                        sms.AdditionalData["smpp_user_details_id"] = (int)((SmppSessionData)connection.Identifier).UserId;
                        sms.AdditionalData["dlt_cost"] = session.DltCharge;
                        sms.AdditionalData["sms_cost"] = session.SmsCost;
                        sms.AdditionalData["sms_cost_mode"] = "1";
                        Messages.Enqueue(@operator, sms);
                        
                    }
                    #endregion
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, String.Format("[{0}] Connection_OnSubmitSm | {1} | ProcessMessage | {2}", connection.SessionId, e.Sequence, ex.ToString()));
            }
            //finally
            //{
            //    logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | ProcessMessage | Begin lock release", connection.SessionId, e.Sequence));
            //    if (!ReferenceEquals(c.MessageIdentification, null))
            //    {
            //        lock (connection.ProcessingMessageList.SyncRoot)
            //        {
            //            if (connection.ProcessingMessageList.Contains(c.MessageIdentification))
            //                connection.ProcessingMessageList.Remove(c.MessageIdentification);
            //        }
            //    }
            //    logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | ProcessMessage | Lock released", connection.SessionId, e.Sequence));
            //}

        }
        #endregion

        #region [ Message Merger ]
        protected async Task ConsolidateMessage(SMPPConnection connection, SubmitSmEventArgs e, SmppText smppText, string @operator, string status)
        {
            SmppSessionData session = (SmppSessionData)connection.Identifier;

            //String key = string.Format("{0}{1}{2}",
            //    smppText.SourceAddress,
            //    smppText.DestAddress,
            //    smppText.MessageIdentification
            //);

            ulong key = ulong.Parse(String.Format("{0}{1:000}", smppText.DestAddress, smppText.MessageIdentification));
            IList smppTexts = null;
            string messageText = string.Empty;
            byte[] partNumbers = new byte[0];
            bool isReceiveCompleted;

            SortedList messageParts = null;

            logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {2} | Begin consolicating part {3} of {4} | MessageId {5}", connection.SessionId, e.Sequence, key, smppText.PartNumber, smppText.TotalParts, smppText.MessageId));
            //lock (connection.MessageBuilder.SyncRoot)
            //{
            //int keyIndex = connection.MessageBuilder.ContainsKey(key);

            // if not matched create new key
            lock (connection.MessageBuilder.SyncRoot)
            {
                if (!connection.MessageBuilderHash.ContainsKey(key))
                    connection.MessageBuilderHash.Add(key, DateTime.Now);
                else
                    connection.MessageBuilderHash[key] = DateTime.Now;


                if (!connection.MessageBuilder.ContainsKey(key))
                {
                    messageParts = SortedList.Synchronized(new SortedList());
                    connection.MessageBuilder.Add(key, messageParts);
                }
                else
                {
                    messageParts = (SortedList)connection.MessageBuilder[key];
                }


                lock (messageParts.SyncRoot)
                {
                    // get first item in key
                    SmppText st = (SmppText)(
                        connection.MessageBuilder.Count > 0
                        ? (
                            messageParts.Count > 0
                            ? messageParts.GetByIndex(0)
                            : null
                        ) : null
                    );
                    //if (
                    //    ReferenceEquals(st, null)
                    //    || String.IsNullOrEmpty(st.SourceAddress)
                    //    || String.IsNullOrEmpty(smppText.SourceAddress)
                    //    || String.IsNullOrEmpty(st.DestAddress)
                    //    || String.IsNullOrEmpty(smppText.DestAddress)
                    //    || !st.SourceAddress.Equals(smppText.SourceAddress, StringComparison.Ordinal)
                    //    || !st.DestAddress.Equals(smppText.DestAddress, StringComparison.Ordinal)
                    //)
                    //{
                    //    logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {2} | Clearing", connection.SessionId, e.Sequence, key));
                    //    smppTexts = ReferenceEquals(messageParts, null)
                    //            ? null
                    //            : messageParts.GetValueList();
                    //    connection.MessageBuilder[key] = messageParts = SortedList.Synchronized(new SortedList());
                    //    logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {2} | Cleared", connection.SessionId, e.Sequence, key));
                    //}

                    //SortedList messageParts = (SortedList)connection.MessageBuilder[key];
                    if (!messageParts.ContainsKey((byte)smppText.PartNumber))
                    {
                        logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {2} | Created", connection.SessionId, e.Sequence, key));
                        messageParts.Add((byte)smppText.PartNumber, smppText);
                    }
                    else
                    {
                        logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {2} | Already Exists {3}. new message id {4}", connection.SessionId, e.Sequence, key, ((SmppText)messageParts[smppText.PartNumber]).MessageId, smppText.MessageId));
                    }
                }
            }

            if (!ReferenceEquals(smppTexts, null) && smppTexts.Count > 0)
            {
                logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {2} | Error Marking {3} of {4} partially received messages", connection.SessionId, e.Sequence, key, smppTexts.Count, ((SmppText)smppTexts[0]).TotalParts));
                for (int i = 0; i < smppTexts.Count; i++)
                {
                    SmppText text = (SmppText)smppTexts[i];
                    text.Status = "ERR";

                    await new SmppServerManager().SaveText(text);
                }

                smppTexts = null;
            }

            logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {2} | Part {3} of {4} | MessageId {5}", connection.SessionId, e.Sequence, key, smppText.PartNumber, smppText.TotalParts, smppText.MessageId));

            isReceiveCompleted = false;


            lock (messageParts.SyncRoot)
            {

                isReceiveCompleted = (smppText.TotalParts == messageParts.Count);
                logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {2} | Received {3} of {4} | isReceiveCompleted : {5}", connection.SessionId, e.Sequence, key, messageParts.Count, smppText.TotalParts, isReceiveCompleted));
                if (isReceiveCompleted)
                {
                    logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {2} | Received completely, building message", connection.SessionId, e.Sequence, key));

                    #region [ Message is completely received ]

                    partNumbers = messageParts //smppTextData
                        .Keys.Cast<byte>()
                        .OrderBy(x => x)
                        .ToArray();

                    foreach (byte index in partNumbers)
                    {
                        string textMessage = ((SmppText)messageParts[index]).ShortMessage;

                        if (!String.IsNullOrEmpty(textMessage))
                            messageText += textMessage;
                    }

                    smppTexts = messageParts.GetValueList();

                    #endregion

                    logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {2} | Received completely, message build complete", connection.SessionId, e.Sequence, key));
                }
            }

            long longValue;
            if (isReceiveCompleted)
            {
                logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {1} | Received completely, saving full message", connection.SessionId, key));
                #region [ Save Record ]
                //SmppSmsRecord smsRecord = new SmppSmsRecord();
                //smsRecord.Id = Guid.NewGuid();
                //smsRecord.SMSType = Int64.TryParse(smppText.SourceAddress, out longValue) ? "P" : "T";
                //smsRecord.From = smppText.SourceAddress;
                //smsRecord.To = smppText.DestAddress;
                //smsRecord.Message = messageText;
                //smsRecord.Priority = smppText.PriorityFlag;
                //smsRecord.DataCoding = smppText.DataCoding;
                //smsRecord.Language = new int[] { 0, 1 }.Contains(smppText.DataCoding) ? "E" : "B";
                //smsRecord.Status = "NEW";
                //smsRecord.TemplateId = String.Empty;
                //smsRecord.SessionId = smppText.SessionId;
                //smsRecord.UserId = ((SmppSessionData)connection.Identifier).UserId;
                //smsRecord.CreatedOn = DateTime.Now;
                //smsRecord.DeliveryStatus = null;
                //smsRecord.DeliveredOn = null;
                //smsRecord.UpdatedOn = DateTime.Now;

                //await new SmppServerManager().SaveSmsRecord(smsRecord);
                #endregion

                #region [ Save Send Sms ] 
                
                ulong send_sms_id = await new BulksSmsManager().SaveSendSms(
                    //send_sms_id: 0,
                    sms_campaign_head_details_id: 0,
                    sms_campaign_details_id: 0,
                    smpp_user_details_id: (int)session.UserId,
                    message: messageText,
                    senderid: smppText.SourceAddress,
                    enitityid: smppText.PEID,
                    templateid: smppText.TemplateId,
                    destination: Convert.ToInt64(smppText.DestAddress),
                    piority: smppText.PriorityFlag,
                    message_id: String.Empty,
                    submit_sms_id: 0,
                    coding: (smppText.DataCoding == 8 ? 2 : 0),
                    smsc_details_id: 0,
                    create_date: DateTime.Now,
                    status: status,
                    dlt_cost: session.DltCharge * (byte) smppText.TotalParts,
                    sms_cost: session.SmsCost * (byte) smppText.TotalParts,
                    serial_number: 1,
                    @operator: @operator,
                    smpp: "",
                    from: "",
                    session_id: null,
                    retry_count: 1,
                    sms_cost_mode: '1',
                    source: "SRV"
                );


                #region [ Add SMS to connection queue ]
                if (smppText.Status == "SUB")
                {
                    //SmppConnectionManager
                    //    .Connections.Where(x => x.CanSend && x.MC.TPS > 0 && @operator.Equals(x.MC.Operator))
                    //    .FirstOrDefault()
                    //    .SendSms()
                    SmsMessage sms = new SmsMessage()
                    {
                        From = smppText.SourceAddress,
                        To = smppText.DestAddress,
                        Coding = (smppText.DataCoding == 8 ? 8 : 0),
                        Message = messageText,
                        AskDeliveryReceipt = true,
                        Priority = smppText.PriorityFlag,
                        RefId = send_sms_id.ToString(),
                        PEID = smppText.PEID,
                        TMID = smppText.TMID,
                        TemplateId = smppText.TemplateId,
                        Operator = @operator,
                        RetryIndex = 0
                    };
                    sms.AdditionalData["sms_campaign_head_details_id"] = (long)0;
                    sms.AdditionalData["sms_campaign_details_id"] = (long) 0;
                    sms.AdditionalData["smpp_user_details_id"] = (int)((SmppSessionData)connection.Identifier).UserId;
                    sms.AdditionalData["dlt_cost"] = session.DltCharge;
                    sms.AdditionalData["sms_cost"] = session.SmsCost;
                    sms.AdditionalData["sms_cost_mode"] = "1";
                    Messages.Enqueue(@operator, sms);

                }
                #endregion
                #endregion


                for (int i = 0; i < smppTexts.Count; i++)
                {
                    SmppText text = (SmppText)smppTexts[i];
                    text.SendSmsId = send_sms_id; //smsRecord.Id;
                    text.Status = "PRC";
                    await new SmppServerManager().SaveText(text);
                }
                logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {1} | Received completely, saving full message complete", connection.SessionId, key));
                lock (connection.MessageBuilder.SyncRoot)
                {
                    if (ReferenceEquals(connection.MessageBuilder[key], messageParts))
                    {
                        connection.MessageBuilder.Remove(key);
                        logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnSubmitSm | {1} | Consolidate Message | Identification {1} | Activity complete, removed parts", connection.SessionId, key));


                        if (connection.MessageBuilderHash.ContainsKey(key))
                            connection.MessageBuilderHash.Remove(key);
                    }
                }

            }
        }
        #endregion

        protected string getShortMessage(byte dataCoding, byte[] ar, int len)
        {
            string messageText = String.Empty;
            Encoding encoding;

            switch (dataCoding)
            {
                case 0b00000000: // MC default
                    //messageText = Encoding.ASCII.GetString(ar, 0, len);

                    Encoding gsmEnc = new Mediaburst.Text.GSMEncoding();
                    Encoding utf8Enc = new System.Text.UTF8Encoding();

                    //byte[] gsmBytes = utf8Enc.GetBytes(body);
                    byte[] gsmBytes = new byte[len];
                    Array.Copy(ar, 0, gsmBytes, 0, len);
                    byte[] utf8Bytes = Encoding.Convert(gsmEnc, utf8Enc, gsmBytes);
                    messageText = utf8Enc.GetString(utf8Bytes);

                    break;
                case 0b00000001: //IA5 (CCITT T.50)/ASCII (ANSI b3.4)
                    messageText = Encoding.ASCII.GetString(ar, 0, len);
                    break;
                case 0b00000010: //Octet unspecified (8-bit binary)
                    break;
                case 0b00000011: // Latin 1 (ISO-8859-1)
                    encoding = Encoding.GetEncoding("ISO-8859-1");
                    messageText = encoding.GetString(ar, 0, len);
                    break;
                case 0b00000100: // Octet unspecified (8-bit binary)
                    break;
                case 0b00000101: // JIS (b 0208-1990)
                    break;
                case 0b00000110: // Cyrillic (ISO-8859-5)
                    encoding = Encoding.GetEncoding("ISO-8859-5");
                    messageText = encoding.GetString(ar, 0, len);
                    break;
                case 0b00000111: // Latin/Hebrew (ISO-8859-8)
                    encoding = Encoding.GetEncoding("ISO-8859-8");
                    messageText = encoding.GetString(ar, 0, len);
                    break;
                case 0b00001000: // UCS2 (ISO/IEC-10646)
                    messageText = Encoding.BigEndianUnicode.GetString(ar, 0, len);
                    break;
                case 0b00001001: // Pictogram Encoding
                    break;
                case 0b00001010: // ISO-2022-JP (Music Codes)
                    break;
                case 0b00001011: // Reserved
                    break;
                case 0b00001100: // Reserved
                    break;
                case 0b00001101: // Ebtended Kanji JIS (b 0212-1990)
                    break;
                case 0b00001110: // KS C 5601
                    break;
                default:
                    if (0b00001111 <= dataCoding && dataCoding <= 0b10111111)
                    {
                        // reserved
                    }
                    else if (0b11000000 <= dataCoding && dataCoding <= 0b11001111)
                    {
                        // GSM MWI control - see [GSM 03.38]
                    }
                    else if (0b11010000 <= dataCoding && dataCoding <= 0b11011111)
                    {
                        // GSM MWI control - see [GSM 03.38]
                    }
                    else if (0b11100000 <= dataCoding && dataCoding <= 0b11101111)
                    {
                        // Reserved
                    }
                    else if (0b11110000 <= dataCoding && dataCoding <= 0b11111111)
                    {
                        // GSM message class control - see [GSM 03.38]
                        Encoding gsmEnc1 = new Mediaburst.Text.GSMEncoding();
                        Encoding utf8Enc1 = new System.Text.UTF8Encoding();

                        //byte[] gsmBytes = utf8Enc.GetBytes(body);
                        byte[] gsmBytes1 = new byte[len];
                        Array.Copy(ar, 0, gsmBytes1, 0, len);
                        byte[] utf8Bytes1 = Encoding.Convert(gsmEnc1, utf8Enc1, gsmBytes1);
                        messageText = utf8Enc1.GetString(utf8Bytes1);
                    }

                    break;

            }

            return messageText;
        }

        #region [ 7-bit decoder ]
        public static string Decode7bit(string source, int length)
        {
            byte[] bytes = GetInvertBytes(source);

            string binary = string.Empty;

            foreach (byte b in bytes)
                binary += Convert.ToString(b, 2).PadLeft(8, '0');

            binary = binary.PadRight(length * 7, '0');

            string result = string.Empty;

            for (int i = 1; i <= length; i++)
                result += (char)Convert.ToByte(binary.Substring(binary.Length - i * 7, 7), 2);

            return result.Replace('\x0', '\x40');
        }

        public static byte[] GetInvertBytes(string source)
        {
            byte[] bytes = GetBytes(source);

            Array.Reverse(bytes);

            return bytes;
        }

        public static byte[] GetBytes(string source)
        {
            return GetBytes(source, 16);
        }

        public static byte[] GetBytes(string source, int fromBase)
        {
            List<byte> bytes = new List<byte>();

            for (int i = 0; i < source.Length / 2; i++)
                bytes.Add(Convert.ToByte(source.Substring(i * 2, 2), fromBase));

            return bytes.ToArray();
        }
        #endregion

        #endregion

        #region [ Deliver SM ]
        private void Connection_OnDeliverSmTimerTick(SMPPConnection connection)
        {
            //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnDeliverSmTimerTick | Begin processing", connection.SessionId));
            try
            {
                if (ReferenceEquals(connection, null))
                {
                    //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnDeliverSmTimerTick | Connection is null", connection.SessionId));
                    return;
                }

                if (!connection.CanSend)
                {
                    //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnDeliverSmTimerTick | Not connected", connection.SessionId));
                    return;
                }

                //logMessage(LogLevels.LogSteps, String.Format("connection.DeliveryQueue.Count = {0}", connection.DeliveryQueue.Count));
                if (connection.DeliveryQueue.Count() >= 200)
                {
                    //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnDeliverSmTimerTick | Queue full", connection.SessionId));
                    return;
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, String.Format("[{0}] Connection_OnDeliverSmTimerTick | {1}", connection.SessionId, ex.ToString()));
                return;
            }

            if (!Monitor.TryEnter(deliveryLoaderLock))
            {
                //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnDeliverSmTimerTick | Unable to get lock", connection.SessionId));
                return;
            }

            try
            {
                SmppSessionData session = (SmppSessionData)connection.Identifier;

                //List<SmppDelivery> smppDeliveries = new List<SmppDelivery>();
                SmppDeliveryData[] smppDeliveries = null;
                //smppDeliveries = new SmppDeliveryManager()
                //        .Search(new SmppDeliverySearch
                //        {
                //            Top = 100,
                //            Status = new List<string> { "NEW" },
                //            MaxRetryIndex = SmppDeliverySearch.MAX_RETRY,
                //            RetryTo = DateTime.Now,
                //            UserId = ((SmppSession)connection.Identifier).User.Id
                //        });

                //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnDeliverSmTimerTick | Begin loading", connection.SessionId));

                //smppDeliveries = new SmppServerManager()
                //    .GetPendingDeliveries(
                //        session.UserId,
                //        1000,
                //        "INP",
                //        deliveryMaxRetry,
                //        DateTime.Now
                //    )
                //    .Result
                //    .ToArray();


                int rowsAffected = new SmppServerManager()
                    .MarkDeliveries(
                        session.UserId,
                        1000,
                        "SUB",
                        deliveryMaxRetry,
                        "INP"
                    ).Result;

                if (rowsAffected == 0)
                    return;

                smppDeliveries = new SmppServerManager()
                    .GetPendingDeliveries(
                        session.UserId,
                        1000,
                        "INP",
                        deliveryMaxRetry,
                        DateTime.Now
                    )
                    .Result
                    .Select(x => x.ToSmppDeliveryData())
                    .ToArray();

                rowsAffected = new SmppServerManager()
                    .MarkDeliveries(
                        session.UserId,
                        1000,
                        "INP",
                        deliveryMaxRetry,
                        "PRO"
                    ).Result;

                if (ReferenceEquals(smppDeliveries, null))
                    return;

                if (smppDeliveries.Length == 0)
                    //throw new NotSupportedException();
                    return;

                //lock (connection.DeliveryQueue.SyncRoot)
                //{
                //foreach (var item in smppDeliveries)
                //{
                //    connection.DeliveryQueue.Enqueue(item);
                //}
                //}
                for (int i = 0; i < smppDeliveries.Length; i++)
                {
                    connection.DeliveryQueue.Enqueue(smppDeliveries[i]);
                }


                //connection.DeliveryQueue.Enqueue(smppDeliveries[0]);
                //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnDeliverSmTimerTick | {1} Loaded", connection.SessionId, smppDeliveries.Count));
            }
            //catch (NotSupportedException ex)
            //{

            //}
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, String.Format("[{0}] Connection_OnDeliverSmTimerTick | {1}", connection.SessionId, ex.ToString()));
            }
            finally
            {
                Monitor.Exit(deliveryLoaderLock);
            }
        }

        private void Connection_OnDeliverSmSend(SMPPConnection connection, DeliverSmSentEventArgs e)
        {
            SmppDelivery smppDelivery = e.Data.ToSmppDelivery();
            //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnDeliverSmSend | {1} | Begin processing", connection.SessionId, e.Sequence));
            try
            {
                //SmppEventArgs smppEventArgs = null;

                //connection.SendDeliveyReport(
                //    smppDelivery.SourceAddress,
                //    smppDelivery.DestAddress,
                //    smppDelivery.ShortMessage,
                //    smppDelivery.MessageId,
                //    (DeliveryStatus)smppDelivery.DeliveryStatus,
                //    smppDelivery.DeliveryTime,
                //    ref smppEventArgs
                //);
                //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnDeliverSmSend | {1} | Begin saving", connection.SessionId, e.Sequence));

                smppDelivery.RetryCount += 1;
                //smppDelivery.CommandId = ReferenceEquals(smppEventArgs, null) ? null : (Guid?)smppEventArgs.Id;
                smppDelivery.CommandId = e.Data.ToSmppDelivery().CommandId ?? e.Id;
                if (e.SentStatus == 0)
                {
                    smppDelivery.Status = "SENT";
                }
                else if (smppDelivery.RetryCount >= deliveryMaxRetry)
                {
                    smppDelivery.RetryOn = DateTime.MaxValue;
                    smppDelivery.Status = "ERR";
                }
                else
                {
                    smppDelivery.RetryOn = DateTime.Now.AddMinutes(5 * smppDelivery.RetryCount);
                    smppDelivery.Status = "NEW";
                }
                smppDelivery.UpdatedOn = DateTime.Now;

                new SmppServerManager().SaveDelivery(smppDelivery);
                //Guid? id = new SmppDeliveryManager().Save(smppDelivery).Result;

                //logMessage(LogLevels.LogSteps, String.Format("[{0}] Connection_OnDeliverSmSend | {1} | Saved", connection.SessionId, e.Sequence));
            }
            //catch (NotSupportedException ex)
            //{
            //    smppDelivery.RetryIndex += 1;
            //    if (smppDelivery.RetryIndex == SmppDeliverySearch.MAX_RETRY)
            //    {
            //        smppDelivery.RetryOn = DateTime.MaxValue;
            //        smppDelivery.Status = "ERR";
            //    }
            //    else
            //    {
            //        smppDelivery.RetryOn = DateTime.Now.AddMinutes(5 * smppDelivery.RetryIndex);
            //        smppDelivery.Status = "NEW";
            //    }

            //    smppDelivery.UpdatedOn = DateTime.Now;
            //}
            catch (Exception ex)
            {
                //smppDelivery.RetryIndex += 1;
                //if (smppDelivery.RetryIndex == SmppDeliverySearch.MAX_RETRY)
                //{
                //    smppDelivery.RetryOn = DateTime.MaxValue;
                //    smppDelivery.Status = "ERR";
                //}
                //else
                //{
                //    smppDelivery.RetryOn = DateTime.Now.AddMinutes(5 * smppDelivery.RetryIndex);
                //    smppDelivery.Status = "NEW";
                //}

                //smppDelivery.UpdatedOn = DateTime.Now;

                logMessage(LogLevels.LogExceptions, String.Format("[{0}] Connection_OnDeliverSmServer | {1}", connection.SessionId, ex.ToString()));
            }

            finally
            {

            }
        }
        #endregion

        #region [ Unbind ]
        private uint Connection_OnUnbind(SMPPConnection connection)
        {
            try
            {
                SmppSession session = ((SmppSession)connection.Identifier);
                session.UnbindRequest = DateTime.Now;
                new SmppServerManager().SaveSession(session);//.Wait();

                return StatusCodes.ESME_ROK;
            }
            catch (Exception ex)
            {
                return StatusCodes.ESME_RUNKNOWNERR;
            }
        }
        #endregion

        #endregion Events

        #region Private functions

        #region [ Listen ]
        private void StartListen()
        {
            listener.BeginAcceptTcpClient(onAcceptConnection, listener);
        }

        private void OnAcceptTcpClient(IAsyncResult result)
        {
            try
            {
                TcpListener listener = result.AsyncState as TcpListener;

                if (active)
                    listener.BeginAcceptTcpClient(onAcceptConnection, listener);

                TcpClient tcpClient = listener.EndAcceptTcpClient(result);

                if (tcpClient.Connected && active)
                {
                    //Logger.Write(LogType.Information, "Client Connected!!");
                    //Logger.Write(LogType.Information, "Client IP : " + tcpClient.Client.RemoteEndPoint);
                    processLog(new LogEventArgs(LogType.Information, "Client Connected!!"));
                    processLog(new LogEventArgs(LogType.Information, "Client IP : " + tcpClient.Client.RemoteEndPoint));


                    SMPPConnection connection = new SMPPConnection(
                        smsc,
                        tcpClient,
                        deliverySmDateFormat: "yyMMddHHmmss",
                        onSessionStart: Connection_OnSessionStart,
                        onSessionEnd: Connection_OnSessionEnd,
                        onClientBindRequested: Connection_OnBindTransceiver,
                        onUnbind : Connection_OnUnbind,
                        onSubmitSm : Connection_OnSubmitSm,
                        onLog : Connection_OnLog
                    );
                    //connection.OnSessionStart += Connection_OnSessionStart;
                    //connection.OnLog += Connection_OnLog;
                    //connection.OnSessionEnd += Connection_OnSessionEnd;
                    //if ((int)Logger.LogLevel <= (int)LogType.Steps)
                    //{
                    connection.OnRecieved += Connection_OnRecieved;
                    connection.OnSent += Connection_OnSent;
                    //}
                    //connection.OnBindTransceiver += Connection_OnBindTransceiver;
                    //connection.OnClientBindRequested += Connection_OnBindTransceiver;
                    //connection.OnClientBind += Connection_OnBindTransceiver;
                    //connection.OnUnbind += Connection_OnUnbind;
                    //connection.OnSubmitSm += Connection_OnSubmitSm;
                    connection.OnDeliverSmTimerTick += Connection_OnDeliverSmTimerTick;
                    connection.OnDeliverSmSend += Connection_OnDeliverSmSend;
                    Connections.Add(connection);
                }
                else if (tcpClient.Connected && !active)
                {
                    tcpClient.Client.Shutdown(SocketShutdown.Both);
                }
            }
            catch (ObjectDisposedException ex)
            {
                return;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "OnAcceptTcpClient | " + ex.ToString());
            }
        }

        //private int Connection_OnClientBind(SMPPConnection connection, BindEventArgs e)
        //{
        //    throw new NotImplementedException();
        //}
        #endregion

        #region [ OnLog ]
        private void Connection_OnLog(SMPPConnection connection, LogEventArgs e)
        {
            processLog(e);
        }

        private void Connection_OnLog(LogEventArgs e)
        {
            processLog(e);
        }
        #endregion

        #region [ Log ]

        private void processLog(LogEventArgs e)
        {
            try
            {
                if (OnLog != null)
                {
                    //OnLog.BeginInvoke(e, onLogCallback, e);
                    Task.Run(() => this.OnLog(null, e));
                }
            }
            catch (Exception ex)
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

        private void logMessage(int logLevel, string pMessage)
        {
            try
            {
                if ((logLevel) > 0)
                {
                    //MyLog.WriteLogFile("Log", "Log level-" + logLevel, pMessage);
                    //Logger.Write((LogType)logLevel, pMessage);
                    LogEventArgs evArg = new LogEventArgs((LogType)logLevel, pMessage);
                    processLog(evArg);
                }
            }
            catch (Exception ex)
            {
                // DO NOT USE LOG INSIDE LOG FUNCTION !!! logMessage(LogLevels.LogExceptions, "logMessage | " +ex.ToString());
            }
        }//logMessage
        #endregion

        #endregion Private Functions

    }//SMPPClient
}
