using Newtonsoft.Json;
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

                //SMSC smsc = new SMSC(
                //    host: "smpp.ifbhub.com",
                //    port: 8012,
                //    systemId: "ifb_industries",
                //    password: "P@ssw0rd",
                //    systemType: "IFB-TEST"
                //  );

                SMPPConnection connection = new SMPPConnection(
                    smsc: smsc,
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
            _logger.LogInformation("SmppWorker_ExecuteAsync :: Start");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("SmppWorker_ExecuteAsync :: Tick");
                try
                {
                    //_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
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
                GC.Collect();
                GC.WaitForPendingFinalizers();
                //await Task.Delay(1000, stoppingToken);



                try
                {
                    await Task.Delay(100, stoppingToken);
                }
                catch (TaskCanceledException exception)
                {
                    //_logger.LogCritical(exception, "TaskCanceledException Error", exception.Message);
                }
                _logger.LogDebug("SmppWorker_ExecuteAsync :: Finish");
            }

            _logger.LogInformation("SmppWorker_ExecuteAsync :: Stopping");
            Stop();
            _logger.LogInformation("SmppWorker_ExecuteAsync :: Stopped");
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
                            dataEncoding: m.Coding == 0 ? Encoding.ASCII : Encoding.BigEndianUnicode,
                            dataCoding: m.Coding == 0 ? Tools.MessageEncoding.DEFAULT : MessageEncoding.UCS2,
                            refid: m.RefId,
                            peId: m.PEID,
                            tmId: m.TMID,
                            templateId: m.TemplateId,
                            retryIndex: m.RetryIndex
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
        private void Connection_OnSendSms(SMPPConnection connnection, SendSmsEventArgs e)
        {
            try
            {
                new BulksSmsManager()
                   .UpdateSendSms(
                        Convert.ToInt64(e.RefId),
                        "SENT", 
                        e.RetryIndex, 
                        $"{connnection.MC.Operator}_{connnection.MC.Instance}",
                        connnection.MC.DLTCost,
                        connnection.MC.SubmitCost * e.MessageCount
                    )
                   .Wait();
            }
            catch(Exception ex)
            {
                _logger.LogCritical(ex.Message, ex);
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
                _logger.LogCritical(ex.Message, ex);
            }
            return null;
        }
        #endregion



        #region [ Submit SMS Response ]
        private void Connection_OnSubmitSmResp(SMPPConnection connection, SubmitSmEventArgs submitSmEventArgs, SubmitSmRespEventArgs submitSmRespEvent)
        {
            try
            {
                new BulksSmsManager()
                    .SaveSentSms(
                        sent_sms_id: Convert.ToInt64(submitSmEventArgs.RefId),
                        send_sms_s1_id: Convert.ToInt64(submitSmEventArgs.RefId),
                        send_sms_p1_id: Convert.ToInt64(submitSmEventArgs.RefId),
                        send_sms_id: Convert.ToInt64(submitSmEventArgs.RefId),
                        sms_campaign_head_details_id: Convert.ToInt64(submitSmEventArgs.RefId),
                        sms_campaign_details_id: 0,
                        smpp_user_details_id: 0,
                        message: Utility.RemoveApostropy(Encoding.UTF8.GetString(submitSmEventArgs.Message)),
                        senderid: submitSmEventArgs.SourceAddress,
                        enitityid: submitSmEventArgs.OptionalParams.Where(x => x.Tag == 0x1400).Select(x => Encoding.ASCII.GetString(x.Value)).FirstOrDefault(),
                        templateid: submitSmEventArgs.OptionalParams.Where(x => x.Tag == 0x1401).Select(x => Encoding.ASCII.GetString(x.Value)).FirstOrDefault(),
                        destination: submitSmEventArgs.DestAddress,
                        piority: submitSmEventArgs.PriorityFlag,
                        coding: submitSmEventArgs.DataCoding,
                        smsc_details_id: connection.MC.Operator,
                        create_date: DateTime.Now,
                        status: "SENT",
                        dlt_cost: connection.MC.DLTCost.ToString("0.000"),
                        sms_cost: connection.MC.SubmitCost.ToString("0.000"),
                        p1_move_date: DateTime.Now,
                        s1_move_date: DateTime.Now,
                        move_date: DateTime.Now,
                        pdu_id: "",
                        sequence_id: submitSmEventArgs.Sequence.ToString(),
                        message_id: submitSmRespEvent.MessageID
                    ).Wait();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex.Message, ex);
            }
        }
        #endregion

        #region [ Delivery Report ]
        private uint Connection_OnDeliverSm(SMPPConnection connection, DeliverSmEventArgs e)
        {
            try
            {

                //Console.WriteLine($"{DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss.fff")} : { JsonConvert.SerializeObject(e) }");
                //throw new NotImplementedException();

                new BulksSmsManager().SaveDeliveryReport(
                    message_id: e.ReceiptedMessageID,
                    destination: e.To,
                    sender: e.From,
                    sms_dlr_status_id: String.Empty,
                    smsc_details_id: connection?.MC?.Operator,
                    smpp_user_details_id: 0,
                    message: String.Empty,
                    submit_date: ReferenceEquals(e.SubmitDate, null) ? new DateTime(2000, 1, 1) : (DateTime) e.SubmitDate, // ReferenceEquals(e.SubmitDate, null) ? "01-Jan-1970 00:00:00" : ((DateTime)e.SubmitDate).ToString("dd-MMM-yyyy HH:mm:ss"),
                    dlr_status_date: ReferenceEquals(e.DoneDate, null) ? new DateTime(2000, 1, 1) : (DateTime) e.DoneDate, //ReferenceEquals(e.DoneDate, null) ? "01-Jan-1970 00:00:00" : ((DateTime)e.DoneDate).ToString("dd-MMM-yyyy HH:mm:ss"),
                    errorCode: String.Empty,
                    shortmessage: e.TextString
                ).Wait();

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
                    case LogType.Error:
                        _logger.LogError(e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;
                    case LogType.Warning:
                        _logger.LogWarning(e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;
                    case LogType.Information:
                        _logger.LogInformation(e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;
                    case LogType.Pdu:
                        _logger.LogDebug(e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;
                    case LogType.Exceptions:
                        _logger.LogCritical(e.Message, connection.MC.Operator, connection.MC.Instance);
                        break;
                    default:
                        _logger.LogTrace(e.Message, connection.MC.Operator, connection.MC.Instance);
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