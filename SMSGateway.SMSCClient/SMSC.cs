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
 * File Name: SMSC.cs
 * 
 * File Authors:
 * 		Balan Name, http://balan.name
 */

using Newtonsoft.Json;
using SMSGateway.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Threading;

namespace SMSGateway.SMSCClient
{
    public class SMSC
    {
        //[JsonProperty(PropertyName = "operator")]
        public string Operator { get; set; }
        public string Instance { get; set; }

        /// <summary>
        /// Host
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Port
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// SSL status
        /// </summary>
        public bool Secured { get; set; }

        /// <summary>
        /// System ID VARCHAR 16 MAX
        /// </summary>
        public string SystemId { get; set; }

        /// <summary>
        /// Password VARCHAR 9 MAX
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// System Type VARCHAR 13 MAX
        /// </summary>
        public string SystemType { get; set; }


        /// <summary>
        /// Addr Type Of Number (TON)
        /// </summary>
        public byte AddrTon { get; set; } = 0;

        /// <summary>
        /// Addr Numbering Plan Indicator (NPI)
        /// </summary>
        public byte AddrNpi { get; set; } = 0;

        /// <summary>
        /// Address Range VARCHAR 41 MAX
        /// </summary>
        public string AddressRange { get; set; } = "";
        //{
        //    get
        //    {
        //        return addressRange;
        //    }
        //    set
        //    {
        //        if (String.IsNullOrEmpty(value))
        //            addressRange = "";
        //        else
        //        {
        //            if (value.Length > 40)
        //                addressRange = value.Substring(40);
        //            else
        //                addressRange = value;
        //        }
        //    }
        //}

        private uint sequenceNumber;
        private byte messageIdentificationNumber = 0;
        //private string addressRange = "";
        public MessageEncoding DefaultEncoding { get; set; }

        public SMSC(int port, string systemId, string password = "", string systemType = "", bool secured = false, byte addrTon = 0, byte addrNpi = 0, string addressRange = "")
        {
            this.Port = port;

            if (systemId.Length > 15)
                this.SystemId = systemId.Substring(0, 15);
            else
                this.SystemId = systemId;

            if (password.Length > 8)
                this.Password = password.Substring(0, 8);
            else
                this.Password = password;

            if (systemType.Length > 12)
                this.SystemType = systemType.Substring(0, 8);
            else
                this.SystemType = systemType;

            this.Secured = secured;
            this.AddrTon = addrTon;
            this.AddrNpi = addrNpi;

            if (addressRange.Length > 40)
                this.AddressRange = addressRange.Substring(0, 8);
            else
                this.AddressRange = addressRange;

            Random random = new Random();

            uint fullRange = 0;
            do
            {
                uint thirtyBits = (uint)random.Next(1 << 30);
                uint twoBits = (uint)random.Next(1 << 2);
                fullRange = (thirtyBits << 2) | twoBits;
            }
            while (fullRange > KernelParameters.MaxSequenceNumber);

            this.sequenceNumber = fullRange;
            this.messageIdentificationNumber = (byte)random
                .Next(0, KernelParameters.MaxIdentificationNumber);
        }

        public SMSC(string host, int port, string systemId, string password = "", string systemType = "", bool secured = false, byte addrTon = 0, byte addrNpi = 0, string addressRange = "", MessageEncoding encoding = MessageEncoding.DEFAULT)
        {
            this.Host = host;
            this.Port = port;

            if (systemId.Length > 15)
                this.SystemId = systemId.Substring(0, 15);
            else
                this.SystemId = systemId;

            if (password.Length > 8)
                this.Password = password.Substring(0, 8);
            else
                this.Password = password;

            if (systemType.Length > 12)
                this.SystemType = systemType.Substring(0, 8);
            else
                this.SystemType = systemType;

            this.Secured = secured;
            this.AddrTon = addrTon;
            this.AddrNpi = addrNpi;

            if (addressRange.Length > 40)
                this.AddressRange = addressRange.Substring(0, 8);
            else
                this.AddressRange = addressRange;

            Random random = new Random();

            uint fullRange = 0;
            do
            {
                uint thirtyBits = (uint)random.Next(1 << 30);
                uint twoBits = (uint)random.Next(1 << 2);
                fullRange = (thirtyBits << 2) | twoBits;
            }
            while (fullRange > KernelParameters.MaxSequenceNumber);

            this.sequenceNumber = fullRange;
            this.messageIdentificationNumber = (byte)random
                .Next(0, KernelParameters.MaxIdentificationNumber);

            this.DefaultEncoding = encoding;
        }

