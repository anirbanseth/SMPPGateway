using SMSGateway.SMSCClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMPPClient
{
    public class SmppOptions
    {
        public SMSC[] Providers { get; set; }

        public DatabaseSettings DatabaseSettings { get; set; }
    }

    public class DatabaseSettings
    {
        public string DatabaseType { get; set;}
        public string ConnectionString { get; set;}
    }
}
