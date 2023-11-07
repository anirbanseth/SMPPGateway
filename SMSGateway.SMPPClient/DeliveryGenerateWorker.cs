using Google.Protobuf;
using MySqlX.XDevAPI;
using Newtonsoft.Json;
using SMSGateway.DataManager;
using SMSGateway.Entity;
using SMSGateway.SMSCClient;
using SMSGateway.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;

namespace SMSGateway.SMPPClient
{
    public class DeliveryGenerateWorker : BackgroundService
    {
        private readonly ILogger<DeliveryGenerateWorker> _logger;
        IConfiguration Configuration = null;
        SmppOptions options = null;

        #region [ Constructor ]
        public DeliveryGenerateWorker(ILogger<DeliveryGenerateWorker> logger)
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
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("DeliveryGenerateWorker :: Starting");
                try
                {

                    _logger.LogDebug("DeliveryGenerateWorker_ExecuteAsync :: Tick");

                    List<string> activeOperators = SmppConnectionManager.GetActiveOperatorsWithAvailableQueue();

                    _logger.LogDebug($"DeliveryGenerateWorker_ExecuteAsync :: Active operators {(!activeOperators.Any() ? "NONE" : String.Join(",", activeOperators))}");

                    if (ReferenceEquals(activeOperators, null) || !activeOperators.Any())
                        throw new DataMisalignedException("No active SMPP connections");

                    await ProcessByStatus(activeOperators, "CUT", "CUTI", "CUTP");
                    await ProcessByStatus(activeOperators, "BLK", "BLKI", "BLKP");
                    await ProcessByStatus(activeOperators, "DND", "DNDI", "DNDP");
                    await ProcessByStatus(activeOperators, "ER0", "ER0I", "ER0P");
                }
                catch (DataMisalignedException ex)
                {
                    _logger.LogDebug("DeliveryGenerateWorker_ExecuteAsync", ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                }

                _logger.LogDebug("DeliveryGenerateWorker :: Complete");

                try
                {
                    await Task.Delay(100, stoppingToken);
                }
                catch (TaskCanceledException exception)
                {
                    //_logger.LogCritical(exception, "TaskCanceledException Error", exception.Message);
                }
                
            }
        }

