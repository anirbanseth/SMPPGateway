using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMPPClient
{
    public static class ConfigurationExtensions
    {
        public static T GetOptions<T>(this IConfiguration configuration)
            where T : class, new()
            => configuration.GetSection(typeof(T).Name).Get<T>() ?? new T();
    }
}