        public SMSC(string opreator, string instance, string host, int port, string systemId, string password = "", string systemType = "", bool secured = false, byte addrTon = 0, byte addrNpi = 0, string addressRange = "", MessageEncoding encoding = MessageEncoding.DEFAULT)
            : this(host, port, systemId, password, systemType, secured, addrTon, addrNpi, addressRange, encoding)
        {
            this.Operator = Operator;
            this.Instance = instance;
        }

        public SMSC() { }

        //public string AddressRange
        //{
        //    get
        //    {
        //        return addressRange;
        //    }
        //    set
        //    {
        //        if (String.IsNullOrEmpty(value))
        //            addressRange = "";
        //        else
        //        {
        //            if (value.Length > 40)
        //                addressRange = value.Substring(40);
        //            else
        //                addressRange = value;
        //        }
        //    }
        //}//AddressRange

        public uint SequenceNumber
        {
            get
            {
                //lock (this)
                Monitor.Enter(this);
                try
                {
                    if (sequenceNumber == KernelParameters.MaxSequenceNumber)
                        sequenceNumber = 0;
                    else
                        sequenceNumber++;
                    return sequenceNumber;
                    //return Convert.Toint32(String.Format("{0}{1:00000000}", ApplicationParameter.ApplicationInstance, sequenceNumber));
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }
        }//SequenceNumber

        public uint LastSequenceNumber
        {
            get
            {
                //lock (this)
                Monitor.Enter(this);
                try
                {
                    return sequenceNumber;
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }
        }//LastSequenceNumber

        public byte MessageIdentificationNumber
        {
            get
            {
                //lock (this)
                Monitor.Enter(this);
                try
                {
                    if (messageIdentificationNumber == KernelParameters.MaxIdentificationNumber)
                        messageIdentificationNumber = 0;
                    else
                        messageIdentificationNumber++;
                    return messageIdentificationNumber;
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }
        }//MessageIdentificationNumber

        public int TPS { get; set; }
        public decimal DLTCost { get; set; }
        public decimal SubmitCost { get; set; }
        public string DeliveryDateFormat { get; set; }
        public int MaxQueue { get; set; }
        public int MaxRetry { get; set; }
    }

    //public class SMSCArray
    //{
    //    private ArrayList SMSCAr = new ArrayList();
    //    private int curSMSC = 0;

    //    public void AddSMSC(SMSC pSMSC)
    //    {
    //        //lock (this)
    //        Monitor.Enter(this);
    //        try
    //        {
    //            SMSCAr.Add(pSMSC);
    //        }
    //        finally
    //        {
    //            Monitor.Exit(this);
    //        }
    //    }//AddSMSC

    //    public void Clear()
    //    {
    //        //lock (this)
    //        Monitor.Enter(this);
    //        try
    //        {
    //            SMSCAr.Clear();
    //            curSMSC = 0;
    //        }
    //        finally
    //        {
    //            Monitor.Exit(this);
    //        }
    //    }//Clear

    //    public void NextSMSC()
    //    {
    //        //lock (this)
    //        Monitor.Enter(this);
    //        try
    //        {
    //            curSMSC++;
    //            if ((curSMSC + 1) > SMSCAr.Count)
    //                curSMSC = 0;
    //        }
    //        finally
    //        {
    //            Monitor.Exit(this);
    //        }
    //    }//AddSMSC


    //    public SMSC currentSMSC
    //    {
    //        get
    //        {
    //            SMSC mSMSC = null;
    //            try
    //            {
    //                //lock (this)
    //                Monitor.Enter(this);
    //                try
    //                {

    //                    if (SMSCAr.Count == 0)
    //                        return null;
    //                    if (curSMSC > (SMSCAr.Count - 1))
    //                    {
    //                        curSMSC = 0;
    //                    }
    //                    mSMSC = (SMSC)SMSCAr[curSMSC];
    //                }
    //                finally
    //                {
    //                    Monitor.Exit(this);
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //            }
    //            return mSMSC;
    //        }
    //    }//currentSMSC

    //    public bool HasItems
    //    {
    //        get
    //        {
    //            //lock (this)
    //            Monitor.Enter(this);
    //            try
    //            {
    //                if (SMSCAr.Count > 0)
    //                    return true;
    //                else
    //                    return false;
    //            }
    //            finally
    //            {
    //                Monitor.Exit(this);
    //            }
    //        }
    //    }//HasItems
    //}//SMSCArray


    //public class SMSCItem : ConfigurationElement
    //{
    //    [ConfigurationProperty("type", IsRequired = true)]
    //    public SMSCType Type
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return (SMSCType)Enum.Parse(typeof(SMSCType), this["type"].ToString());
    //            }
    //            catch
    //            {
    //                return SMSCType.Primary;
    //            }
    //        }
    //    }

    //    [ConfigurationProperty("name", IsRequired = true)]
    //    public string Name
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return this["name"] as string;
    //            }
    //            catch
    //            {
    //                return String.Empty;
    //            }
    //        }
    //    }//Description

    //    [ConfigurationProperty("vendor", IsRequired = true)]
    //    public string VendorName
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return this["vendor"] as string;
    //            }
    //            catch
    //            {
    //                return String.Empty;
    //            }
    //        }
    //    }//Description

    //    [ConfigurationProperty("host", IsRequired = true)]
    //    public string Host
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return this["host"] as string;
    //            }
    //            catch
    //            {
    //                return String.Empty;
    //            }
    //        }
    //    }//Host

    //    [ConfigurationProperty("port", IsRequired = true)]
    //    public int Port
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return Convert.ToInt32(this["port"]);
    //            }
    //            catch
    //            {
    //                return 0;
    //            }
    //        }
    //    }//Port

    //    [ConfigurationProperty("systemid", IsRequired = true)]
    //    public string SystemId
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return this["systemid"] as string;
    //            }
    //            catch
    //            {
    //                return String.Empty;
    //            }
    //        }
    //    }//SystemId

    //    [ConfigurationProperty("password", IsRequired = true)]
    //    public string Password
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return this["password"] as string;
    //            }
    //            catch
    //            {
    //                return String.Empty;
    //            }
    //        }
    //    }//Password

    //    [ConfigurationProperty("systemtype", IsRequired = true)]
    //    public string SystemType
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return this["systemtype"] as string;
    //            }
    //            catch
    //            {
    //                return String.Empty;
    //            }
    //        }
    //    }//SystemType

    //    [ConfigurationProperty("addrton", IsRequired = false)]
    //    public byte AddrTon
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return Convert.ToByte(this["addrTon"] as string);
    //            }
    //            catch
    //            {
    //                return 0;
    //            }
    //        }
    //    }//AddrTon

    //    [ConfigurationProperty("addrnpi", IsRequired = false)]
    //    public byte AddrNpi
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return Convert.ToByte(this["addrNpi"] as string);
    //            }
    //            catch
    //            {
    //                return 0;
    //            }
    //        }
    //    }//AddrNpi

    //    [ConfigurationProperty("addressrange", IsRequired = false)]
    //    public string AddressRange
    //    {
    //        get
    //        {
    //            try
    //            {
    //                return this["addressRange"] as string;
    //            }
    //            catch
    //            {
    //                return String.Empty;
    //            }
    //        }
    //    }//AddressRange

    //    [ConfigurationProperty("defaultencoding", IsRequired = true)]
    //    public MessageEncoding DefaultEncoding
    //    {
    //        get
    //        {
    //            MessageEncoding smscEncoding = MessageEncoding.DEFAULT;
    //            try
    //            {
    //                string encoding = this["defaultencoding"].ToString();
    //                Enum.TryParse(encoding, out smscEncoding);
    //                return smscEncoding;
    //            }
    //            catch
    //            {
    //                return smscEncoding;
    //            }
    //        }
    //    }//AddressRange

    //    [ConfigurationProperty("messageIdType", IsRequired = false)]
    //    public MessageIdType MessageIdType
    //    {
    //        get
    //        {
    //            MessageIdType messageIdType = MessageIdType.STR;
    //            try
    //            {

    //                string strMessageIdType = this["messageIdType"].ToString();
    //                Enum.TryParse(strMessageIdType, out messageIdType);
    //                return messageIdType;
    //            }
    //            catch (Exception ex)
    //            {
    //                return messageIdType;
    //            }
    //        }
    //    }

    //    public SMSC ToSMSC()
    //    {
    //        SMSC smsc = new SMSC();
    //        smsc.Type = this.Type;
    //        smsc.Name = this.Name;
    //        smsc.VendorName = this.VendorName;
    //        smsc.Host = this.Host;
    //        smsc.Port = this.Port;
    //        smsc.SystemId = this.SystemId;
    //        smsc.Password = this.Password;
    //        smsc.SystemType = this.SystemType;
    //        smsc.AddrTon = this.AddrTon;
    //        smsc.AddrNpi = this.AddrNpi;
    //        smsc.AddressRange = this.AddressRange;
    //        smsc.DefaultEncoding = this.DefaultEncoding;
    //        smsc.MessageIdType = this.MessageIdType;
    //        return smsc;
    //    }
    //}

    //public class SMSCCollection : ConfigurationElementCollection
    //{
    //    public SMSCItem this[int index]
    //    {
    //        get
    //        {
    //            return base.BaseGet(index) as SMSCItem;
    //        }
    //        set
    //        {
    //            if (base.BaseGet(index) != null)
    //            {
    //                base.BaseRemoveAt(index);
    //            }
    //            this.BaseAdd(index, value);
    //        }
    //    }

    //    public new SMSCItem this[string responseString]
    //    {
    //        get { return (SMSCItem)BaseGet(responseString); }
    //        set
    //        {
    //            if (BaseGet(responseString) != null)
    //            {
    //                BaseRemoveAt(BaseIndexOf(BaseGet(responseString)));
    //            }
    //            BaseAdd(value);
    //        }
    //    }

    //    protected override System.Configuration.ConfigurationElement CreateNewElement()
    //    {
    //        return new SMSCItem();
    //    }

    //    protected override object GetElementKey(System.Configuration.ConfigurationElement element)
    //    {
    //        return ((SMSCItem)element);//.Description;
    //    }
    //}

    //public class RegisterSMSCConfig : ConfigurationSection
    //{

    //    public static RegisterSMSCConfig GetConfig()
    //    {
    //        return (RegisterSMSCConfig)System.Configuration.ConfigurationManager.GetSection("RegisterSMSCConfig") ?? new RegisterSMSCConfig();
    //    }

    //    [System.Configuration.ConfigurationProperty("SMSCCollection")]
    //    [ConfigurationCollection(typeof(SMSCCollection), AddItemName = "SMSC")]
    //    public SMSCCollection SMSCCollection
    //    {
    //        get
    //        {
    //            object o = this["SMSCCollection"];
    //            return o as SMSCCollection;
    //        }
    //    }

    //}
}
