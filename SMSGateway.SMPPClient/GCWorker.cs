using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMPPClient
{
    public class GCWorker : BackgroundService
    {
        private readonly ILogger<GCWorker> _logger;

        #region [ Constructor ]
        public GCWorker(ILogger<GCWorker> logger)
        {
            _logger = logger;
        }
        #endregion
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("GCWorker :: Starting");
                GC.WaitForPendingFinalizers();
                GC.Collect();
                _logger.LogDebug("GCWorker :: Complete");
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
    }
}
