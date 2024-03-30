using Org.BouncyCastle.Crypto.Agreement.Srp;
using SMSGateway.SMSCClient;
using SMSGateway.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace SMSGateway.SMPPClient
{
    public class SmppServerWorker : BackgroundService
    {
        private readonly ILogger<SmppServerWorker> _logger;
        //public static List<SMPPConnection> connections = new List<SMPPConnection>();
        IConfiguration Configuration = null;
        SmppOptions options = null;
        SortedList activeConnections = SortedList.Synchronized(new SortedList());
        List<SmppServer> servers = new List<SmppServer>();

        #region [ Constructor ]
        public SmppServerWorker(ILogger<SmppServerWorker> logger)
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

            foreach (SmscServerOptions item in options.Servers)
            {
                SmppServer server = new SmppServer(
                    new SMSC(
                        port: item.Port,
                        systemId: item.Name,
                        secured : false//item.Secured
                    )
                );
                server.OnLog += Server_OnLog;   
                server.Start();
                servers.Add( server );
            }

            await base.StartAsync(cancellationToken);
        }

        private void Server_OnLog(SMPPConnection connection, LogEventArgs e)
        {
            switch (e.LogType)
            {
                case Tools.LogType.Pdu:
                    _logger.LogTrace(e.Message);
                    break;
                case Tools.LogType.Steps:
                    _logger.LogDebug(e.Message);
                    break;
                case Tools.LogType.Warning:
                    _logger.LogWarning(e.Message);
                    break;
                case Tools.LogType.Error:
                    _logger.LogError(e.Message);
                    break;
                case Tools.LogType.Exceptions:
                    _logger.LogCritical (e.Message);
                    break;
                case Tools.LogType.Information:
                    _logger.LogInformation(e.Message);
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region [ Execute Async ]
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogDebug("SmppServerWorker_ExecuteAsync :: Start");

            _logger.LogDebug("SmppServerWorker_ExecuteAsync :: Stopping");
            //Stop();
            _logger.LogDebug("SmppServerWorker_ExecuteAsync :: Stopped");
        }
        #endregion


        #region [ Stop Async ]
        public override async Task StopAsync(CancellationToken cancellationToken)
        {

            for (int i = 0; i < servers.Count; i++)
            {
                servers[i].Stop();
            }
            //jobsProcessor.Stop();
            GC.WaitForFullGCComplete();
            await base.StopAsync(cancellationToken);
        }
        #endregion
    }
}
