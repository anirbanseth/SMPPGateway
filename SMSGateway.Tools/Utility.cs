/*
 * EasySMPP - SMPP protocol library for fast and easy
 * SMSC(Short Message Service Centre) client development
 * even for non-telecom guys.
 * 
 * Easy to use classes covers all needed functionality
 * for SMS applications developers and Content Providers.
 * 
 * Written for .NET 2.0 in C#
 * 
 * Copyright (C) 2006 Balan Andrei, http://balan.name
 * 
 * Licensed under the terms of the GNU Lesser General Public License:
 * 		http://www.opensource.org/licenses/lgpl-license.php
 * 
 * For further information visit:
 * 		http://easysmpp.sf.net/
 * 
 * 
 * "Support Open Source software. What about a donation today?"
 *
 * 
 * File Name: Tools.cs
 * 
 * File Authors:
 * 		Balan Name, http://balan.name
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SMSGateway.Tools
{
    public class Utility
    {
        public static void CopyIntToArray(int x, byte[] ar, int pos)
        {
            byte[] arTmp;
            ConvertIntToArray(x, out arTmp);
            Array.Copy(arTmp, 0, ar, pos, 4);
        }

        public static void ConvertIntToArray(int x, out byte[] ar)
        {
            ar = new byte[4];
            ar[3] = Convert.ToByte(x & 0xFF);
            ar[2] = Convert.ToByte((x >> 8) & 0xFF);
            ar[1] = Convert.ToByte((x >> 16) & 0xFF);
            ar[0] = Convert.ToByte((x >> 24) & 0xFF);
        }

        public static void CopyIntToArray(uint x, byte[] ar, int pos)
        {
            byte[] arTmp;
            ConvertIntToArray(x, out arTmp);
            Array.Copy(arTmp, 0, ar, pos, 4);
        }

        public static void CopyShortToArray(short x, byte[] ar, int pos)
        {
            byte[] arTmp;
            ConvertShortToArray(x, out arTmp);
            Array.Copy(arTmp, 0, ar, pos, 2);
        }

        public static void ConvertShortToArray(short x, out byte[] ar)
        {
            ar = new byte[2];
            ar[1] = Convert.ToByte(x & 0xFF);
            ar[0] = Convert.ToByte((x >> 8) & 0xFF);
            //ar[1] = Convert.ToByte((x >> 16) & 0xFF);
            //ar[0] = Convert.ToByte((x >> 24) & 0xFF);
        }
        //public static void CopyShortToArray(ushort x, byte[] ar, int pos)
        //{
        //    byte[] arTmp;
        //    ConvertShortToArray(x, out arTmp);
        //    Array.Copy(arTmp, 0, ar, pos, 4);
        //}

        //public static void ConvertShortToArray(ushort x, out byte[] ar)
        //{
        //    ar = new byte[4];
        //    ar[3] = Convert.ToByte(x & 0xFF);
        //    ar[2] = Convert.ToByte((x >> 8) & 0xFF);
        //    ar[1] = Convert.ToByte((x >> 16) & 0xFF);
        //    ar[0] = Convert.ToByte((x >> 24) & 0xFF);
        //}

        public static string ConvertIntToHexString(int x)
        {
            string resp;
            resp = "0x";
            resp += GetHexByte(Convert.ToByte((x >> 24) & 0xFF));
            resp += GetHexByte(Convert.ToByte((x >> 16) & 0xFF));
            resp += GetHexByte(Convert.ToByte((x >> 8) & 0xFF));
            resp += GetHexByte(Convert.ToByte(x & 0xFF));
            return resp;

        }
        public static string ConvertUIntToHexString(uint x)
        {
            string resp;
            resp = "0x";
            resp += GetHexByte(Convert.ToByte((x >> 24) & 0xFF));
            resp += GetHexByte(Convert.ToByte((x >> 16) & 0xFF));
            resp += GetHexByte(Convert.ToByte((x >> 8) & 0xFF));
            resp += GetHexByte(Convert.ToByte(x & 0xFF));
            return resp;

        }
        public static void ConvertIntToArray(uint x, out byte[] ar)
        {
            ar = new byte[4];
            ar[3] = Convert.ToByte(x & 0xFF);
            ar[2] = Convert.ToByte((x >> 8) & 0xFF);
            ar[1] = Convert.ToByte((x >> 16) & 0xFF);
            ar[0] = Convert.ToByte((x >> 24) & 0xFF);
        }

        public static string ConvertArrayToHexString(byte[] ar, int len)
        {
            int i;
            string sTmp;
            string HEX = "0123456789ABCDEF";
            sTmp = "";
            for (i = 0; i < len; i++)
            {
                sTmp = sTmp + HEX[(ar[i] >> 4) & 0x0F];
                sTmp = sTmp + HEX[ar[i] & 0x0F];
            }
            return sTmp;
        }
        public static string ConvertArrayToString(byte[] ar, int len)
        {
            string sTmp = "";
            try
            {
                sTmp = Encoding.ASCII.GetString(ar, 0, len);
            }
            catch (Exception ex)
            {
            }
            return sTmp;
        }
        public static string ConvertArrayToUnicodeString(byte[] ar, int len)
        {
            string sTmp = "";
            try
            {
                sTmp = Encoding.BigEndianUnicode.GetString(ar, 0, len);
            }
            catch (Exception ex)
            {
            }
            return sTmp;
        }
        public static string GetHexFromByte(byte a)
        {
            string sTmp;
            string HEX = "0123456789ABCDEF";
            sTmp = "0x";
            sTmp = sTmp + HEX[(a >> 4) & 0x0F];
            sTmp = sTmp + HEX[a & 0x0F];
            return sTmp;
        }
        public static string GetHexByte(byte a)
        {
            string sTmp;
            string HEX = "0123456789ABCDEF";
            sTmp = "";
            sTmp = sTmp + HEX[(a >> 4) & 0x0F];
            sTmp = sTmp + HEX[a & 0x0F];
            return sTmp;
        }
        public static byte[] ConvertHexStringToByteArray(string str)
        {
            if ((str.Length & 1) == 1)
                str += "0";
            int i, j, n;
            n = str.Length;
            byte a;
            byte[] x = new byte[n / 2];
            for (i = 0, j = 0; i < n; i++, j++)
            {
                a = getHexVal(str[i]);
                a <<= 4;
                a = Convert.ToByte(a | (getHexVal(str[i + 1]) & 0x0F));
                x[j] = a;
                i++;
            }
            return x;
        }
        public static byte[] ConvertStringToByteArray(string str)
        {
            int i, n;
            n = str.Length;
            byte[] x = new byte[n];
            for (i = 0; i < n; i++)
            {
                x[i] = (byte)str[i];
            }
            return x;
        }
        public static byte getHexVal(char ch)
        {
            byte b;
            b = 0;
            switch (ch)
            {
                case '0':
                    b = 0;
                    break;
                case '1':
                    b = 1;
                    break;
                case '2':
                    b = 2;
                    break;
                case '3':
                    b = 3;
                    break;
                case '4':
                    b = 4;
                    break;
                case '5':
                    b = 5;
                    break;
                case '6':
                    b = 6;
                    break;
                case '7':
                    b = 7;
                    break;
                case '8':
                    b = 8;
                    break;
                case '9':
                    b = 9;
                    break;
                case 'A':
                    b = 10;
                    break;
                case 'B':
                    b = 11;
                    break;
                case 'C':
                    b = 12;
                    break;
                case 'D':
                    b = 13;
                    break;
                case 'E':
                    b = 14;
                    break;
                case 'F':
                    b = 15;
                    break;
                case 'a':
                    b = 10;
                    break;
                case 'b':
                    b = 11;
                    break;
                case 'c':
                    b = 12;
                    break;
                case 'd':
                    b = 13;
                    break;
                case 'e':
                    b = 14;
                    break;
                case 'f':
                    b = 15;
                    break;
            }
            return b;
        }
        public static string ConvertNullEndArrayToHexString(byte[] ar, int len)
        {
            int i;
            string sTmp;
            string HEX = "0123456789ABCDEF";
            sTmp = "";
            for (i = 0; i < len; i++)
            {
                if (ar[i] == 0x00)
                    break;
                sTmp = sTmp + HEX[(ar[i] >> 4) & 0x0F];
                sTmp = sTmp + HEX[ar[i] & 0x0F];
            }
            return sTmp;
        }

        public static bool Get2ByteIntFromArray(byte[] ar, int pos, int length, out int res)
        {
            res = 0;
            bool result = false;
            int ps = pos;
            if (ps < length)
                res = ar[ps];
            else
                return false;
            res <<= 8;
            ps++;
            if (ps < length)
            {
                res |= ar[ps];
                result = true;
            }
            return result;
        }
        public static string GetString(string inpString, int maxLen, string defValue)
        {
            if (inpString == null)
                return defValue;
            if (inpString.Length > maxLen)
                return inpString.Substring(0, maxLen);
            return inpString;
        }
        public static string GetString(string inpString, string defValue)
        {
            if (inpString == null)
                return defValue;
            return inpString;
        }
        public static string GetDateString(DateTime pTime)
        {
            if (pTime == DateTime.MinValue)
                return "";
            return pTime.ToString("yyMMddHHmmss000R");
        }
        public static DateTime? ParseDateString(String str)
        {
            DateTime dt;

            if (String.IsNullOrEmpty(str))
                return null;
            else if (
                DateTime.TryParseExact(
                            str,
                            "yyMMddHHmmss000R",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out dt)
            )
                return dt;
            else
                return null;
        }
        public static byte GetDataCoding(string text)
        {
            if (UTF2Endian(text).Length != ASCII2Endian(text).Length)
            {
                return 8;
            }
            else
            {
                return 0;
            }
        }

        public static string UTF2Endian(string s)
        {

            Encoding ui = Encoding.BigEndianUnicode;

            Encoding u8 = Encoding.UTF8;

            return ui.GetString(u8.GetBytes(s));

        } // u2i

        public static string Endian2UTF(string s)
        {

            Encoding ui = Encoding.BigEndianUnicode;

            Encoding u8 = Encoding.UTF8;

            return u8.GetString(ui.GetBytes(s));

        } // i2u


        public static string ASCII2Endian(string s)
        {

            Encoding ui = Encoding.BigEndianUnicode;

            Encoding a = Encoding.ASCII;

            return ui.GetString(a.GetBytes(s));

        } // a2i

        public static string Endian2ASCII(string s)
        {

            Encoding ui = Encoding.BigEndianUnicode;

            Encoding a = Encoding.ASCII;

            return a.GetString(ui.GetBytes(s));

        } // i2a

        public static bool IsDigital(string s)
        {

            Regex re = new Regex(@"^[\d]+$");

            return re.Match(s).Success;

        } // IsDigital

        public static string GetRandomString(int Len)
        {
            string ret = "";
            int i;
            Random rnd = new Random(unchecked((int)DateTime.Now.Ticks));
            for (i = 0; i < Len; i++)
            {
                ret += Convert.ToString(rnd.Next(9));
            }
            return ret;
        }//GetRandomString
        public static string getBetween(string strSource, string strStart, string strEnd)
        {
            int Start, End;
            if (strSource.Contains(strStart) && strSource.Contains(strEnd))
            {
                Start = strSource.IndexOf(strStart, 0) + strStart.Length;
                End = strSource.IndexOf(strEnd, Start);
                return strSource.Substring(Start, End - Start);
            }
            else
            {
                return "";
            }
        }

        public static bool ParseDelivertReportShortMessage(byte[] ar, int length, out Dictionary<string, string> list)
        {
            try
            {
                string arString = ConvertArrayToString(ar, length);
                ParseDelivertReportShortMessage(arString, out list);
                return true;
            }
            catch
            {
                list = null;
                return false;
            }
        }


        public static bool ParseDelivertReportShortMessage(string arString, out Dictionary<string, string> list)
        {
            try
            {
                //List<KeyValuePair<string, string>> 
                string[] tlvKeys = { "id", "sub", "dlvrd", "submit date", "done date", "stat", "err", "text" };
                list = new Dictionary<string, string>();
                //string arString = ConvertArrayToString(ar, length);
                int length = arString.Length;
                int pos = 0;
                while (pos < length)
                {
                    int index = arString.IndexOf(':', pos);
                    string key = arString.Substring(pos, index - pos);

                    int minIndex = length;
                    foreach (string tlvKey in tlvKeys)
                    {
                        int keyIndex = arString.IndexOf(tlvKey, index);
                        if (keyIndex > 0 && keyIndex < minIndex)
                            minIndex = keyIndex;
                    }
                    string value = arString.Substring(index + 1, minIndex - index - 1).Trim();
                    list.Add(key, value);
                    pos = minIndex;
                };
                return true;
            }
            catch
            {
                list = null;
                return false;
            }
        }


        public static bool GetKeyValueFromTextString(string textString, out Dictionary<string, string> list)
        {
            try
            {
                //List<KeyValuePair<string, string>> 
                string[] tlvKeys = { "id", "sub", "dlvrd", "submit date", "done date", "stat", "err", "text" };
                list = new Dictionary<string, string>();
                string arString = textString;
                int pos = 0;
                while (pos < textString.Length)
                {
                    int index = arString.IndexOf(':', pos);
                    string key = arString.Substring(pos, index - pos);

                    int minIndex = textString.Length;
                    foreach (string tlvKey in tlvKeys)
                    {
                        int keyIndex = arString.IndexOf(tlvKey, index);
                        if (keyIndex > 0 && keyIndex < minIndex)
                            minIndex = keyIndex;
                    }
                    string value = arString.Substring(index + 1, minIndex - index - 1).Trim();
                    list.Add(key, value);
                    pos = minIndex;
                };
                return true;
            }
            catch
            {
                list = null;
                return false;
            }
        }

        public static string RemoveApostropy(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return String.Empty;
            }
            else
            {
                text = text.Replace("'", "''");
                return text;
            }

        }

        public static bool HasNonASCIIChars(string str)
        {
            return (System.Text.Encoding.UTF8.GetByteCount(str) != str.Length);
        }

        public static string MessageDeliveryStatus(int index)
        {
            try
            {
                string[] statusName = new string[] {
                    "SCHEDULED",
                    "ENROUTE",
                    "DELIVERED",
                    "EXPIRED",
                    "DELETED",
                    "UNDELIVERABLE",
                    "ACCEPTED",
                    "UNKNOWN",
                    "REJECTED",
                    "SKIPPED"
                };

                return statusName[index];
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public static byte MessageDeliveryStatus(string status)
        {
            try
            {
                Dictionary<string, byte> statuses = new Dictionary<string, byte>();
                statuses.Add("SCHEDULED", 0);
                statuses.Add("SCHEDUL", 0);
                statuses.Add("ENROUTE", 1);
                statuses.Add("EN_ROUTE", 1);
                statuses.Add("EN_ROUT", 1);
                statuses.Add("DELIVERED", 2);
                statuses.Add("DELIVER", 2);
                statuses.Add("DELIVRD", 2);
                statuses.Add("EXPIRED", 3);
                statuses.Add("DELETED", 4);
                statuses.Add("UNDELIVERABLE", 5);
                statuses.Add("UNDELIV", 5);
                statuses.Add("ACCEPTED", 6);
                statuses.Add("UNKNOWN", 7);
                statuses.Add("REJECTD", 8);
                statuses.Add("REJECTED", 8);
                statuses.Add("SKIPPED", 9);

                //string[] keys = statuses.Keys.ToArray();
                //foreach (string key in keys)
                //{
                //    if (key.Length > 7)
                //    {
                //        statuses.Add(key.Substring(0, 7), statuses[key]);
                //    }
                //}
                return statuses[status];
            }
            catch (Exception ex)
            {
                return 255;
            }
        }

        public static Dictionary<string, string> ParseDeliveryMessageText(string short_message)
        {
            string[] array = short_message.Split(new string[] { "id", "submit date", "done date", "sub", "dlvrd", "stat", "err", "text" }, StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            foreach (string s in array)
            {
                string key = short_message.Substring(0, short_message.IndexOf(s));
                string val = s.Substring(1).Trim();
                short_message = short_message.Substring(key.Length + val.Length + 1).Trim();
                dictionary.Add(key, val);
            }
            return dictionary;
        }

        public static long GetMemorySize(object obj)
        {
            //try
            //{
                //using (Stream s = new MemoryStream())
                //{
                //    BinaryFormatter bf = new BinaryFormatter();
                //    bf.Serialize(s, obj);
                //    return s.Length;
                //}
            //}
            //catch (Exception ex)
            //{

            //    throw;
            //}

            // need working
            return Marshal.SizeOf(obj);
        }

        public static async Task<long> GetMemorySizeAsync(object obj)
        {
            return await Task.Run(() => { return GetMemorySize(obj); });
        }


        public static byte[] GetBytes(byte dataCoding, string message)
        {
            byte[] messageBytes = null;
            Encoding encoding;
            int len = message.Length;

            switch (dataCoding)
            {
                case 0b00000000: // MC default
                    //messageText = Encoding.ASCII.GetString(ar, 0, len);

                    Encoding gsmEnc = new Mediaburst.Text.GSMEncoding();
                    Encoding utf8Enc = new System.Text.UTF8Encoding();

                    byte[] utf8Bytes = Encoding.ASCII.GetBytes(message);
                    byte[] gsmBytes = Encoding.Convert(utf8Enc, gsmEnc, utf8Bytes);
                    messageBytes = gsmBytes;
                    break;
                case 0b00000001: //IA5 (CCITT T.50)/ASCII (ANSI b3.4)
                    messageBytes = Encoding.ASCII.GetBytes(message);
                    break;
                case 0b00000010: //Octet unspecified (8-bit binary)
                    break;
                case 0b00000011: // Latin 1 (ISO-8859-1)
                    encoding = Encoding.GetEncoding("ISO-8859-1");
                    messageBytes = encoding.GetBytes(message);
                    break;
                case 0b00000100: // Octet unspecified (8-bit binary)
                    break;
                case 0b00000101: // JIS (b 0208-1990)
                    break;
                case 0b00000110: // Cyrillic (ISO-8859-5)
                    encoding = Encoding.GetEncoding("ISO-8859-5");
                    messageBytes = encoding.GetBytes(message);
                    break;
                case 0b00000111: // Latin/Hebrew (ISO-8859-8)
                    encoding = Encoding.GetEncoding("ISO-8859-8");
                    messageBytes = encoding.GetBytes(message);
                    break;
                case 0b00001000: // UCS2 (ISO/IEC-10646)
                    messageBytes = Encoding.BigEndianUnicode.GetBytes(message);
                    break;
                case 0b00001001: // Pictogram Encoding
                    break;
                case 0b00001010: // ISO-2022-JP (Music Codes)
                    break;
                case 0b00001011: // Reserved
                    break;
                case 0b00001100: // Reserved
                    break;
                case 0b00001101: // Ebtended Kanji JIS (b 0212-1990)
                    break;
                case 0b00001110: // KS C 5601
                    break;
                default:
                    if (0b00001111 <= dataCoding && dataCoding <= 0b10111111)
                    {
                        // reserved
                    }
                    else if (0b11000000 <= dataCoding && dataCoding <= 0b11001111)
                    {
                        // GSM MWI control - see [GSM 03.38]
                    }
                    else if (0b11010000 <= dataCoding && dataCoding <= 0b11011111)
                    {
                        // GSM MWI control - see [GSM 03.38]
                    }
                    else if (0b11100000 <= dataCoding && dataCoding <= 0b11101111)
                    {
                        // Reserved
                    }
                    else if (0b11110000 <= dataCoding && dataCoding <= 0b11111111)
                    {
                        // GSM message class control - see [GSM 03.38]
                    }

                    break;

            }

            return messageBytes;
        }
        public static string GetString(byte dataCoding, byte[] ar, int len)
        {
            string messageText = String.Empty;
            Encoding encoding;

            switch (dataCoding)
            {
                case 0b00000000: // MC default
                    //messageText = Encoding.ASCII.GetString(ar, 0, len);

                    Encoding gsmEnc = new Mediaburst.Text.GSMEncoding();
                    Encoding utf8Enc = new System.Text.UTF8Encoding();

                    //byte[] gsmBytes = utf8Enc.GetBytes(body);
                    byte[] gsmBytes = new byte[len];
                    Array.Copy(ar, 0, gsmBytes, 0, len);
                    byte[] utf8Bytes = Encoding.Convert(gsmEnc, utf8Enc, gsmBytes);
                    messageText = utf8Enc.GetString(utf8Bytes);

                    break;
                case 0b00000001: //IA5 (CCITT T.50)/ASCII (ANSI b3.4)
                    messageText = Encoding.ASCII.GetString(ar, 0, len);
                    break;
                case 0b00000010: //Octet unspecified (8-bit binary)
                    break;
                case 0b00000011: // Latin 1 (ISO-8859-1)
                    encoding = Encoding.GetEncoding("ISO-8859-1");
                    messageText = encoding.GetString(ar, 0, len);
                    break;
                case 0b00000100: // Octet unspecified (8-bit binary)
                    break;
                case 0b00000101: // JIS (b 0208-1990)
                    break;
                case 0b00000110: // Cyrillic (ISO-8859-5)
                    encoding = Encoding.GetEncoding("ISO-8859-5");
                    messageText = encoding.GetString(ar, 0, len);
                    break;
                case 0b00000111: // Latin/Hebrew (ISO-8859-8)
                    encoding = Encoding.GetEncoding("ISO-8859-8");
                    messageText = encoding.GetString(ar, 0, len);
                    break;
                case 0b00001000: // UCS2 (ISO/IEC-10646)
                    messageText = Encoding.BigEndianUnicode.GetString(ar, 0, len);
                    break;
                case 0b00001001: // Pictogram Encoding
                    break;
                case 0b00001010: // ISO-2022-JP (Music Codes)
                    break;
                case 0b00001011: // Reserved
                    break;
                case 0b00001100: // Reserved
                    break;
                case 0b00001101: // Ebtended Kanji JIS (b 0212-1990)
                    break;
                case 0b00001110: // KS C 5601
                    break;
                default:
                    if (0b00001111 <= dataCoding && dataCoding <= 0b10111111)
                    {
                        // reserved
                    }
                    else if (0b11000000 <= dataCoding && dataCoding <= 0b11001111)
                    {
                        // GSM MWI control - see [GSM 03.38]
                    }
                    else if (0b11010000 <= dataCoding && dataCoding <= 0b11011111)
                    {
                        // GSM MWI control - see [GSM 03.38]
                    }
                    else if (0b11100000 <= dataCoding && dataCoding <= 0b11101111)
                    {
                        // Reserved
                    }
                    else if (0b11110000 <= dataCoding && dataCoding <= 0b11111111)
                    {
                        // GSM message class control - see [GSM 03.38]
                    }

                    break;

            }

            return messageText;
        }
    }
}
