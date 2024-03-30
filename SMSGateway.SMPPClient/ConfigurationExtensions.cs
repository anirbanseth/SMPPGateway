using Newtonsoft.Json;
using Org.BouncyCastle.Bcpg;
using SMSGateway.Entity;
using SMSGateway.SMSCClient;
using SMSGateway.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace SMSGateway.SMPPClient
{
    public static class ConfigurationExtensions
    {
        public static T GetOptions<T>(this IConfiguration configuration)
            where T : class, new()
            => configuration.GetSection(typeof(T).Name).Get<T>() ?? new T();
    }

    public static class SmppDeliveryExtensions
    {
        public static SmppDeliveryData ToSmppDeliveryData (this SmppDelivery x)
        {
            SmppDeliveryData data = new SmppDeliveryData();
            data.ServiceType = x.ServiceType;
            data.SourceAddressNpi = x.SourceAddressNpi;
            data.SourceAddressTon = x.SourceAddressTon;
            data.SourceAddress = x.SourceAddress;
            data.DestinationAddressNpi = x.DestAddressNpi;
            data.DestinationAddressTon = x.DestAddressTon;
            data.DestAddress = x.DestAddress;
            data.EsmClass = x.EsmClass;
            data.PriorityFlag = x.PriorityFlag;
            data.ShortMessage = x.ShortMessage;
            data.AdditionalParameters.Add("id", x.Id);
            data.AdditionalParameters.Add("commandid", x.CommandId);
            data.AdditionalParameters.Add("userid", x.UserId);
            data.AdditionalParameters.Add("sourceaddress", x.SourceAddress);
            data.AdditionalParameters.Add("destaddress", x.DestAddress);
            data.AdditionalParameters.Add("messageid", x.MessageId);
            data.AdditionalParameters.Add("shortmessage", x.ShortMessage);
            //data.AdditionalParameters.Add("submittime", x.SubmitTime);
            data.AdditionalParameters.Add("messagestate", x.MessageState);
            data.AdditionalParameters.Add("messagestateupdatedon", x.MessageStateUpdatedOn);
            data.AdditionalParameters.Add("errorcode", x.ErrorCode);
            //data.AdditionalParameters.Add("retryindex", x.RetryIndex);
            data.AdditionalParameters.Add("retrycount", x.RetryCount);
            data.AdditionalParameters.Add("retryon", x.RetryOn);
            data.AdditionalParameters.Add("status", x.Status);
            //data.AdditionalParameters.Add("senton", x.SentOn);
            data.AdditionalParameters.Add("createdon", x.CreatedOn);
            data.AdditionalParameters.Add("updatedon", x.UpdatedOn);

            return data;
        }

        public static SmppDelivery ToSmppDelivery(this SmppDeliveryData data)
        {
            SmppDelivery smppDelivery = new SmppDelivery();
            smppDelivery.Id = data.AdditionalParameters.ContainsKey("id") ? (Guid)data.AdditionalParameters["id"] : new Guid();
            smppDelivery.CommandId = data.AdditionalParameters.ContainsKey("commandid") ? (Guid)data.AdditionalParameters["commandid"] : new Guid();
            smppDelivery.UserId = data.AdditionalParameters.ContainsKey("userid") ? (long)data.AdditionalParameters["userid"] : 0;
            smppDelivery.ServiceType = data.ServiceType;
            smppDelivery.SourceAddressNpi = data.SourceAddressNpi;
            smppDelivery.SourceAddressTon = data.SourceAddressTon;
            smppDelivery.SourceAddress = data.SourceAddress;
            smppDelivery.DestAddressNpi= data.DestinationAddressNpi;
            smppDelivery.DestAddressTon = data.DestinationAddressTon;
            smppDelivery.DestAddress = data.DestAddress;
            smppDelivery.EsmClass = data.EsmClass;
            smppDelivery.PriorityFlag = data.PriorityFlag;
            smppDelivery.MessageId = data.AdditionalParameters.ContainsKey("messageid") ? (string)data.AdditionalParameters["messageid"] : String.Empty;
            smppDelivery.ShortMessage = data.AdditionalParameters.ContainsKey("shortmessage") ? (string)data.AdditionalParameters["shortmessage"] : String.Empty;
            //smppDelivery.SubmitTime = data.AdditionalParameters.ContainsKey("submittime") ? (DateTime?)data.AdditionalParameters["submittime"] : (DateTime?)null;
            smppDelivery.MessageState = data.AdditionalParameters.ContainsKey("messagestate") ? (MessageState)data.AdditionalParameters["messagestate"] : (byte)0;
            smppDelivery.MessageStateUpdatedOn = data.AdditionalParameters.ContainsKey("messagestateupdatedon") ? (DateTime?)data.AdditionalParameters["messagestateupdatedon"] : (DateTime?)null;
            smppDelivery.ErrorCode = data.AdditionalParameters.ContainsKey("errorcode") ? (string)data.AdditionalParameters["errorcode"] : String.Empty;
            smppDelivery.RetryCount = data.AdditionalParameters.ContainsKey("retrycount") ? (byte)data.AdditionalParameters["retrycount"] : (byte) 0;
            smppDelivery.RetryOn = data.AdditionalParameters.ContainsKey("retryon") ? (DateTime)data.AdditionalParameters["retryon"] : DateTime.Now;
            smppDelivery.Status = data.AdditionalParameters.ContainsKey("status") ? (string)data.AdditionalParameters["status"] : String.Empty;
            //smppDelivery.SentOn = data.AdditionalParameters.ContainsKey("senton") ? (DateTime?)data.AdditionalParameters["senton"] : (DateTime?)null;
            smppDelivery.CreatedOn = data.AdditionalParameters.ContainsKey("createdon") ? (DateTime)data.AdditionalParameters["createdon"] : DateTime.Now;
            smppDelivery.UpdatedOn = data.AdditionalParameters.ContainsKey("updatedon") ? (DateTime)data.AdditionalParameters["updatedon"] : DateTime.Now;
            return smppDelivery;
        }

        public static bool IsValidAddress(this SmppUser user, IPAddress address)
        {
            if (String.IsNullOrEmpty(user.WhitelistedIps))
                return false;

            string[] ipList = JsonConvert.DeserializeObject<string[]>(user.WhitelistedIps);
            foreach (string ipText in ipList)
            {
                IPAddress ipAddress;
                IPNetwork2 ipNetwork;
                if (IPAddress.TryParse(ipText, out ipAddress))
                {
                    if (ipAddress.Equals(address))
                        return true;
                }
                else if (IPNetwork2.TryParse(ipText, out ipNetwork))
                {
                    if (ipNetwork.Contains(address))
                        return true;
                }
            }

            return false;
        }

        public static SmppSessionData ToSessionData(this SmppSession session)
        {
            SmppSessionData data = new SmppSessionData(); ;
            data.Id = session.Id;
            data.Address = session.Address;
            data.UserId = session.UserId;
            data.LastRecieved = session.LastRecieved;
            data.BindRequest = session.BindRequest;
            data.UnbindRequest = session.UnbindRequest;
            data.ReceivedCount = session.ReceivedCount;
            data.SentCount = session.SentCount;
            data.ValidFrom = session.ValidFrom;
            data.ValidTo = session.ValidTo;
            return data;
        }


        public static SmppSession ToSession(this SmppSessionData session)
        {
            SmppSession data = new SmppSession(); ;
            data.Id = session.Id;
            data.Address = session.Address;
            data.UserId = session.UserId;
            data.LastRecieved = session.LastRecieved;
            data.BindRequest = session.BindRequest;
            data.UnbindRequest = session.UnbindRequest;
            data.ReceivedCount = session.ReceivedCount;
            data.SentCount = session.SentCount;
            data.ValidFrom = session.ValidFrom;
            data.ValidTo = session.ValidTo;
            return data;
        }
    }
}
