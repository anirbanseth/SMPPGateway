using SMSGateway.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMSCClient
{
    public class SmsEncoding
    {
        public static Encoding[] Encodings = new Encoding[] {
            new Mediaburst.Text.GSMEncoding(),
            Encoding.UTF8,
            null,
            null,
            null,
            null,
            null,
            null,
            Encoding.BigEndianUnicode
        };
        public static byte[] MaxTextLength = new byte[] { 160, 160, 0, 0, 0, 0, 0, 0, 140 };
        public static byte[] DataSize = new byte[] { 8, 8, 0, 0, 0, 0, 0, 0, 16 };
        public static byte[] SplitSize = new byte[] { 153, 153, 0, 0, 0, 0, 0, 0, 67 };
        public static byte[] DataBufferSize = new byte[] { 153, 153, 0, 0, 0, 0, 0, 0, 140 };
    }

    public class Helper
    {
        public static string ParseDeliveryReportId(MessageIdType msgIdType, string value)
        {
            string returnValue = value;
            try
            {
                switch (msgIdType)
                {
                    case MessageIdType.DEC_HEX:
                        returnValue = Int64.Parse(value, System.Globalization.NumberStyles.HexNumber).ToString();

                        break;
                    case MessageIdType.HEX_DEC:
                        returnValue = Int64.Parse(value).ToString("X");
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
            }
            return returnValue;
        }
    }
}
