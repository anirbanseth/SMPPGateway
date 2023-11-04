using SMSGateway.SMSCClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMPPClient
{
    public class SmppConnectionManager
    {
        public static List<SMPPConnection> Connections = new List<SMPPConnection>();

        public static List<string> GetActiveOperators()
        {
            List<string> activeOperators = SmppConnectionManager
                .Connections.Where(x => x.CanSend && x.MC.TPS > 0)
                .Select(x => x.MC.Operator)
                .Distinct()
                .ToList();

            return activeOperators;
        }

        public static List<string> GetActiveOperatorsWithAvailableQueue()
        {
            List<string> activeOperators = SmppConnectionManager
                .Connections.Where(x => x.CanSend && x.MC.TPS > 0 && Messages.Count(x.MC.Operator) < x.MC.MaxQueue)
                .Select(x => x.MC.Operator)
                .Distinct()
                .ToList();
            return activeOperators;
        }
    }
}
