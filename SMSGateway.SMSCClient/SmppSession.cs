﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.SMSCClient
{
    public class SmppSessionData
    {
        public Guid Id { get; set; }
        public string Address { get; set; }
        public long UserId { get; set; }
        public DateTime? LastRecieved { get; set; }
        public DateTime? BindRequest { get; set; }
        public DateTime? UnbindRequest { get; set; }
        public long ReceivedCount { get; set; }
        public long SentCount { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public uint TPS { get; set; }
        public Dictionary<string, object> AdditionalParamers {  get; set; }
        public decimal SmsCost { get; set; }
        public decimal DltCharge { get; set; }

        public SmppSessionData()
        {
            ValidFrom = DateTime.Now;
            ValidTo = DateTime.MaxValue;
            AdditionalParamers = new Dictionary<string, object>();
        }

        //public SmppSession(DateTime validFrom)
        //{
        //    ValidForm = validFrom;
        //    ValidTo = DateTime.MaxValue;
        //}
    }
}