        protected async Task ProcessByStatus(List<string> activeOperators, string status, string intermediate_status, string final_status)
        {
            try
            {
                

                _logger.LogDebug($"DeliveryGenerateWorker_ExecuteAsync :: ProcessByStatus :: Marking as {intermediate_status}");
                await new BulksSmsManager().MarkMessages(status, activeOperators, intermediate_status);

                _logger.LogDebug($"DeliveryGenerateWorker_ExecuteAsync :: ProcessByStatus :: Reading {intermediate_status}");
                List<SmsMessage> messages = await new BulksSmsManager().GetMarkedMessages(intermediate_status);

                _logger.LogDebug($"DeliveryGenerateWorker_ExecuteAsync :: ProcessByStatus :: Marking as {final_status}");
                await new BulksSmsManager().MarkMessages(intermediate_status, activeOperators, final_status);

                _logger.LogDebug($"DeliveryGenerateWorker_ExecuteAsync :: ProcessByStatus :: Processing {messages.Count} messages");
                foreach (var message in messages)
                {
                    _logger.LogDebug($"DeliveryGenerateWorker_ExecuteAsync :: ProcessByStatus :: {message.Operator}");
                    //Messages.Enqueue(message.Operator, message);
                    SMSC? smsc = options.Providers.Where(x => x.Operator == message.Operator).FirstOrDefault();
                    await ProcessMessage(smsc, status, status, message)
                    .ContinueWith(task => {
                        if (task.IsFaulted)
                        {
                            var ex = task.Exception.InnerException != null ? task.Exception.InnerException : task.Exception;
                            _logger.LogCritical(ex.Message, JsonConvert.SerializeObject(ex));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        protected async Task ProcessMessage(SMSC smsc, string instance, string status, SmsMessage message)
        {
            if (ReferenceEquals(smsc, null))
                return;

            #region [ Message Splitting ]
            MessageEncoding dataCoding = message.Coding == 0 ? Tools.MessageEncoding.DEFAULT : MessageEncoding.UCS2;
            Encoding dataEncoding = message.Coding == 0 ? Encoding.ASCII : Encoding.BigEndianUnicode;
            int maxLength = SmsEncoding.MaxTextLength[(int)dataCoding];
            int splitSize = SmsEncoding.SplitSize[(int)dataCoding];//maxLength;
            string smsText = message.Message;

            bool isLongMessage = smsText.Length > maxLength;
            int dstCoding = (int)(dataCoding == MessageEncoding.DEFAULT ? smsc.DefaultEncoding : dataCoding);

            List<byte[]> messages = new List<byte[]>();

            if (!isLongMessage)
                splitSize = maxLength;

            int index = 0;
            byte[] srcBytes;
            byte[] destBytes;
            while (index < smsText.Length)
            {
                int count = splitSize > (smsText.Length - index) ? (smsText.Length - index) : splitSize;

                byte[] messageBytes;
                do
                {
                    //logMessage(LogType.Steps, String.Format("STR  :{0:000}:{1}", smsText.Substring(index, count).Length, smsText.Substring(index, count)));
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
            #endregion

            decimal dlt_cost = (Decimal)GetAdditionalParameterValue(message.AdditionalData, "dlt_cost", smsc.DLTCost);
            decimal sms_cost = (Decimal)GetAdditionalParameterValue(message.AdditionalData, "sms_cost", smsc.SubmitCost);


            await new BulksSmsManager()
                       .UpdateSendSms(
                            Convert.ToInt64(message.RefId),
                            $"SENT_{status}",
                            message.RetryIndex + 1,
                            $"{smsc.Operator}_{instance}",
                            dlt_cost,
                            sms_cost * messages.Count
                        );

            foreach (byte[] msg in messages)
            {
                string message_id = Guid.NewGuid().ToString();
                DateTime submit_date = DateTime.Now;
                string message_text = Encoding.UTF8.GetString(msg);

                #region [ sent_sms ]
                await new BulksSmsManager()
                .SaveSentSms(
                    sent_sms_id: Convert.ToInt64(message.RefId),
                    send_sms_s1_id: Convert.ToInt64(message.RefId),
                    send_sms_p1_id: Convert.ToInt64(message.RefId),
                    send_sms_id: Convert.ToInt64(message.RefId),
                    //sms_campaign_head_details_id: Convert.ToInt64(submitSmEventArgs.RefId),
                    sms_campaign_head_details_id: (long)GetAdditionalParameterValue(message.AdditionalData, "sms_campaign_head_details_id", 0),
                    sms_campaign_details_id: (long)GetAdditionalParameterValue(message.AdditionalData, "sms_campaign_details_id", 0),
                    smpp_user_details_id: (int)GetAdditionalParameterValue(message.AdditionalData, "smpp_user_details_id", 0),
                    message: Utility.RemoveApostropy(message_text),
                    senderid: message.From,
                    enitityid: message.PEID,
                    templateid: message.TemplateId,
                    destination: message.To,
                    piority: message.Priority,
                    coding: message.Coding,
                    smsc_details_id: message.Operator,
                    create_date: DateTime.Now.ToLocalTime(),
                    status: $"SENT-{status}",
                    dlt_cost: dlt_cost.ToString("0.000"),
                    sms_cost: sms_cost.ToString("0.000"),
                    p1_move_date: DateTime.Now.ToLocalTime(),
                    s1_move_date: DateTime.Now.ToLocalTime(),
                    move_date: DateTime.Now.ToLocalTime(),
                    pdu_id: "",
                    sequence_id: new Random().Next(Int32.MaxValue).ToString(),
                    message_id: message_id
                );
                #endregion

                #region [ sms_dlr_all_status ]
                //Dictionary<string, Dictionary<byte, string[]>[]>[] dic_operator = options
                //    .DeliveryGenerateParams
                //    .Where(x => x.ContainsKey(smsc.Operator))
                //    .Select(x => x[smsc.Operator])
                //    .FirstOrDefault();

                //Dictionary <byte, string[]>[] operator_message_status = dic_operator
                //    .Where(x => x.ContainsKey(status))
                //    .Select(x => x[status])
                //    .FirstOrDefault();



                //byte[] applicable_delivery_codes = new byte[] { 
                //    2, // Delivered
                //    3, // Expired
                //    5, // Undeliverable
                //    8  // Rejected
                //};

                ////applicable_delivery_codes = operator_message_status.
                //string[][] error_codes = new string[][]
                //{
                //    // Delivered
                //    new string[] { "000" },
                //    // Expired
                //    new string[] { "0000" },
                //    // Undeliverable
                //    new string[] { "00" },
                //    // Rejected
                //    new string[] { "0" }
                //};

                //int message_status_index = new Random().Next(0, applicable_delivery_codes.Length - 1);
                //byte message_status = applicable_delivery_codes[message_status_index];

                //int error_code_index = new Random().Next(0, error_codes[message_status_index].Length - 1);
                //string error_code = error_codes[message_status_index][error_code_index];


                Dictionary<string, Dictionary<byte, string[]>>[] dic_operator = options
                    .DeliveryGenerateParams
                    .Where(x => x.ContainsKey(smsc.Operator))
                    .Select(x => x[smsc.Operator])
                    .FirstOrDefault();

                Dictionary<byte, string[]> operator_message_status = dic_operator
                    .Where(x => x.ContainsKey(status))
                    .Select(x => x[status])
                    .FirstOrDefault();


                byte[] applicable_delivery_codes = operator_message_status.Keys.Cast<byte>().ToArray();
                int message_status_index = new Random().Next(0, applicable_delivery_codes.Length);
                if (message_status_index >= applicable_delivery_codes.Length)
                    return;
                byte message_status = applicable_delivery_codes[message_status_index];

                string[] error_codes = operator_message_status[applicable_delivery_codes[message_status_index]];
                int error_code_index = new Random().Next(0, error_codes.Length);
                if (error_code_index >= error_codes.Length)
                    return;
                string error_code = error_codes[error_code_index];


                await new BulksSmsManager().SaveDeliveryReport(
                        message_id: message_id,
                        destination: message.To,
                        sender: message.From,
                        //sms_dlr_status_id: Utility.MessageDeliveryStatus(e.MessageState),
                        sms_dlr_status_id: message_status.ToString(),
                        smsc_details_id: smsc.Operator,
                        smpp_user_details_id: 0,
                        message: String.Empty,
                        submit_date: submit_date, // ReferenceEquals(e.SubmitDate, null) ? "01-Jan-1970 00:00:00" : ((DateTime)e.SubmitDate).ToString("dd-MMM-yyyy HH:mm:ss"),
                        dlr_status_date: DateTime.Now, // ReferenceEquals(e.DoneDate, null) ? new DateTime(2000, 1, 1) : (DateTime)e.DoneDate, //ReferenceEquals(e.DoneDate, null) ? "01-Jan-1970 00:00:00" : ((DateTime)e.DoneDate).ToString("dd-MMM-yyyy HH:mm:ss"),
                        errorCode: error_code, //dictionaryText.ContainsKey("err") ? dictionaryText["err"] : String.Empty,
                        shortmessage: (message_text?.Length > 50 ? message_text.Substring(0, 50) : message_text)
                    );

                #endregion
            }


        }


        protected object GetAdditionalParameterValue(IDictionary<string, object> parameters, string key, object defaultValue = null)
        {
            if (ReferenceEquals(parameters, null))
                return defaultValue;

            if (parameters.ContainsKey(key))
                return parameters[key];

            return defaultValue;
        }
    }
}
