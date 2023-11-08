using Newtonsoft.Json;
using Org.BouncyCastle.Utilities.Encoders;
using SMSGateway.DataManager;
using SMSGateway.Entity;
using SMSGateway.SMSCClient;
using SMSGateway.Tools;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Text.Json.Serialization;

namespace SMSGateway.SMPPClient
{
    public class SmppWorker : BackgroundService
    {
        private readonly ILogger<SmppWorker> _logger;
        //public static List<SMPPConnection> connections = new List<SMPPConnection>();
        IConfiguration Configuration = null;
        SmppOptions options = null;
        SortedList activeConnections = SortedList.Synchronized(new SortedList());

        #region [ Constructor ]
        public SmppWorker(ILogger<SmppWorker> logger)
        {
            _logger = logger;

            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("smppconfig.json")
                .Build();

            options = Configuration.GetOptions<SmppOptions>();
            options.KernelParameters.Save();
        }
        #endregion


        #region [ Start Async ]
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            //Dictionary<string, object> settings = Configuration
            //    .GetSection("clients")
            //    .Get<Dictionary<string, object>>();
            //string json = JsonConvert.SerializeObject(settings);

            foreach (SMSC smsc in options.Providers)
            {
                for (int instanceCount = 0; instanceCount < (smsc.Instances < 1 ? 1 : smsc.Instances); instanceCount++)
                {
                    SMSC sMSC = smsc.Clone();
                    sMSC.Instance = (instanceCount + 1).ToString();
                    sMSC.TPS = smsc.TPS / smsc.Instances;
                    SMPPConnection connection = new SMPPConnection(
                        smsc: sMSC,
                        onSmppBinded: Connection_OnSmppBinded
                    );
                    connection.OnUnbind += Connection_OnUnbind;
                    connection.OnLog += Connection_OnLog;
                    connection.OnSendSms += Connection_OnSendSms;
                    connection.OnSubmitSm += Connection_OnSubmitSm;
                    connection.OnSubmitSmResp += Connection_OnSubmitSmResp;
                    connection.OnDeliverSm += Connection_OnDeliverSm;
                    SmppConnectionManager.Connections.Add(connection);
                }
            }
            //while(!connection.CanSend)
            //{
            //    await Task.Delay(50);
            //}

            //For Unicode SMS
            // 16 - Unicode / UTF-16 / UCS2 - 8
            // 7 ASCII   - 1
            // SMSC Default - 0
            // https://en.wikipedia.org/wiki/Data_Coding_Scheme
            //byte dataCoding = (byte)(recordItem.Language == "E" ? DefaultEncoding : 8);

            //if (connection.CanSend)
            //{
            //    connection.SendSms(
            //        from: "IFBONL",
            //        to: "919330332978",
            //        splitLongText: true,
            //        text: "Dear Anirban, We regret your service request 1122334455 is still pending with us, due to prevailing situation that is causing inevitable delays on spares. This is not an experience we would want you to have. We are doing our best to expedite this -IFB",
            //        askDeliveryReceipt: (byte)1,
            //        dataEncoding: true ? Encoding.ASCII : Encoding.BigEndianUnicode,
            //        dataCoding: true ? Tools.MessageEncoding.DEFAULT : MessageEncoding.UCS2,
            //        refid: Guid.NewGuid().ToString("N"),
            //        peId: "1601100000000005628",
            //        tmId: "1602100000000005442",
            //        templateId: "1707167964420071950",
            //        retryIndex: 0
            //    );
            //}


            await base.StartAsync(cancellationToken);
        }
        #endregion

