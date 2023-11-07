using Newtonsoft.Json;
using SMSGateway.DataManager;
using SMSGateway.Entity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace SMSGateway.SMPPClient
{
    internal class DatabaseWorker : BackgroundService
    {
        private readonly ILogger<DatabaseWorker> _logger;
        IConfiguration Configuration = null;
        SmppOptions options = null;


        #region [ Constructor ]
        public DatabaseWorker(ILogger<DatabaseWorker> logger)
        {
            _logger = logger;

            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("smppconfig.json")
                .Build();

            SmppOptions options = Configuration.GetOptions<SmppOptions>();

            //Messages.OnMessageRemoved += Messages_OnMessageRemoved;
        }
        #endregion


        #region [ Start Async ]
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            base.StartAsync(cancellationToken);
        }
        #endregion

        #region [ Execute Async ]
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("DatabaseWorker_ExecuteAsync :: Start");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("DatabaseWorker_ExecuteAsync :: Tick");

                    List<string> activeOperators = SmppConnectionManager.GetActiveOperatorsWithAvailableQueue();

                    _logger.LogDebug($"DatabaseWorker_ExecuteAsync :: Active operators { (!activeOperators.Any() ? "NONE" : String.Join(",", activeOperators)) }");

                    if (ReferenceEquals(activeOperators, null) || !activeOperators.Any())
                        throw new DataMisalignedException("No active SMPP connections");
                    
                    _logger.LogDebug("DatabaseWorker_ExecuteAsync :: Marking as PRO");
                    await new BulksSmsManager().MarkMessages("NEW", activeOperators, "PRO");

                    _logger.LogDebug("DatabaseWorker_ExecuteAsync :: Reading PRO");
                    List<SmsMessage> messages = await new BulksSmsManager().GetMarkedMessages("PRO");

                    _logger.LogDebug("DatabaseWorker_ExecuteAsync :: Marking as INP");
                    await new BulksSmsManager().MarkMessages("PRO", activeOperators, "INP");

                    _logger.LogDebug($"DatabaseWorker_ExecuteAsync :: Distributing {messages.Count} messages");
                    foreach (var message in messages)
                    {
                        _logger.LogDebug($"DatabaseWorker_ExecuteAsync :: {message.Operator}");
                        Messages.Enqueue(message.Operator, message);
                    }
                }
                catch (DataMisalignedException ex)
                {
                    _logger.LogDebug("DatabaseWorker_ExecuteAsync", ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex.Message, JsonConvert.SerializeObject(ex));
                }
                _logger.LogDebug("DatabaseWorker_ExecuteAsync :: Finish");

                try
                {
                    await Task.Delay(100, stoppingToken);
                }
                catch (TaskCanceledException exception)
                {
                    //_logger.LogCritical(exception, "TaskCanceledException Error", exception.Message);
                }
            }


            _logger.LogDebug("DatabaseWorker_StopAsync :: Stopping");
            await Stop(stoppingToken);
            _logger.LogDebug("DatabaseWorker_StopAsync :: Cleanup complete");
        }
        #endregion

        #region [ Stop Async ]
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            

            await base.StopAsync(stoppingToken);
        }
        #endregion

        #region [ Stop ]
        protected async Task Stop(CancellationToken stoppingToken)
        {
            try
            {
                while (SmppConnectionManager.Connections.Where(x => !ReferenceEquals(x, null) && x.CanSend).Count() > 0)
                {
                    await Task.Delay(10).WaitAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {

            }

            List<SmsMessage> smsMessages = new List<SmsMessage>();

            foreach (string smppOperator in Messages.Keys)
            {
                try
                {
                    int count = Messages.Count(smppOperator);
                    _logger.LogDebug($"Restoring {count} messages for {smppOperator}");
                    if (count == 0) continue;

                    //smsMessages.AddRange();

                    List<SmsMessage> sm = Messages.Take(smppOperator, count).ToList();

                    await new BulksSmsManager().UpdateSendSmsById(sm.Select(x => Convert.ToInt64(x.RefId)).ToList(), "NEW")
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

        }
        #endregion


        #region [ Events ]
        #region [ Mesage Removed ]
        private void Messages_OnMessageRemoved(SmsMessage e)
        {
            //string query = $"UPDATE send_sms\r\n" +
            //    $"SET status = 'NEW'\r\n" +
            //    $"WHERE send_sms_id = '{ e.RefId }'";
        }
        #endregion
        #endregion
    }
}
