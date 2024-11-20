using SMSGateway.DataManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMPPClient
{
    internal class DeliveryReportWorker : BackgroundService
    {
        private readonly ILogger<DeliveryReportWorker> _logger;

        #region [ Constructor ]
        public DeliveryReportWorker(ILogger<DeliveryReportWorker> logger)
        {
            _logger = logger;
        }
        #endregion

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("DeliveryReportWorker :: Starting");
                try
                {
                    new BulksSmsManager().ProcessDeliveryReport().Wait();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                }
                _logger.LogDebug("DeliveryReportWorker :: Complete");
                try
                {
                    await Task.Delay(500, stoppingToken);
                }
                catch (TaskCanceledException exception)
                {
                    //_logger.LogCritical(exception, "TaskCanceledException Error", exception.Message);
                }

            }
            


        }
    }
}