        #region [ Execute Async ]
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("SmppWorker_ExecuteAsync :: Start");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("SmppWorker_ExecuteAsync :: Tick");
                try
                {
                    //_logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
                    foreach (SMPPConnection connection in SmppConnectionManager.Connections)
                    {

                        _logger.LogDebug($"SmppWorker_ExecuteAsync :: Operator - {connection.MC.Operator}, \n Instance - {connection.MC.Instance}, Can Send - {connection.CanSend} [{connection.ConnectionState}], TPS - {connection.MC.TPS}, Que Msg - {Messages.Count(connection.MC.Operator)}, Max Q - {connection.MC.MaxQueue}, xx - {(Messages.Count(connection.MC.Operator) < connection.MC.MaxQueue)}");
                        //For Unicode SMS
                        // 16 - Unicode / UTF-16 / UCS2 - 8
                        // 7 ASCII   - 1
                        // SMSC Default - 0
                        // https://en.wikipedia.org/wiki/Data_Coding_Scheme
                        //byte dataCoding = (byte)(recordItem.Language == "E" ? DefaultEncoding : 8);

                        // Ensure the connection is not being used and is connected and TPS is allocated
                        if (!activeConnections.Contains(connection.SessionId) && connection.CanSend && connection.MC.TPS > 0)
                        { 
                            List<SmsMessage> messages = Messages.TryDequeueN(connection.MC.Operator, 100).ToList();

                            if (!messages.Any())
                                continue;

                            Task.Run(async () =>
                                {
                                    lock (activeConnections.SyncRoot)
                                    {
                                        activeConnections.Add(connection.SessionId, new object());
                                    }
                                    await SendMessages(connection, messages, 1000 / connection.MC.TPS, stoppingToken);
                                })
                                .ContinueWith(task =>
                                {
                                    lock (activeConnections.SyncRoot)
                                    {
                                        activeConnections.Remove(connection.SessionId);
                                    }
                                });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                }
                _logger.LogDebug("SmppWorker_ExecuteAsync :: Finish");

                try
                {
                    await Task.Delay(100, stoppingToken);
                }
                catch (TaskCanceledException exception)
                {
                    //_logger.LogCritical(exception, "TaskCanceledException Error", exception.Message);
                }
            }

            _logger.LogDebug("SmppWorker_ExecuteAsync :: Stopping");
            Stop();
            _logger.LogDebug("SmppWorker_ExecuteAsync :: Stopped");
        }
        #endregion

