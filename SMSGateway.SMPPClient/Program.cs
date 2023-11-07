using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using SMSGateway.SMPPClient;
using System.Configuration;
using static Org.BouncyCastle.Math.EC.ECCurve;

//IConfiguration Configuration { get; set;  }

//Console.WriteLine($"Current Director : {Directory.GetCurrentDirectory()}");

IConfiguration Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("smppconfig.json", optional: true, reloadOnChange: true)
                .Build();

NLog.LogManager.Configuration = new NLogLoggingConfiguration(Configuration.GetSection("NLog"));

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<SmppWorker>();
        services.AddHostedService<DatabaseWorker>();
        services.AddHostedService<GCWorker>();
        services.AddHostedService<DeliveryReportWorker>();
        services.AddHostedService<DeliveryGenerateWorker>();
    })
    .ConfigureAppConfiguration(config => {
        config
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", true, true)
        .AddJsonFile("smppconfig.json", true, true);
    })
    .ConfigureLogging(loggingBuilder => {
        loggingBuilder.ClearProviders();
        loggingBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
        loggingBuilder.AddNLog(Configuration);

    })
    .Build();



SmppOptions options = Configuration.GetOptions<SmppOptions>();
switch (options.DatabaseSettings.DatabaseType?.ToLower())
{
    case "mysql":
        SMSGateway.DataManager.General.MySqlDataConnection.ConnectionString = options.DatabaseSettings.ConnectionString;
        break ;
    case "mssql":
        break;
    default:
        break;
}
Thread.Sleep(100);

host.Run();