        #region [ Stop Async ]
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            
            await base.StopAsync(cancellationToken);
        }
        #endregion

        protected void Stop()
        {
            int connectionCount = SmppConnectionManager.Connections.Count();
            for (int i = 0; i < connectionCount; i++)
            {
                try
                {
                    SmppConnectionManager.Connections[i].unBind();
                }
                catch (Exception ex)
                {

                }
            }
        }

        protected async Task SendMessages(SMPPConnection connection, List<SmsMessage> messages, int delay, CancellationToken stoppingToken)
        {
            try
            {
                //if (!connection.CanSend)
                //{
                //    foreach (var m in messages)
                //        Messages.Enqueue(m.Operator, m);

                //    return;
                //}
                List<SmsMessage> maxRetriedMessages = new List<SmsMessage>();
                foreach (var m in messages)
                {
                    if (m.RetryIndex > connection.MC.MaxRetry)
                    {
                        maxRetriedMessages.Add(m);
                        continue;
                    }

                    int count = 0;
                    if (connection.CanSend)
                    {
                        count = connection.SendSms(
                            from: m.From,
                            to: m.To,
                            splitLongText: true,
                            text: m.Message,
                            askDeliveryReceipt: (byte)1,
                            priorityFlag: m.Priority,
                            dataEncoding: m.Coding == 0 ? Encoding.ASCII : Encoding.BigEndianUnicode,
                            dataCoding: m.Coding == 0 ? Tools.MessageEncoding.DEFAULT : MessageEncoding.UCS2,
                            refid: m.RefId,
                            peId: m.PEID,
                            tmId: m.TMID,
                            templateId: m.TemplateId,
                            retryIndex: m.RetryIndex,
                            additionalParameters: m.AdditionalData
                        );
                    }
                    else
                    {
                        m.RetryIndex++;
                        Messages.Enqueue(m.Operator, m);
                    }

                    await Task.Delay(delay * count, stoppingToken);
                }

                if (maxRetriedMessages.Any())
                {
                    await new BulksSmsManager().UpdateSendSmsById(maxRetriedMessages.Select(x => Convert.ToInt64(x.RefId)).ToList(), "ERR");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }


        #region [ Event Actions ]

        #region [ Bind ]
        private int Connection_OnSmppBinded(SMPPConnection connection, BindEventArgs args)
        {
            return 0;
        }
        #endregion

        #region [ Unbind ]
        private uint Connection_OnUnbind(SMPPConnection connection)
        {
            SmppConnectionManager.Connections.Remove(connection);
            return 0;
        }
        #endregion

        #region [ Send SMS ]
        private void Connection_OnSendSms(SMPPConnection connection, SendSmsEventArgs e)
        {
            try
            {
                Task.Run(async () => {
                    decimal dlt_cost = (Decimal)GetAdditionalParameterValue(e.AdditionalParameters, "dlt_cost", connection.MC.DLTCost);
                    decimal sms_cost = (Decimal)GetAdditionalParameterValue(e.AdditionalParameters, "sms_cost", connection.MC.SubmitCost);

                    await new BulksSmsManager()
                       .UpdateSendSms(
                            Convert.ToInt64(e.RefId),
                            "SENT",
                            e.RetryIndex,
                            $"{connection.MC.Operator}_{connection.MC.Instance}",
                            dlt_cost,
                            sms_cost * e.MessageCount
                        );
                }).ContinueWith(task => {
                    if (task.IsFaulted)
                    {
                        var ex = task.Exception.InnerException != null ? task.Exception.InnerException : task.Exception;
                        _logger.LogCritical(ex.Message, JsonConvert.SerializeObject(ex));
                    }
                });
            }
            catch(Exception ex)
            {
                _logger.LogCritical(ex.Message, JsonConvert.SerializeObject(ex));
            }
        }
        #endregion

        #region [ Submit SMS ]
        private string Connection_OnSubmitSm(SMPPConnection connection, SubmitSmEventArgs e)
        {
            try
            {
                //string sql = String.Format(
                //        @"INSERT INTO [SMSPush] (
                //            [Source], [Destination], [Message], [PeId], [TemplateId], [TmId]
                //            , [Vendor], [Connection], [SequenceNo], [RefID], [RetryIndex], [DataCoding], [CreationDate]
                //        ) 
                //        VALUES (
                //            '{0}', '{1}', '{2}', '{3}', '{4}', '{5}', 
                //            '{6}', '{7}', {8}, {9}, {10}, {11}, '{12:yyyy-MM-ddTHH:mm:ss.fff}'
                //        )"
                //        , e.SourceAddress
                //        , e.DestAddress
                //        , Utility.RemoveApostropy(Encoding.UTF8.GetString(e.Message))
                //        , e.OptionalParams.Where(x => x.Tag == 0x1400).Select(x => Encoding.ASCII.GetString(x.Value)).FirstOrDefault()
                //        , e.OptionalParams.Where(x => x.Tag == 0x1401).Select(x => Encoding.ASCII.GetString(x.Value)).FirstOrDefault()
                //        , e.OptionalParams.Where(x => x.Tag == 0x1402).Select(x => Encoding.ASCII.GetString(x.Value)).FirstOrDefault()
                //        , connection.MC.Operator
                //        , $"{connection.MC.Operator}_{connection.MC.Instance}"
                //        , e.Sequence
                //        , e.RefId
                //        , e.RetryIndex
                //        , e.DataCoding
                //        , DateTime.Now
                //    );
                //Console.WriteLine($"{DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss.fff")} : { sql }");

            }
            catch(Exception ex)
            {
                _logger.LogCritical(ex.Message, JsonConvert.SerializeObject(ex));
            }
            return null;
        }
        #endregion

        protected object GetAdditionalParameterValue(IDictionary<string, object> parameters, string key, object defaultValue = null)
        {
            if (ReferenceEquals(parameters, null))
                return defaultValue;

            if (parameters.ContainsKey(key))
                return parameters[key];

            return defaultValue;
        }

        #region [ Submit SMS Response ]
        private void Connection_OnSubmitSmResp(SMPPConnection connection, SubmitSmEventArgs submitSmEventArgs, SubmitSmRespEventArgs submitSmRespEvent)
        {
            try
            {
                Task.Run(async () => {

                    decimal dlt_cost = (Decimal)GetAdditionalParameterValue(submitSmEventArgs.AdditionalParameters, "dlt_cost", connection.MC.DLTCost);
                    decimal sms_cost = (Decimal)GetAdditionalParameterValue(submitSmEventArgs.AdditionalParameters, "sms_cost", connection.MC.SubmitCost);

                    await new BulksSmsManager()
                        .SaveSentSms(
                            sent_sms_id: Convert.ToInt64(submitSmEventArgs.RefId),
                            send_sms_s1_id: Convert.ToInt64(submitSmEventArgs.RefId),
                            send_sms_p1_id: Convert.ToInt64(submitSmEventArgs.RefId),
                            send_sms_id: Convert.ToInt64(submitSmEventArgs.RefId),
                            //sms_campaign_head_details_id: Convert.ToInt64(submitSmEventArgs.RefId),
                            sms_campaign_head_details_id: (long)GetAdditionalParameterValue(submitSmEventArgs.AdditionalParameters, "sms_campaign_head_details_id", 0),
                            sms_campaign_details_id: (long)GetAdditionalParameterValue(submitSmEventArgs.AdditionalParameters, "sms_campaign_details_id", 0),
                            smpp_user_details_id: (int)GetAdditionalParameterValue(submitSmEventArgs.AdditionalParameters, "smpp_user_details_id", 0),
                            message: Utility.RemoveApostropy(Encoding.UTF8.GetString(submitSmEventArgs.Message)),
                            senderid: submitSmEventArgs.SourceAddress,
                            enitityid: submitSmEventArgs.OptionalParams.Where(x => x.Tag == 0x1400).Select(x => Encoding.ASCII.GetString(x.Value)).FirstOrDefault(),
                            templateid: submitSmEventArgs.OptionalParams.Where(x => x.Tag == 0x1401).Select(x => Encoding.ASCII.GetString(x.Value)).FirstOrDefault(),
                            destination: submitSmEventArgs.DestAddress,
                            piority: submitSmEventArgs.PriorityFlag,
                            coding: submitSmEventArgs.DataCoding,
                            smsc_details_id: connection.MC.Operator,
                            create_date: DateTime.Now.ToLocalTime(),
                            status: "SENT",
                            dlt_cost: dlt_cost.ToString("0.000"),
                            sms_cost: sms_cost.ToString("0.000"),
                            p1_move_date: DateTime.Now.ToLocalTime(),
                            s1_move_date: DateTime.Now.ToLocalTime(),
                            move_date: DateTime.Now.ToLocalTime(),
                            pdu_id: "",
                            sequence_id: submitSmEventArgs.Sequence.ToString(),
                            message_id: submitSmRespEvent.MessageID
                        );
                })
                .ContinueWith(task => { 
                    if (task.IsFaulted)
                    {
                         var ex = task.Exception.InnerException != null ? task.Exception.InnerException : task.Exception;
                        _logger.LogCritical(ex.Message, JsonConvert.SerializeObject(ex));
                    }
                });

            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex.Message, JsonConvert.SerializeObject(ex));
            }
        }
        #endregion

        #region [ Delivery Report ]
        private uint Connection_OnDeliverSm(SMPPConnection connection, DeliverSmEventArgs e)
        {
            try
            {
                Dictionary<string, string> dictionaryText = Utility.ParseDeliveryMessageText(e.TextString);


                //Console.WriteLine($"{DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss.fff")} : { JsonConvert.SerializeObject(e) }");
                //throw new NotImplementedException();
                string messageId = e.ReceiptedMessageID;
                switch (connection.MC.MessageIdType?.ToUpper())
                {
                    
                    case "INT_HEX":
                        // convery hex to int
                        messageId = Int64
                            .Parse(messageId, System.Globalization.NumberStyles.HexNumber)
                            .ToString();                        
                        break;
                    case "HEX_INT":
                        // convert int to hex
                        messageId = Int64
                            .Parse(messageId, System.Globalization.NumberStyles.Any)
                            .ToString("x");
                        break;
                    case "STRING":
                    case "INT_INT":
                    case "HEX_HEX":
                    default:
                        break;
                }

                Task.Run(async() => {
                    // ReferenceEquals(e.SubmitDate, null) ? "01-Jan-1970 00:00:00" : ((DateTime)e.SubmitDate).ToString("dd-MMM-yyyy HH:mm:ss"),

                    DateTime submit_date = ReferenceEquals(e.SubmitDate, null)
                        ? new DateTime(1970, 1, 1)
                        : (DateTime)e.SubmitDate;

                    //ReferenceEquals(e.DoneDate, null) ? "01-Jan-1970 00:00:00" : ((DateTime)e.DoneDate).ToString("dd-MMM-yyyy HH:mm:ss"),
                    DateTime dlr_status_date = ReferenceEquals(e.DoneDate, null)
                        ? new DateTime(1970, 1, 1)
                        : (DateTime)e.DoneDate;

                    if (connection.MC.DLRInUTC)
                    {
                        //TimeZoneInfo ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                        TimeZoneInfo localTz = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneInfo.Local.StandardName);

                        submit_date = new DateTime(
                                submit_date.Year,
                                submit_date.Month,
                                submit_date.Day,
                                submit_date.Hour,
                                submit_date.Minute,
                                submit_date.Second,
                                submit_date.Millisecond,
                                submit_date.Microsecond,
                                DateTimeKind.Utc
                            );

                        submit_date = TimeZoneInfo.ConvertTime(submit_date, TimeZoneInfo.Utc, localTz);

                        dlr_status_date = new DateTime(
                                dlr_status_date.Year,
                                dlr_status_date.Month,
                                dlr_status_date.Day,
                                dlr_status_date.Hour,
                                dlr_status_date.Minute,
                                dlr_status_date.Second,
                                dlr_status_date.Millisecond,
                                dlr_status_date.Microsecond,
                                DateTimeKind.Utc
                            );

                        dlr_status_date = TimeZoneInfo.ConvertTime(dlr_status_date, TimeZoneInfo.Utc, localTz);
                    }


                    await new BulksSmsManager().SaveDeliveryReport(
                        message_id: messageId,
                        destination: e.From,
                        sender: e.To,
                        //sms_dlr_status_id: Utility.MessageDeliveryStatus(e.MessageState),
                        sms_dlr_status_id: e.MessageState.ToString(),
                        smsc_details_id: connection?.MC?.Operator,
                        smpp_user_details_id: 0,
                        message: String.Empty,
                        //submit_date: (ReferenceEquals(e.SubmitDate, null) ? new DateTime(2000, 1, 1) : (DateTime)e.SubmitDate).ToLocalTime(), // ReferenceEquals(e.SubmitDate, null) ? "01-Jan-1970 00:00:00" : ((DateTime)e.SubmitDate).ToString("dd-MMM-yyyy HH:mm:ss"),
                        //dlr_status_date: (ReferenceEquals(e.DoneDate, null) ? new DateTime(2000, 1, 1) : (DateTime)e.DoneDate).ToLocalTime(), //ReferenceEquals(e.DoneDate, null) ? "01-Jan-1970 00:00:00" : ((DateTime)e.DoneDate).ToString("dd-MMM-yyyy HH:mm:ss"),
                        submit_date: submit_date,
                        dlr_status_date: dlr_status_date,
                        errorCode: dictionaryText.ContainsKey("err") ? dictionaryText["err"] : String.Empty,
                        shortmessage: e.TextString
                    );
                }).ContinueWith(task => {
                    if (task.IsFaulted)
                    {
                        var ex = task.Exception.InnerException != null ? task.Exception.InnerException : task.Exception;
                        _logger.LogCritical(ex.Message, JsonConvert.SerializeObject(ex));
                    }
                });

                _logger.LogTrace(JsonConvert.SerializeObject(e), e);
                return StatusCodes.ESME_ROK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                return StatusCodes.ESME_RDELIVERYFAILURE;
            }
        }
        #endregion

        #region [ Log ]
        private void Connection_OnLog(SMPPConnection connection, LogEventArgs e)
        {
            //throw new NotImplementedException();
            //Console.WriteLine($"{DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss.fff")} : {e.LogType.ToString()} : {e.Message}");
            try
            {
                switch (e.LogType)
                {
                    case LogType.Pdu:
                        _logger.LogTrace(e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;
                    case LogType.Steps:
                        _logger.LogDebug(e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;
                    case LogType.Warning:
                        _logger.LogWarning(e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;
                    case LogType.Error:
                    case LogType.Exceptions:
                        _logger.LogError(e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;
                    case LogType.Information:
                        _logger.LogInformation(e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;
                    default:
                        _logger.Log(LogLevel.None, e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;

                }
            }
            catch (Exception ex)
            {

            }
        }
        #endregion
        
        #endregion
    }
}