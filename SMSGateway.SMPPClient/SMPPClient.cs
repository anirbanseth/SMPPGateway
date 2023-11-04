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
 * File Name: SMPPClient.cs
 * 
 * File Authors:
 * 		Balan Name, http://balan.name
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
//using EasySMPP.DataManager;
//using EasySMPP.Tools;
using SMSGateway.Tools;
using SMSGateway.SMSCClient;
using Microsoft.Extensions.Logging;

namespace SMSGateway.SMPPClient
{
    /// <summary>
    /// Summary description for SMPPClient.
    /// </summary>
    public class SMSCClient
    {

        #region Private variables
        private Socket clientSocket;

        private int connectionState;

        private DateTime enquireLinkSendTime;
        private DateTime enquireLinkResponseTime;
        private DateTime lastSeenConnected;
        private DateTime lastPacketSentTime;

        private Timer enquireLinkTimer;
        // Commented by Anirban Seth On 21-Sep-2022
        //private int undeliveredMessages = 0;

        //private SMSCArray smscArray = new SMSCArray();
        SMSC smsc = null;

        private byte[] mbResponse = new byte[KernelParameters.MaxBufferSize];
        private int mPos = 0;
        private int mLen = 0;

        private bool mustBeConnected;

        private int logLevel = 0;
        private byte askDeliveryReceipt = KernelParameters.AskDeliveryReceipt;
        private bool splitLongText = KernelParameters.SplitLongText;
        private int nationalNumberLength = KernelParameters.NationalNumberLength;
        private bool useEnquireLink = KernelParameters.UseEnquireLink;
        private int enquireLinkTimeout = KernelParameters.EnquireLinkTimeout;
        private int reconnectTimeout = KernelParameters.ReconnectTimeout;

        private SortedList sarMessages = SortedList.Synchronized(new SortedList());
        //private string gToNo = "";
        //private string gIDNo = "";
        #endregion Private variables

        #region Public Functions

        public SMSCClient(SMSC smsc)
        {
            this.smsc = smsc;
            connectionState = ConnectionStates.SMPP_SOCKET_DISCONNECTED;

            enquireLinkSendTime = DateTime.Now;
            enquireLinkResponseTime = enquireLinkSendTime.AddSeconds(1);
            lastSeenConnected = DateTime.Now;
            lastPacketSentTime = DateTime.MaxValue;

            mustBeConnected = false;

            TimerCallback timerDelegate = new TimerCallback(checkSystemIntegrity);
            enquireLinkTimer = new Timer(timerDelegate, null, enquireLinkTimeout, enquireLinkTimeout);

        }//SMPPClient

        public void Connect()
        {
            try
            {
                mustBeConnected = true;
                connectToSMSC();
                //unBind();
                //disconnectSocket();
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "connectToSMSC | " + ex.ToString());
            }
        }//connectToSMSC

        public void Disconnect()
        {
            try
            {
                mustBeConnected = false;
                unBind();
                Thread.Sleep(10000);
                //disconnectSocket();
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "DisconnectFromSMSC | " + ex.ToString());
            }
        }//DisconnectFromSMSC

        //public void ClearSMSC()
        //{
        //    try
        //    {
        //        smscArray.Clear();
        //    }
        //    catch (Exception ex)
        //    {
        //        logMessage(LogLevels.LogExceptions, "AddSMSC | " + ex.ToString());
        //    }

        //}//ClearSMSC

        public void AddSMSC(SMSC mSMSC)
        {
            try
            {
                smscArray.AddSMSC(mSMSC);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "AddSMSC | " + ex.ToString());
            }

        }//AddSMSC

        #region Send Functions

        public void Send(byte[] data, int n)
        {
            try
            {
                lastPacketSentTime = DateTime.Now;
                logMessage(LogLevels.LogPdu, "Sending PDU : " + Utility.ConvertArrayToHexString(data, n));
                //clientSocket.SendAsync(
                clientSocket.BeginSend(data, 0, n, 0, new AsyncCallback(sendCallback), clientSocket);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "Send | " + ex.ToString());
            }
        }//Send

        public int SubmitSM(byte sourceAddressTon, byte sourceAddressNpi, string sourceAddress,
                            byte destinationAddressTon, byte destinationAddressNpi, string destinationAddress,
                            byte esmClass, byte protocolId, byte priorityFlag,
                            DateTime sheduleDeliveryTime, DateTime validityPeriod, byte registeredDelivery,
                            byte replaceIfPresentFlag, byte dataCoding, byte smDefaultMsgId,
                            byte[] message, string peId, string templateId, string tmId)
        {
            try
            {
                byte[] _destination_addr;
                byte[] _source_addr;
                byte[] _SUBMIT_SM_PDU;
                byte[] _shedule_delivery_time;
                byte[] _validity_period;
                int _sequence_number;
                int pos;
                byte _sm_length;


                _SUBMIT_SM_PDU = new byte[KernelParameters.MaxPduSize];

                ////////////////////////////////////////////////////////////////////////////////////////////////
                /// Start filling PDU						

                Utility.CopyIntToArray(0x00000004, _SUBMIT_SM_PDU, 4); //command_id
                _sequence_number = smsc.SequenceNumber;
                Utility.CopyIntToArray(_sequence_number, _SUBMIT_SM_PDU, 12); //sequence_number
                pos = 16;
                _SUBMIT_SM_PDU[pos] = 0x00; //service_type
                pos += 1;
                _SUBMIT_SM_PDU[pos] = sourceAddressTon; //source_addr_ton
                pos += 1;
                _SUBMIT_SM_PDU[pos] = sourceAddressNpi; // source_addr_npi
                pos += 1;
                _source_addr = Utility.ConvertStringToByteArray(Utility.GetString(sourceAddress, 20, "")); //source_addr
                Array.Copy(_source_addr, 0, _SUBMIT_SM_PDU, pos, _source_addr.Length);
                pos += _source_addr.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = destinationAddressTon; // dest_addr_ton
                pos += 1;
                _SUBMIT_SM_PDU[pos] = destinationAddressNpi; // dest_addr_npi
                pos += 1;
                _destination_addr = Utility.ConvertStringToByteArray(Utility.GetString(destinationAddress, 20, "")); // destination_addr
                Array.Copy(_destination_addr, 0, _SUBMIT_SM_PDU, pos, _destination_addr.Length);
                pos += _destination_addr.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = esmClass; // esm_class
                pos += 1;
                _SUBMIT_SM_PDU[pos] = protocolId; // protocol_id
                pos += 1;
                _SUBMIT_SM_PDU[pos] = priorityFlag; // priority_flag
                pos += 1;
                _shedule_delivery_time = Utility.ConvertStringToByteArray(Utility.GetDateString(sheduleDeliveryTime)); // schedule_delivery_time
                Array.Copy(_shedule_delivery_time, 0, _SUBMIT_SM_PDU, pos, _shedule_delivery_time.Length);
                pos += _shedule_delivery_time.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _validity_period = Utility.ConvertStringToByteArray(Utility.GetDateString(validityPeriod)); // validity_period
                Array.Copy(_validity_period, 0, _SUBMIT_SM_PDU, pos, _validity_period.Length);
                pos += _validity_period.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = registeredDelivery; // registered_delivery
                pos += 1;
                _SUBMIT_SM_PDU[pos] = replaceIfPresentFlag; // replace_if_present_flag
                pos += 1;
                _SUBMIT_SM_PDU[pos] = dataCoding; // data_coding
                pos += 1;
                _SUBMIT_SM_PDU[pos] = smDefaultMsgId; // sm_default_msg_id
                pos += 1;

                _sm_length = message.Length > 254 ? (byte)254 : (byte)message.Length; // sm_length

                _SUBMIT_SM_PDU[pos] = _sm_length;
                pos += 1;
                Array.Copy(message, 0, _SUBMIT_SM_PDU, pos, _sm_length); // short_message
                pos += _sm_length;

                #region [ TLV parameters ]
                List<KeyValuePair<int, string>> tlvList = new List<KeyValuePair<int, string>>();
                tlvList.Add(new KeyValuePair<int, string>(0x1400, peId)); // PE_ID
                tlvList.Add(new KeyValuePair<int, string>(0x1401, templateId)); // Template ID
                tlvList.Add(new KeyValuePair<int, string>(0x1402, tmId)); // TM_ID

                foreach (KeyValuePair<int, string> item in tlvList)
                {
                    byte[] tag, length, value;

                    Utility.ConvertShortToArray((short)item.Key, out tag);
                    //logMessage(LogLevels.LogPdu, "Submit SM | TAG :: " + item.Key.ToString() + " :: " + BitConverter.ToString(tag));
                    Array.Copy(tag, 0, _SUBMIT_SM_PDU, pos, tag.Length);
                    pos += tag.Length;

                    int l = item.Value.Length + 1;
                    Utility.ConvertShortToArray((short)l, out length);
                    //logMessage(LogLevels.LogPdu, "Submit SM | LENGTH :: " + l.ToString() + " :: " + BitConverter.ToString(length));
                    Array.Copy(length, 0, _SUBMIT_SM_PDU, pos, length.Length);
                    pos += length.Length;

                    value = Utility.ConvertStringToByteArray(Utility.GetString(item.Value, ""));
                    //logMessage(LogLevels.LogPdu, "Submit SM | VALUE :: " + item.Value.ToString() + " :: " + BitConverter.ToString(value));
                    Array.Copy(value, 0, _SUBMIT_SM_PDU, pos, value.Length);
                    pos += value.Length;

                    pos += 1;
                }
                //logMessage(LogLevels.LogPdu, "SubmitSM |++ " + BitConverter.ToString(_SUBMIT_SM_PDU));
                #endregion


                Utility.CopyIntToArray(pos, _SUBMIT_SM_PDU, 0);


                Send(_SUBMIT_SM_PDU, pos);
                // Commented by Anirban Seth On 21-Sep-2022
                //undeliveredMessages++;
                return _sequence_number;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "SubmitSM | " + ex.ToString());
            }
            return -1;

        }//SubmitSM

        public int SubmitSMExtended(byte sourceAddressTon, byte sourceAddressNpi, string sourceAddress,
                            byte destinationAddressTon, byte destinationAddressNpi, string destinationAddress,
                            byte esmClass, byte protocolId, byte priorityFlag,
                            DateTime sheduleDeliveryTime, DateTime validityPeriod, byte registeredDelivery,
                            byte replaceIfPresentFlag, byte dataCoding, byte smDefaultMsgId,
                            byte[] message, byte messageIdentification, byte messageIndex, byte messageTotalIndex,
                            string peId, string templateId, string tmId
                    )
        {
            try
            {
                byte[] _destination_addr;
                byte[] _source_addr;
                byte[] _SUBMIT_SM_PDU;
                byte[] _shedule_delivery_time;
                byte[] _validity_period;
                int _sequence_number;
                int pos;
                byte _sm_length;


                _SUBMIT_SM_PDU = new byte[KernelParameters.MaxPduSize];

                ////////////////////////////////////////////////////////////////////////////////////////////////
                /// Start filling PDU						

                Utility.CopyIntToArray(0x00000004, _SUBMIT_SM_PDU, 4);
                _sequence_number = smsc.SequenceNumber;
                Utility.CopyIntToArray(_sequence_number, _SUBMIT_SM_PDU, 12);
                pos = 16;
                _SUBMIT_SM_PDU[pos] = 0x00; //service_type
                pos += 1;
                _SUBMIT_SM_PDU[pos] = sourceAddressTon;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = sourceAddressNpi;
                pos += 1;
                _source_addr = Utility.ConvertStringToByteArray(Utility.GetString(sourceAddress, 20, ""));
                Array.Copy(_source_addr, 0, _SUBMIT_SM_PDU, pos, _source_addr.Length);
                pos += _source_addr.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = destinationAddressTon;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = destinationAddressNpi;
                pos += 1;
                _destination_addr = Utility.ConvertStringToByteArray(Utility.GetString(destinationAddress, 20, ""));
                Array.Copy(_destination_addr, 0, _SUBMIT_SM_PDU, pos, _destination_addr.Length);
                pos += _destination_addr.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = esmClass;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = protocolId;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = priorityFlag;
                pos += 1;
                _shedule_delivery_time = Utility.ConvertStringToByteArray(Utility.GetDateString(sheduleDeliveryTime));
                Array.Copy(_shedule_delivery_time, 0, _SUBMIT_SM_PDU, pos, _shedule_delivery_time.Length);
                pos += _shedule_delivery_time.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _validity_period = Utility.ConvertStringToByteArray(Utility.GetDateString(validityPeriod));
                Array.Copy(_validity_period, 0, _SUBMIT_SM_PDU, pos, _validity_period.Length);
                pos += _validity_period.Length;
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = registeredDelivery;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = replaceIfPresentFlag;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = dataCoding;
                pos += 1;
                _SUBMIT_SM_PDU[pos] = smDefaultMsgId;
                pos += 1;

                _sm_length = message.Length > 254 ? (byte)254 : (byte)message.Length;

                _SUBMIT_SM_PDU[pos] = (byte)(_sm_length + 6);
                pos += 1;

                // Begin UDH 
                // Length of UDH (5 bytes)
                _SUBMIT_SM_PDU[pos] = 0x05;
                pos += 1;
                // Indicator for concatenated message
                _SUBMIT_SM_PDU[pos] = 0x00;
                pos += 1;
                // Subheader Length (3 bytes)
                _SUBMIT_SM_PDU[pos] = 0x03;
                pos += 1;
                // message identification - can be any hexadecimal
                // number but needs to match the UDH Reference Number of all  concatenated SMS
                _SUBMIT_SM_PDU[pos] = messageIdentification;
                pos += 1;
                // Number of pieces of the concatenated message
                _SUBMIT_SM_PDU[pos] = messageTotalIndex;
                pos += 1;
                // Sequence number (used by the mobile to concatenate the split messages)
                _SUBMIT_SM_PDU[pos] = messageIndex;
                pos += 1;
                // End UDH 


                Array.Copy(message, 0, _SUBMIT_SM_PDU, pos, _sm_length);
                pos += _sm_length;

                #region [ TLV parameters ]
                List<KeyValuePair<int, string>> tlvList = new List<KeyValuePair<int, string>>();
                tlvList.Add(new KeyValuePair<int, string>(0x1400, peId)); // PE_ID
                tlvList.Add(new KeyValuePair<int, string>(0x1401, templateId)); // Template ID
                tlvList.Add(new KeyValuePair<int, string>(0x1402, tmId)); // TM_ID

                foreach (KeyValuePair<int, string> item in tlvList)
                {
                    byte[] tag, length, value;

                    Utility.ConvertShortToArray((short)item.Key, out tag);
                    //logMessage(LogLevels.LogPdu, "Submit SM | TAG :: " + item.Key.ToString() + " :: " + BitConverter.ToString(tag));
                    Array.Copy(tag, 0, _SUBMIT_SM_PDU, pos, tag.Length);
                    pos += tag.Length;

                    int l = item.Value.Length + 1;
                    Utility.ConvertShortToArray((short)l, out length);
                    //logMessage(LogLevels.LogPdu, "Submit SM | LENGTH :: " + l.ToString() + " :: " + BitConverter.ToString(length));
                    Array.Copy(length, 0, _SUBMIT_SM_PDU, pos, length.Length);
                    pos += length.Length;

                    value = Utility.ConvertStringToByteArray(Utility.GetString(item.Value, ""));
                    //logMessage(LogLevels.LogPdu, "Submit SM | VALUE :: " + item.Value.ToString() + " :: " + BitConverter.ToString(value));
                    Array.Copy(value, 0, _SUBMIT_SM_PDU, pos, value.Length);
                    pos += value.Length;

                    pos += 1;
                }
                //logMessage(LogLevels.LogPdu, "SubmitSM |++ " + BitConverter.ToString(_SUBMIT_SM_PDU));
                #endregion

                Utility.CopyIntToArray(pos, _SUBMIT_SM_PDU, 0);

                Send(_SUBMIT_SM_PDU, pos);

                // Commented by Anirban Seth On 21-Sep-2022
                //undeliveredMessages++;
                return _sequence_number;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "SubmitSM | " + ex.ToString());
            }
            return -1;

        }//SubmitSMExtended

        public int DataSM(byte sourceAddressTon, byte sourceAddressNpi, string sourceAddress,
                                byte destinationAddressTon, byte destinationAddressNpi, string destinationAddress,
                                byte esmClass,
                                byte registeredDelivery,
                                byte dataCoding,
                                byte[] data)
        {
            try
            {
                byte[] _destination_addr;
                byte[] _source_addr;
                byte[] _DATA_SM_PDU;
                int _sequence_number;
                int pos;
                Int16 _sm_length;


                _DATA_SM_PDU = new byte[KernelParameters.MaxPduSize];

                ////////////////////////////////////////////////////////////////////////////////////////////////
                /// Start filling PDU						

                Utility.CopyIntToArray(0x00000103, _DATA_SM_PDU, 4);
                _sequence_number = smsc.SequenceNumber;
                Utility.CopyIntToArray(_sequence_number, _DATA_SM_PDU, 12);
                pos = 16;
                _DATA_SM_PDU[pos] = 0x00; //service_type
                pos += 1;
                _DATA_SM_PDU[pos] = sourceAddressTon;
                pos += 1;
                _DATA_SM_PDU[pos] = sourceAddressNpi;
                pos += 1;
                _source_addr = Utility.ConvertStringToByteArray(Utility.GetString(sourceAddress, 20, ""));
                Array.Copy(_source_addr, 0, _DATA_SM_PDU, pos, _source_addr.Length);
                pos += _source_addr.Length;
                _DATA_SM_PDU[pos] = 0x00;
                pos += 1;
                _DATA_SM_PDU[pos] = destinationAddressTon;
                pos += 1;
                _DATA_SM_PDU[pos] = destinationAddressNpi;
                pos += 1;
                _destination_addr = Utility.ConvertStringToByteArray(Utility.GetString(destinationAddress, 20, ""));
                Array.Copy(_destination_addr, 0, _DATA_SM_PDU, pos, _destination_addr.Length);
                pos += _destination_addr.Length;
                _DATA_SM_PDU[pos] = 0x00;
                pos += 1;
                _DATA_SM_PDU[pos] = esmClass;
                pos += 1;
                _DATA_SM_PDU[pos] = registeredDelivery;
                pos += 1;
                _DATA_SM_PDU[pos] = dataCoding;
                pos += 1;

                _DATA_SM_PDU[pos] = 0x04;
                pos += 1;
                _DATA_SM_PDU[pos] = 0x24;
                pos += 1;

                _sm_length = data.Length > 32767 ? (Int16)32767 : (Int16)data.Length;

                _DATA_SM_PDU[pos] = BitConverter.GetBytes(_sm_length)[1];
                pos += 1;
                _DATA_SM_PDU[pos] = BitConverter.GetBytes(_sm_length)[0];
                pos += 1;
                Array.Copy(data, 0, _DATA_SM_PDU, pos, _sm_length);
                pos += _sm_length;

                Utility.CopyIntToArray(pos, _DATA_SM_PDU, 0);

                Send(_DATA_SM_PDU, pos);
                // Commented by Anirban Seth On 21-Sep-2022
                //undeliveredMessages++;
                return _sequence_number;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "DataSM | " + ex.ToString());
            }
            return -1;

        }//DataSM

        //public int SendSms(String from, String to, String text)
        //{
        //    return SendSms(from, to, splitLongText, text, askDeliveryReceipt);
        //}//SendSms
        //public int SendSms(String from, String to, String text, byte askDeliveryReceipt)
        //{
        //    return SendSms(from, to, splitLongText, text, askDeliveryReceipt);
        //}//SendSms
        //public int SendSms(String from, String to, bool splitLongText, String text)
        //{
        //    return SendSms(from, to, splitLongText, text, askDeliveryReceipt);
        //}//SendSms
        //public int SendSms(String from, String to, bool splitLongText, String text, byte askDeliveryReceipt)
        //{
        //    return SendSms(from, to, splitLongText, text, askDeliveryReceipt, 0, Utility.GetDataCoding(text));
        //}

        public int SendSms(
            String from,
            String to,
            bool splitLongText,
            String text,
            byte askDeliveryReceipt,
            //byte esmClass, 
            Encoding dataEncoding,
            MessageEncoding dataCoding,
            string id,
            string peId,
            string templateId,
            string tmId,
            int retryIndex = 0
        )
        {
            int sequenceNo = -1;
            byte sourceAddressTon = 5;
            byte sourceAddressNpi = 9;
            string sourceAddress;
            byte destinationAddressTon = 1;
            byte destinationAddressNpi = 1;
            string destinationAddress;
            byte registeredDelivery;
            int maxLength = SmsEncoding.MaxTextLength[(int)dataCoding];
            int splitSize = SmsEncoding.SplitSize[(int)dataCoding];//maxLength;

            sourceAddress = Utility.GetString(from, 20, "IFB");

            destinationAddress = Utility.GetString(to, 20, "");

            registeredDelivery = askDeliveryReceipt;

            //if (dataCoding == 8)
            //{
            //    //text = Utility.Endian2UTF(text);
            //    maxLength = 70 * 2;
            //    splitSize = 67 * 2;
            //}
            //else
            //{
            //    maxLength = 160;
            //    splitSize = 153;
            //}

            //if ((text.Length <= maxLength) || (splitLongText))
            //{
            //    byte protocolId;
            //    byte priorityFlag;
            //    DateTime sheduleDeliveryTime;
            //    DateTime validityPeriod;
            //    byte replaceIfPresentFlag;
            //    byte smDefaultMsgId;
            //    byte[] message;
            //    string smsText = text;

            //    protocolId = 0;
            //    priorityFlag = PriorityFlags.VeryUrgent;
            //    sheduleDeliveryTime = DateTime.MinValue;
            //    validityPeriod = DateTime.MinValue;
            //    replaceIfPresentFlag = ReplaceIfPresentFlags.DoNotReplace;
            //    smDefaultMsgId = 0;

            //    while (smsText.Length > 0)
            //    {
            //        if (dataCoding == 8)
            //            message = Encoding.BigEndianUnicode.GetBytes(smsText.Substring(0, smsText.Length > maxLength ? maxLength : smsText.Length));
            //        else
            //            message = Encoding.ASCII.GetBytes(smsText.Substring(0, smsText.Length > maxLength ? maxLength : smsText.Length));

            //        smsText = smsText.Remove(0, smsText.Length > maxLength ? maxLength : smsText.Length);

            //        sequenceNo = SubmitSM(sourceAddressTon, sourceAddressNpi, sourceAddress, 
            //                            destinationAddressTon, destinationAddressNpi, destinationAddress,
            //                            esmClass, protocolId, priorityFlag,
            //                            sheduleDeliveryTime, validityPeriod, registeredDelivery, replaceIfPresentFlag,
            //                            dataCoding, smDefaultMsgId, message);

            //    }
            //}
            //else
            //{
            //    byte[] data;

            //    if (dataCoding == 8)
            //        data = Encoding.UTF8.GetBytes(text);
            //    else
            //        data = Encoding.ASCII.GetBytes(text);
            //    sequenceNo = DataSM(sourceAddressTon, sourceAddressNpi, sourceAddress,
            //                        destinationAddressTon, destinationAddressNpi, destinationAddress,
            //                        esmClass, registeredDelivery, dataCoding, data);
            //}

            //#region [ Database Entry ]
            ////For DataBase Entry
            //LocalStorage ls = new LocalStorage();
            ////string entry = "INSERT INTO [SMSPush] WITH (ROWLOCK) ([Source],[Destination],[MySubmitDate],[Message],SequenceNo,RefID) VALUES ('"
            ////    + sourceAddress + "','" + destinationAddress + "',GETDATE(),'" + s.apostropy(text) + "','" + sequenceNo + "','" + ID + "')";
            //string sql = String.Format(
            //        @"INSERT INTO [SMSPush] ([Source],[Destination],[MySubmitDate],[Message],[SequenceNo],[RefID],[CreationDate]) 
            //        VALUES ('{0}', '{1}', '{2:yyyy-MM-ddTHH:mm:ss.fff}', '{3}', {4}, {5}, '{2:yyyy-MM-ddTHH:mm:ss.fff}')"
            //        , sourceAddress
            //        , destinationAddress
            //        , DateTime.Now
            //        , Utility.RemoveApostropy(text)
            //        , sequenceNo
            //        , ID
            //    );
            //LocalStorage.ExecuteNonQuery(sql);
            //#endregion

            //logMessage(LogLevels.LogDebug, "Send To- " + destinationAddress +" Submit Time- "+System.DateTime.Now+ " Send Message id- " + messageId);

            byte protocolId = 0;
            byte priorityFlag = PriorityFlags.VeryUrgent;
            DateTime sheduleDeliveryTime = DateTime.MinValue;
            DateTime validityPeriod = DateTime.MinValue;
            byte replaceIfPresentFlag = ReplaceIfPresentFlags.DoNotReplace;
            byte smDefaultMsgId = 0;

            List<byte[]> messages = new List<byte[]>();
            string smsText = text;
            bool isLongMessage = smsText.Length > maxLength;
            int dstCoding = (int)(dataCoding == MessageEncoding.DEFAULT ? smsc.DefaultEncoding : dataCoding);

            //splitSize = (maxLength / SmsEncoding.DataSize[(int)dstCoding]);

            Logger.Write(LogType.Steps, String.Format("{0} Message with length {3} in Coding {1} {2}", isLongMessage ? "Extended" : "Normal", (int)dataCoding, dataCoding, smsText.Length));

            if (isLongMessage)
                splitSize = SmsEncoding.SplitSize[(int)dstCoding];

            Logger.Write(LogType.Steps, String.Format("Max Length : {0}\tData Size : {1}\tSplit size : {2}", maxLength, SmsEncoding.DataSize[(int)dstCoding], splitSize));

            int index = 0;
            byte[] srcBytes;
            byte[] destBytes;
            while (index < smsText.Length)
            {
                int count = splitSize > (smsText.Length - index) ? (smsText.Length - index) : splitSize;
                Logger.Write(LogType.Steps, String.Format("COUNT:{0}", count));
                byte[] messageBytes;
                do
                {
                    Logger.Write(LogType.Steps, String.Format("STR  :{0:000}:{1}", smsText.Substring(index, count).Length, smsText.Substring(index, count)));
                    //int srcCoding = dataCoding;

                    string str = smsText.Substring(index, count);
                    srcBytes = dataEncoding.GetBytes(str);
                    if (dataEncoding.EncodingName != (SmsEncoding.Encodings[(int)dataCoding]).EncodingName)
                    {
                        destBytes = Encoding.Convert(dataEncoding, SmsEncoding.Encodings[dstCoding], srcBytes);
                        //if (dstCoding == 0)
                        //    destBytes = PduBitPacker.PackBytes(destBytes);
                    }
                    else
                    {
                        destBytes = srcBytes;
                    }
                    messageBytes = destBytes;
                    if (messageBytes.Length <= (isLongMessage ? SmsEncoding.DataBufferSize : SmsEncoding.MaxTextLength)[(int)dataCoding])
                        break;
                    count--;
                } while (true);

                byte[] b = new byte[messageBytes.Length];
                Array.Copy(messageBytes, 0, b, 0, messageBytes.Length);
                messages.Add(b);
                index += count;
            }

            if (messages.Count == 1)
            {
                sequenceNo = SubmitSM(sourceAddressTon, sourceAddressNpi, sourceAddress,
                                            destinationAddressTon, destinationAddressNpi, destinationAddress,
                                            //esmClass, protocolId, priorityFlag,
                                            0x00, protocolId, priorityFlag,
                                            sheduleDeliveryTime, validityPeriod, registeredDelivery, replaceIfPresentFlag,
                                            (byte)dataCoding, smDefaultMsgId, messages[0], peId, templateId, tmId);

                #region [ Database Entry ]
                //For DataBase Entry
                LocalStorage ls = new LocalStorage();
                string sql = String.Format(
                            @"INSERT INTO [SMSPush] (
                                [Source], [Destination], [Message], [PeId], [TemplateId], [TmId]
                                , [Vendor], [Connection], [SequenceNo], [RefID], [RetryIndex], [DataCoding], [CreationDate]
                            ) 
                            VALUES (
                                '{0}', '{1}', '{2}', '{3}', '{4}', '{5}', 
                                '{6}', '{7}', {8}, {9}, {10}, {11}, '{12:yyyy-MM-ddTHH:mm:ss.fff}'
                            )"
                            , sourceAddress
                            , destinationAddress
                            , Utility.RemoveApostropy(text)
                            , peId
                            , templateId
                            , tmId
                            , smsc.VendorName
                            , smsc.Name
                            , sequenceNo
                            , id
                            , retryIndex
                            , (byte)dataCoding
                            , DateTime.Now
                        );
                LocalStorage.ExecuteNonQuery(sql);
                #endregion
            }
            else
            {
                byte messageIdentification = smsc.MessageIdentificationNumber; //(byte) new Random(255).Next();
                Logger.Write(LogType.Steps, String.Format("Message split in {0} blocks", messages.Count));
                Logger.Write(LogType.Steps, String.Format("Message Identificaiton Number is {0}", messageIdentification));

                for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
                {
                    string strMessage = ShortMessage.GetString((byte)dataCoding, messages[messageIndex], messages[messageIndex].Length);
                    Logger.Write(LogType.Steps, String.Format("Sending {0}/{1} - {2}", messageIndex + 1, messages.Count, strMessage));

                    sequenceNo = SubmitSMExtended(sourceAddressTon, sourceAddressNpi, sourceAddress,
                                            destinationAddressTon, destinationAddressNpi, destinationAddress,
                                            //esmClass, protocolId, priorityFlag,
                                            0x40, protocolId, priorityFlag,
                                            sheduleDeliveryTime, validityPeriod, registeredDelivery, replaceIfPresentFlag,
                                            (byte)dataCoding, smDefaultMsgId, messages[messageIndex], messageIdentification, (byte)(messageIndex + 1), (byte)(messages.Count), peId, templateId, tmId);

                    #region [ Database Entry ]
                    //For DataBase Entry
                    LocalStorage ls = new LocalStorage();
                    string sql = String.Format(
                            @"INSERT INTO [SMSPush] (
                                [Source], [Destination], [Message], [PeId], [TemplateId], [TmId]
                                , [Vendor], [Connection], [SequenceNo], [RefID], [RetryIndex], [DataCoding], [CreationDate]
                            ) 
                            VALUES (
                                '{0}', '{1}', '{2}', '{3}', '{4}', '{5}', 
                                '{6}', '{7}', {8}, {9}, {10}, {11}, '{12:yyyy-MM-ddTHH:mm:ss.fff}'
                            )"
                            , sourceAddress
                            , destinationAddress
                            //, Utility.RemoveApostropy(textMessages[messageIndex])
                            , Utility.RemoveApostropy(strMessage)
                            , peId
                            , templateId
                            , tmId
                            , smsc.VendorName
                            , smsc.Name
                            , sequenceNo
                            , id
                            , retryIndex
                            , (byte)dataCoding
                            , DateTime.Now
                        );
                    LocalStorage.ExecuteNonQuery(sql);
                    #endregion
                }
            }


            //#region [ Update - Anirban 05-Aug-2022
            //if (dataCoding == 8)
            //{
            //    message = ShortMessage.GetBytes(dataCoding, text);
            //}
            //Logger.Write(LogType.Steps, "Normal Message in " + (dataCoding == 8 ? "Unicode" : "ASCII"));
            //#endregion



            //message = ShortMessage.GetBytes(dataCoding, text);
            ////if (text.Length <= maxLength) {
            //if (message.Length <= maxLength)
            //{
            //    Logger.Write(LogType.Steps, "Normal Message with coding " + dataCoding.ToString());
            //    //smsText = text.Substring(0, text.Length > maxLength ? maxLength : text.Length);

            //    //if (dataCoding == 8)
            //    //    message = Encoding.BigEndianUnicode.GetBytes(smsText);
            //    //else
            //    //    message = Encoding.ASCII.GetBytes(smsText);
            //    //message = ShortMessage.GetBytes(dataCoding, smsText);

            //    sequenceNo = SubmitSM(sourceAddressTon, sourceAddressNpi, sourceAddress,
            //                                destinationAddressTon, destinationAddressNpi, destinationAddress,
            //                                esmClass, protocolId, priorityFlag,
            //                                sheduleDeliveryTime, validityPeriod, registeredDelivery, replaceIfPresentFlag,
            //                                dataCoding, smDefaultMsgId, message, peId, templateId, tmId);

            //    #region [ Database Entry ]
            //    //For DataBase Entry
            //    LocalStorage ls = new LocalStorage();
            //    string sql = String.Format(
            //                @"INSERT INTO [SMSPush] ([Source], [Destination], [Message], [PeId], [TemplateId], [TmId], [Vendor], [SequenceNo], [RefID], [RetryIndex], [DataCoding], [CreationDate]) 
            //                    VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', {7}, {8}, {9}, {10}, '{11:yyyy-MM-ddTHH:mm:ss.fff}')"
            //                , sourceAddress
            //                , destinationAddress
            //                , Utility.RemoveApostropy(text)
            //                , peId
            //                , templateId
            //                , tmId
            //                , smsc.Description
            //                , sequenceNo
            //                , id
            //                , retryIndex
            //                , dataCoding
            //                , DateTime.Now
            //            );
            //    LocalStorage.ExecuteNonQuery(sql);
            //    #endregion
            //}
            //else
            //{
            //    Logger.Write(LogType.Steps, "Long Message with coding " + dataCoding.ToString());
            //    //List<string> textMessages = new List<string>();

            //    //smsText = text;
            //    //while (smsText.Length > 0)
            //    //{
            //    //    textMessages.Add(smsText.Substring(0, smsText.Length > splitSize ? splitSize : smsText.Length));
            //    //    smsText = smsText.Remove(0, smsText.Length > splitSize ? splitSize : smsText.Length);
            //    //}

            //    List<byte[]> messages = new List<byte[]>(10);
            //    int splitIndex, length;//, messageBlockIndex = 0; 
            //    for (splitIndex = 0; splitIndex < message.Length; splitIndex += length)
            //    {
            //        length = splitSize > (message.Length - splitIndex) ? (message.Length - splitIndex) : splitSize;
            //        byte[] b = new byte[length];
            //        Array.Copy(message, splitIndex, b, 0, length);
            //        messages.Add(b);
            //    }

            //    byte messageIdentification = smsc.MessageIdentificationNumber; //(byte) new Random(255).Next();

            //    //Logger.Write(LogType.Steps, String.Format("Message split in {0} blocks", textMessages.Count));
            //    Logger.Write(LogType.Steps, String.Format("Message split in {0} blocks", messages.Count));
            //    Logger.Write(LogType.Steps, String.Format("Message Identificaiton Number is {0}", messageIdentification));

            //    //for (int messageIndex = 0; messageIndex < textMessages.Count; messageIndex++)
            //    for (int messageIndex = 0; messageIndex < messages.Count; messageIndex++)
            //    {
            //        //if (dataCoding == 8)
            //        //    message = Encoding.BigEndianUnicode.GetBytes(textMessages[messageIndex]);
            //        //else
            //        //    message = Encoding.ASCII.GetBytes(textMessages[messageIndex]);
            //        //message = ShortMessage.GetBytes(dataCoding, textMessages[messageIndex]);

            //        //Logger.Write(LogType.Steps, String.Format("Sending {0}/{1} - {2}", messageIndex+1, messages.Count, textMessages[messageIndex]));

            //        message = messages[messageIndex];
            //        string strMessage = ShortMessage.GetString(dataCoding, messages[messageIndex], messages[messageIndex].Length);
            //        Logger.Write(LogType.Steps, String.Format("Sending {0}/{1} - {2}", messageIndex + 1, messages.Count, strMessage));

            //        //sequenceNo = SubmitSMExtended(sourceAddressTon, sourceAddressNpi, sourceAddress,
            //        //                        destinationAddressTon, destinationAddressNpi, destinationAddress,
            //        //                        esmClass, protocolId, priorityFlag,
            //        //                        sheduleDeliveryTime, validityPeriod, registeredDelivery, replaceIfPresentFlag,
            //        //                        dataCoding, smDefaultMsgId, message, messageIdentification, (byte) (messageIndex+1), (byte) (textMessages.Count), peId, templateId, tmId);
            //        sequenceNo = SubmitSMExtended(sourceAddressTon, sourceAddressNpi, sourceAddress,
            //                                destinationAddressTon, destinationAddressNpi, destinationAddress,
            //                                esmClass, protocolId, priorityFlag,
            //                                sheduleDeliveryTime, validityPeriod, registeredDelivery, replaceIfPresentFlag,
            //                                dataCoding, smDefaultMsgId, message, messageIdentification, (byte)(messageIndex + 1), (byte)(messages.Count), peId, templateId, tmId);

            //        #region [ Database Entry ]
            //        //For DataBase Entry
            //        LocalStorage ls = new LocalStorage();
            //        string sql = String.Format(
            //                @"INSERT INTO [SMSPush] ([Source], [Destination], [Message], [PeId], [TemplateId], [TmId], [Vendor], [SequenceNo], [RefID], [RetryIndex], [DataCoding], [CreationDate]) 
            //                    VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', {7}, {8}, {9}, {10}, '{11:yyyy-MM-ddTHH:mm:ss.fff}')"
            //                , sourceAddress
            //                , destinationAddress
            //                //, Utility.RemoveApostropy(textMessages[messageIndex])
            //                , Utility.RemoveApostropy(strMessage)
            //                , peId
            //                , templateId
            //                , tmId
            //                , smsc.Description
            //                , sequenceNo
            //                , id
            //                , retryIndex
            //                , dataCoding
            //                , DateTime.Now
            //            );
            //        LocalStorage.ExecuteNonQuery(sql);
            //        #endregion
            //    }
            //}

            return sequenceNo;
        }//SendSms
         //public int SendFlashSms(String from, String to, String text)
         //{
         //    return SendFlashSms(from, to, text, askDeliveryReceipt);
         //}//SendFlashSms
         //public int SendFlashSms(String from, String to, String text, byte askDeliveryReceipt)
         //{
         //    return SendSms(from, to, false, text.Substring(0, text.Length > 160 ? 160 : text.Length), askDeliveryReceipt, 0, 0x10);
         //}//SendFlashSms
        public int SendMedia(String from, String to, byte[] media)
        {
            return SendMedia(from, to, media, askDeliveryReceipt);
        }//SendMedia
        public int SendMedia(String from, String to, byte[] media, byte askDeliveryReceipt)
        {
            return SendData(from, to, media, 0x40, 0xF5, askDeliveryReceipt);
        }//SendMedia
        public int SendData(String from, String to, byte[] data)
        {
            return SendData(from, to, data, askDeliveryReceipt);
        }//SendData
        public int SendData(String from, String to, byte[] data, byte askDeliveryReceipt)
        {
            return SendData(from, to, data, 0, 0, 0);
        }//SendData
        public int SendData(String from, String to, byte[] data, byte esmClass, byte dataCoding, byte askDeliveryReceipt)
        {
            int messageId;
            byte sourceAddressTon;
            byte sourceAddressNpi;
            string sourceAddress;
            byte destinationAddressTon;
            byte destinationAddressNpi;
            string destinationAddress;
            byte registeredDelivery;

            sourceAddress = Utility.GetString(from, 20, "");
            sourceAddressTon = getAddrTon(sourceAddress);
            sourceAddressNpi = getAddrNpi(sourceAddress);

            destinationAddress = Utility.GetString(from, 20, "");
            destinationAddressTon = getAddrTon(destinationAddress);
            destinationAddressNpi = getAddrNpi(destinationAddress);

            registeredDelivery = askDeliveryReceipt;

            messageId = DataSM(sourceAddressTon, sourceAddressNpi, sourceAddress,
                                destinationAddressTon, destinationAddressNpi, destinationAddress,
                                esmClass, registeredDelivery, dataCoding, data);

            return messageId;
        }//SendData
        #endregion New Send Functions

        #endregion Public Functions

        #region Properties
        public bool CanSend
        {
            get
            {
                try
                {
                    // Commented by Anirban Seth On 21-Sep-2022
                    //if ((connectionState == ConnectionStates.SMPP_BINDED) && (undeliveredMessages <= KernelParameters.MaxUndeliverableMessages))
                    if (connectionState == ConnectionStates.SMPP_BINDED)
                        return true;
                }
                catch (Exception ex)
                {
                    logMessage(LogLevels.LogExceptions, "CanSend | " + ex.ToString());
                }
                return false;
            }
        }//CanSend

        public int LogLevel
        {
            get
            {
                return logLevel;
            }
            set
            {
                logLevel = value;
            }
        }//CanSend


        public byte AskDeliveryReceipt
        {
            get
            {
                return askDeliveryReceipt;
            }
            set
            {
                if ((value < 3) && (value >= 0))
                    askDeliveryReceipt = value;
            }
        }//AskDeliveryReceipt

        public bool SplitLongText
        {
            get
            {
                return splitLongText;
            }
            set
            {
                splitLongText = value;
            }
        }//SplitLongText
        public int NationalNumberLength
        {
            get
            {
                return nationalNumberLength;
            }
            set
            {
                if (value <= 12)
                    nationalNumberLength = value;
            }
        }//NationalNumberLength
        public bool UseEnquireLink
        {
            get
            {
                return useEnquireLink;
            }
            set
            {
                useEnquireLink = value;
            }
        }//UseEnquireLink
        public int EnquireLinkTimeout
        {
            get
            {
                return enquireLinkTimeout;
            }
            set
            {
                if (value > 1000)
                    enquireLinkTimeout = value;
            }
        }//EnquireLinkTimeout
        public int ReconnectTimeout
        {
            get
            {
                return reconnectTimeout;
            }
            set
            {
                if (value > 1000)
                    reconnectTimeout = value;
            }
        }//ReconnectTimeout

        #endregion Properties

        #region Events

        public event SubmitSmRespEventHandler OnSubmitSmResp;

        public event DeliverSmEventHandler OnDeliverSm;

        public event LogEventHandler OnLog;

        #endregion Events

        #region Private functions
        private void connectToSMSC()
        {
            try
            {
                if (ReferenceEquals(smsc, null))
                {
                    logMessage(LogLevels.LogErrors, "Connect | ERROR #1011 : No SMSC defined. Please ddd SMSC definition first.");
                    return;
                }
                initClientParameters();
                //IPAddress ipAddress = IPAddress.Parse(smsc.Host);
                //IPEndPoint remoteEP = new IPEndPoint(ipAddress, smsc.Port);
                ////  Create a TCP/IP  socket.
                ////Try to disconnect if connected
                //tryToDisconnect();


                //clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                //logMessage(LogLevels.LogInfo, "Trying to connect to " + smsc.Description + "[" + smsc.Host + ":" + smsc.Port + "] Username " + smsc.SystemId + "[" + smsc.Password + "]");
                //clientSocket.BeginConnect(remoteEP, new AsyncCallback(connectCallback), clientSocket);
                //connectionState = ConnectionStates.SMPP_SOCKET_CONNECT_SENT;
                tryToDisconnect();
                logMessage(LogLevels.LogInfo, "Trying to connect to " + smsc.VendorName + "[" + smsc.Host + ":" + smsc.Port + "]");
                logMessage(LogLevels.LogInfo, "Username " + smsc.SystemId + "[" + smsc.Password + "]");
                logMessage(LogLevels.LogInfo, "Default encoding : " + smsc.DefaultEncoding.ToString() + " [" + ((int)smsc.DefaultEncoding).ToString() + "]");
                logMessage(LogLevels.LogInfo, "Message Id Type : " + smsc.MessageIdType.ToString() + " [" + ((int)smsc.MessageIdType).ToString() + "]");
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //IPAddress ipAddress;
                //if (IPAddress.TryParse(smsc.Host, out ipAddress))
                //{
                //    IPEndPoint remoteEP = new IPEndPoint(ipAddress, smsc.Port);
                //    clientSocket.BeginConnect(remoteEP, new AsyncCallback(connectCallback), clientSocket);
                //}
                //else
                //{
                //    clientSocket.BeginConnect(smsc.Host, smsc.Port, new AsyncCallback(connectCallback), clientSocket);
                //}
                clientSocket.BeginConnect(smsc.Host, smsc.Port, new AsyncCallback(connectCallback), clientSocket);
                connectionState = ConnectionStates.SMPP_SOCKET_CONNECT_SENT;
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "connectToSMSC | " + ex.ToString());
            }

        }//connectToSMSC

        private void tryToDisconnect()
        {
            try
            {
                if (clientSocket != null)
                {
                    if (clientSocket.Connected)
                    {
                        clientSocket.Shutdown(SocketShutdown.Both);
                    }
                    clientSocket = null;
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "tryToDisconnect | " + ex.ToString());
            }
        }//tryToDisconnect

        private void disconnectSocket()
        {
            try
            {
                logMessage(LogLevels.LogInfo, "Disconnected");
                connectionState = ConnectionStates.SMPP_SOCKET_DISCONNECTED;
                clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "disconnectSocket | " + ex.ToString());
            }
        }//disconnectSocket

        private void disconnect(Socket client)
        {
            try
            {
                logMessage(LogLevels.LogInfo, "Disconnected");
                connectionState = ConnectionStates.SMPP_SOCKET_DISCONNECTED;
                client.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "disconnect | " + ex.ToString());
            }
        }//Disconnect


        private void receive()
        {
            try
            {
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = clientSocket;

                // Begin receiving the data from the remote device.
                clientSocket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(receiveCallback), state);

            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "receive | " + ex.ToString());
            }

        }//receive

        private void bind()
        {
            try
            {
                byte[] Bind_PDU = new byte[1024];
                int pos, i, n;

                pos = 7;
                Bind_PDU[pos] = 0x09;

                pos = 12;
                Utility.CopyIntToArray(smsc.SequenceNumber, Bind_PDU, pos);
                pos = 15;

                pos++;
                n = smsc.SystemId.Length;
                for (i = 0; i < n; i++, pos++)
                    Bind_PDU[pos] = (byte)smsc.SystemId[i];
                Bind_PDU[pos] = 0;

                pos++;
                n = smsc.Password.Length;
                for (i = 0; i < n; i++, pos++)
                    Bind_PDU[pos] = (byte)smsc.Password[i];
                Bind_PDU[pos] = 0;

                pos++;
                n = smsc.SystemType.Length;
                for (i = 0; i < n; i++, pos++)
                    Bind_PDU[pos] = (byte)smsc.SystemType[i];
                Bind_PDU[pos] = 0;

                Bind_PDU[++pos] = 0x34; //interface version
                Bind_PDU[++pos] = (byte)smsc.AddrTon; //addr_ton
                Bind_PDU[++pos] = (byte)smsc.AddrNpi; //addr_npi

                //address_range
                pos++;
                n = smsc.AddressRange.Length;
                for (i = 0; i < n; i++, pos++)
                    Bind_PDU[pos] = (byte)smsc.AddressRange[i];
                Bind_PDU[pos] = 0x00;

                pos++;
                Bind_PDU[3] = Convert.ToByte(pos & 0x00FF);
                Bind_PDU[2] = Convert.ToByte((pos >> 8) & 0x00FF);

                // Begin sending the data to the remote device.
                logMessage(LogLevels.LogSteps, "BindSent");
                Send(Bind_PDU, pos);
                connectionState = ConnectionStates.SMPP_BIND_SENT;
                receive();
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "bind | " + ex.ToString());
            }

        }//bind

        private void unBind()
        {
            if (connectionState == ConnectionStates.SMPP_BINDED)
            {
                try
                {
                    byte[] _PDU = new byte[16];

                    Utility.CopyIntToArray(16, _PDU, 0);

                    Utility.CopyIntToArray(0x00000006, _PDU, 4);

                    Utility.CopyIntToArray(smsc.SequenceNumber, _PDU, 12);

                    logMessage(LogLevels.LogSteps, "Unbind sent.");
                    connectionState = ConnectionStates.SMPP_UNBIND_SENT;

                    Send(_PDU, 16);
                }
                catch (Exception ex)
                {
                    logMessage(LogLevels.LogExceptions, "unBind | " + ex.ToString());
                }
            }

        }//unBind

        #region [ Sumit SM Response ]

        private void processSubmitSmResp(SubmitSmRespEventArgs e)
        {
            string sql;
            try
            {
                // Commented by Anirban Seth On 21-Sep-2022
                //undeliveredMessages--;
                logMessage(LogLevels.LogInfo, "processSubmitSmResp/ Delivery Record:Seq No-" + e.Sequence + ", Meassage ID-" + e.MessageID);
                if (OnSubmitSmResp != null)
                {
                    OnSubmitSmResp.BeginInvoke(e, processSubmitSmRespCallback, e);
                }


                //#region [ Update Database ]
                //sql = String.Format(
                //            @"INSERT OR IGNORE INTO [SMSSent] ([SequenceNo], [MessageId]) VALUES ({0}, '{1}')"
                //            , e.Sequence
                //            , e.MessageID
                //        );
                //LocalStorage.ExecuteNonQuery(sql);
                ////logMessage(LogLevels.LogInfo, "processSubmitSmResp/ Delivery Record:Seq No-" + e.Sequence + ", Meassage ID-" + e.MessageID);
                //#endregion
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "processSubmitSmResp | " + ex.ToString());
            }

        }//processSubmitSmResp

        private void processSubmitSmRespCallback(IAsyncResult iar)
        {
            var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
            var invokedMethod = (SubmitSmRespEventHandler)ar.AsyncDelegate;
            var args = (SubmitSmRespEventArgs)iar.AsyncState;

            try
            {
                invokedMethod.EndInvoke(iar);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "processSubmitSmRespCallback | " + ex.ToString());
            }
        }
        #endregion

        #region [ Deliver SM ]
        private void processDeliverSm(DeliverSmEventArgs e)
        {
            try
            {
                logMessage(LogLevels.LogInfo, "processDeliverSm/ Delivery Record:" + "To-" + e.To + " From-" + e.From + " Meassage ID-" + e.ReceiptedMessageID + " HexString-" + e.TextString);
                if (OnDeliverSm != null)
                {
                    //OnDeliverSm(e);
                    OnDeliverSm.BeginInvoke(e, processDeliverSmCallback, e);
                }
                else
                    sendDeliverSmResp(e.SequenceNumber, StatusCodes.ESME_ROK);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "processDeliverSm | " + ex.ToString());
            }
        }//processDeliverSm

        private void processDeliverSmCallback(IAsyncResult iar)
        {
            var ar = (System.Runtime.Remoting.Messaging.AsyncResult)iar;
            var invokedMethod = (DeliverSmEventHandler)ar.AsyncDelegate;
            var args = (DeliverSmEventArgs)iar.AsyncState;
            try
            {
                invokedMethod.EndInvoke(iar);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "processDeliverSmCallback | " + ex.ToString());
            }
        }
        #endregion


        #region [ LOG ]
        private void processLog(LogEventArgs e)
        {
            try
            {
                if (OnLog != null)
                {
                    OnLog(e);
                }
            }
            catch
            {
            }

        }//processLog

        private void logMessage(int logLevel, string pMessage)
        {
            try
            {
                if ((logLevel) > 0)
                {
                    //MyLog.WriteLogFile("Log", "Log level-" + logLevel, pMessage);
                    //Logger.Write((LogType)logLevel, pMessage);
                    LogEventArgs evArg = new LogEventArgs((LogType)logLevel, pMessage);
                    //processLog(evArg);

                    if (this.OnLog != null)
                    {
                        this.OnLog(evArg);
                    }
                }
            }
            catch (Exception ex)
            {
                // DO NOT USE LOG INSIDE LOG FUNCTION !!! logMessage(LogLevels.LogExceptions, "logMessage | " +ex.ToString());
            }
        }//logMessage
        #endregion

        private void connectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.
                client.EndConnect(ar);
                clientSocket = client;
                clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);
                connectionState = ConnectionStates.SMPP_SOCKET_CONNECTED;
                logMessage(LogLevels.LogInfo, "Connected");
                lastSeenConnected = DateTime.Now;
                bind();
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "connectCallback | " + ex.ToString());
                tryToReconnect();
            }
        }//connectCallback


        private void tryToReconnect()
        {
            try
            {
                disconnectSocket();
                Thread.Sleep(reconnectTimeout);
                if (mustBeConnected)
                {
                    smscArray.NextSMSC();
                    connectToSMSC();
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "tryToReconnect | " + ex.ToString());
            }

        }//tryToReconnect

        private void receiveCallback(IAsyncResult ar)
        {
            try
            {
                int _command_length;
                uint _command_id;
                int _command_status;
                int _sequence_number;
                int _body_length;
                byte[] _PDU_body = new byte[0];
                byte[] _RESPONSE_PDU = new byte[KernelParameters.MaxPduSize];
                int i, x;
                bool _exit_flag;
                string unbindStr = "";
                // Retrieve the state object and the client socket 
                // from the async state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;
                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);
                //logMessage(LogLevels.LogSteps, "Received " + Utility.ConvertIntToHexString(bytesRead) + " bytes");
                if (bytesRead > 0)
                {
                    //test line
                    //logMessage(LogLevels.LogPdu, "Received Binary Data " + Utility.ConvertArrayToHexString(state.buffer, bytesRead));
                    // logMessage(LogLevels.LogPdu, "Received Binary Data to string " + Utility.ConvertArrayToString(state.buffer, bytesRead));
                    // There might be more data, so store the data received so far.

                    //if ((LogLevel & LogLevels.LogPdu) > 0)
                    //    logMessage(LogLevels.LogPdu, "Received Binary Data " + Utility.ConvertArrayToHexString(state.buffer, bytesRead));
                    //////////////////////////////
                    /// Begin processing SMPP messages
                    /// 
                    mLen = mPos + bytesRead;
                    if (mLen > KernelParameters.MaxBufferSize)
                    {
                        mPos = 0;
                        mLen = 0;
                        mbResponse = new byte[KernelParameters.MaxBufferSize];
                    }
                    else
                    {
                        Array.Copy(state.buffer, 0, mbResponse, mPos, bytesRead);
                        mPos = mLen;
                        _exit_flag = false;
                        x = 0;
                        while (((mLen - x) >= 16) && (_exit_flag == false))
                        {
                            _command_length = mbResponse[x + 0];
                            for (i = x + 1; i < x + 4; i++)
                            {
                                _command_length <<= 8;
                                _command_length = _command_length | mbResponse[i];
                            }

                            _command_id = mbResponse[x + 4];
                            for (i = x + 5; i < x + 8; i++)
                            {
                                _command_id <<= 8;
                                _command_id = _command_id | mbResponse[i];
                            }

                            _command_status = mbResponse[x + 8];
                            for (i = x + 9; i < x + 12; i++)
                            {
                                _command_status <<= 8;
                                _command_status = _command_status | mbResponse[i];
                            }

                            _sequence_number = mbResponse[x + 12];
                            for (i = x + 13; i < x + 16; i++)
                            {
                                _sequence_number <<= 8;
                                _sequence_number = _sequence_number | mbResponse[i];
                            }
                            if ((_command_length <= (mLen - x)) && (_command_length >= 16))
                            {
                                if (_command_length == 16)
                                    _body_length = 0;
                                else
                                {
                                    _body_length = _command_length - 16;
                                    _PDU_body = new byte[_body_length];
                                    Array.Copy(mbResponse, x + 16, _PDU_body, 0, _body_length);
                                }
                                //////////////////////////////////////////////////////////////////////////////////////////
                                ///SMPP Command parsing

                                switch (_command_id)
                                {
                                    case 0x80000009:
                                        logMessage(LogLevels.LogSteps, "Bind_Transiver_Resp");

                                        if (connectionState == ConnectionStates.SMPP_BIND_SENT)
                                        {
                                            if (_command_status == 0)
                                            {
                                                connectionState = ConnectionStates.SMPP_BINDED;
                                                logMessage(LogLevels.LogSteps, "SMPP Binded");
                                            }
                                            else
                                            {
                                                logMessage(LogLevels.LogSteps | LogLevels.LogErrors, "SMPP BIND ERROR : " + Utility.ConvertIntToHexString(_command_status));
                                                disconnect(client);
                                                tryToReconnect();
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "ERROR #3011 : Unexpected Bind_Transiver_Resp");
                                        }
                                        break;
                                    case 0x80000004:
                                        //logMessage(LogLevels.LogSteps, "Submit_Sm_Resp");                                          
                                        SubmitSmRespEventArgs evArg = new SubmitSmRespEventArgs(_sequence_number, _command_status, Utility.ConvertArrayToString(_PDU_body, _body_length - 1));
                                        processSubmitSmResp(evArg);
                                        break;
                                    case 0x80000103:
                                        logMessage(LogLevels.LogSteps, "Data_Sm_Resp");
                                        evArg = new SubmitSmRespEventArgs(_sequence_number, _command_status, Utility.ConvertArrayToString(_PDU_body, _body_length - 1));
                                        processSubmitSmResp(evArg);
                                        break;
                                    case 0x80000015:
                                        logMessage(LogLevels.LogSteps, "Enquire_Link_Resp");
                                        enquireLinkResponseTime = DateTime.Now;
                                        break;
                                    case 0x00000015:
                                        logMessage(LogLevels.LogSteps, "Enquire_Link");
                                        sendEnquireLinkResp(_sequence_number);
                                        break;
                                    case 0x80000006:
                                        logMessage(LogLevels.LogSteps, "Unbind_Resp");
                                        connectionState = ConnectionStates.SMPP_UNBINDED;
                                        disconnect(client);
                                        unbindStr = "Unbind_Resp";
                                        break;
                                    case 0x00000005:
                                        //logMessage(LogLevels.LogSteps, "Deliver_Sm");
                                        decodeAndProcessDeliverSm(_sequence_number, _PDU_body, _body_length);
                                        //logMessage(LogLevels.LogPdu, "Received Binary Data to string " + Utility.ConvertArrayToString(_PDU_body, _body_length));
                                        break;
                                    case 0x00000103:
                                        logMessage(LogLevels.LogSteps, "Data_Sm");
                                        decodeAndProcessDataSm(_sequence_number, _PDU_body, _body_length);
                                        break;
                                    default:
                                        sendGenericNack(_sequence_number, StatusCodes.ESME_RINVCMDID);
                                        logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "Unknown SMPP PDU type" + Utility.ConvertUIntToHexString(_command_id));
                                        break;
                                }
                                ///////////////////////////////////////////////////////////////////////////////////////////
                                ///END SMPP Command parsing
                                ///////////////////////////////////////////////////////////////////////////////////////////

                                if (_command_length == (mLen - x))
                                {
                                    mLen = 0;
                                    mPos = 0;
                                    x = 0;
                                    _exit_flag = true;
                                }
                                else
                                {
                                    x += _command_length;
                                }
                            }
                            else
                            {
                                sendGenericNack(_sequence_number, StatusCodes.ESME_RINVMSGLEN);
                                mLen -= x;
                                mPos = mLen;
                                Array.Copy(mbResponse, x, mbResponse, 0, mLen);
                                _exit_flag = true;
                                logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "Invalid PDU Length");
                            }
                            if (x < mLen)
                                logMessage(LogLevels.LogPdu, "NEXT PDU STEP IN POS " + Convert.ToString(x) + " FROM " + Convert.ToString(mLen));
                        }
                    }
                    //////////////////////////////
                    /// End processing SMPP messages
                    //  Get the rest of the data. 
                    if (unbindStr != "Unbind_Resp")
                    {
                        client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(receiveCallback), state);
                    }

                }
                else
                {
                    logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "Incoming network buffer from SMSC is empty.");
                    /*					if (client.Poll(0,SelectMode.SelectError)&&client.Poll(0,SelectMode.SelectRead)&&client.Poll(0,SelectMode.SelectWrite))
                                        {
                                            logMessage(LogLevels.LogSteps|LogLevels.LogExceptions, "Socket Error");
                                            unBind();
                                        }
                    */
                    tryToReconnect();
                }
            }

            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "receiveCallback | " + ex.ToString());
                unBind();
            }

        }//receiveCallback

        private void initClientParameters()
        {
            mbResponse = new byte[KernelParameters.MaxBufferSize];
            mPos = 0;
            mLen = 0;

            enquireLinkResponseTime = DateTime.Now;

            // Commented by Anirban Seth On 21-Sep-2022
            //undeliveredMessages = 0;
        }//initClientParameters

        private void decodeAndProcessDeliverSm(int sequence_number, byte[] _body, int _length)
        {
            //logMessage(LogLevels.LogDebug,
            //    "sequence_number : " + sequence_number.ToString()
            //    + "\r\n_body : " + BitConverter.ToString(_body)
            //    + "\r\nlength : " + _length.ToString());
            if (_length < 17)
            {
                sendDeliverSmResp(sequence_number, StatusCodes.ESME_RINVCMDLEN);
                return;
            }
            try
            {
                bool isDeliveryReceipt = false;
                bool isUdhiSet = false;
                byte _source_addr_ton, _source_addr_npi, _dest_addr_ton, _dest_addr_npi;
                byte _priority_flag, _data_coding, _esm_class;
                byte[] _source_addr = new byte[21];
                byte[] _dest_addr = new byte[21];
                byte saLength, daLength;
                byte[] _short_message = new byte[0];
                int _sm_length;
                int pos;

                /////////////////////////////////////
                /// Message Delivery Params
                /// 
                byte[] _receipted_message_id = new byte[254];
                byte _receipted_message_id_len = 0;
                byte _message_state = 0;
                Int16 sar_msg_ref_num = 0;
                byte sar_total_segments = 0;
                byte sar_segment_seqnum = 0;
                #region [ Added by Anirban Seth on 16 Jan 2019 - TLV Processing ]
                Dictionary<string, string> shortMessageValueList = null;// = new List<KeyValuePair<string, string>>();
                #endregion

                pos = 0;
                while ((pos < 5) && (_body[pos] != 0x00))
                    pos++;
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x01");
                    return;
                }
                _source_addr_ton = _body[++pos];
                _source_addr_npi = _body[++pos];
                pos++;
                saLength = 0;
                while ((saLength < 20) && (_body[pos] != 0x00))
                {
                    _source_addr[saLength] = _body[pos];
                    pos++;
                    saLength++;
                }
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x02");
                    return;
                }
                _dest_addr_ton = _body[++pos];
                _dest_addr_npi = _body[++pos];
                pos++;
                daLength = 0;
                while ((daLength < 20) && (_body[pos] != 0x00))
                {
                    _dest_addr[daLength] = _body[pos];
                    pos++;
                    daLength++;
                }
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x03");
                    return;
                }
                _esm_class = _body[++pos];
                switch (_esm_class)
                {
                    case 0x00:
                        break;
                    case 0x04:
                        logMessage(LogLevels.LogSteps, "Delivery Receipt Received");
                        //logMessage(LogLevels.LogSteps, "Delivery Receipt "+Utility.ConvertArrayToString(_body,_length));
                        isDeliveryReceipt = true;
                        break;
                    case 0x40:
                        logMessage(LogLevels.LogSteps, "UDHI Indicator set");
                        isUdhiSet = true;
                        break;
                    default:
                        logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "Unknown esm_class for DELIVER_SM : " + Utility.GetHexFromByte(_esm_class));
                        break;
                }
                pos += 1;
                //pos += 5;
                _priority_flag = _body[++pos];
                pos += 4;
                _data_coding = _body[++pos];
                pos += 1;
                _sm_length = _body[++pos];
                pos += 1;
                //logMessage(LogLevels.LogDebug, "_source_addr_ton : " + Convert.ToString(_source_addr_ton));
                //logMessage(LogLevels.LogDebug, "_source_addr_npi : " + Convert.ToString(_source_addr_npi));
                //logMessage(LogLevels.LogDebug, "_dest_addr_ton : " + Convert.ToString(_dest_addr_ton));
                //logMessage(LogLevels.LogDebug, "_dest_addr_npi : " + Convert.ToString(_dest_addr_npi));
                //logMessage(LogLevels.LogDebug, "_esm_class : " + Convert.ToString(_esm_class));
                //logMessage(LogLevels.LogDebug, "_priority_flag : " + Convert.ToString(_priority_flag));
                //logMessage(LogLevels.LogDebug, "_data_coding : " + Convert.ToString(_data_coding));
                //logMessage(LogLevels.LogDebug, "_sm_length : " + Convert.ToString(_sm_length));
                if (_sm_length > 0)
                {
                    _short_message = new byte[_sm_length];
                    Array.Copy(_body, pos, _short_message, 0, _sm_length);
                    //logMessage(LogLevels.LogDebug, "_short_message : " + BitConverter.ToString(_short_message));
                    //logMessage(LogLevels.LogSteps, "_short_message : " + Encoding.UTF8.GetString(_short_message));
                    #region [ Added by Anirban Seth on 16 Jan 2019 - TLV Processing ]
                    Utility.ParseDelivertReportShortMessage(_short_message, _sm_length, out shortMessageValueList);
                    #endregion

                    pos += _sm_length;
                }
                if ((isDeliveryReceipt) || (isUdhiSet))
                {
                    int _par_tag, _par_tag_length;
                    bool exit = false;
                    if (_sm_length > 0)
                    {
                        //pos += 1;// _sm_length;
                    }
                    while ((pos < _length) && (exit == false))
                    {
                        if (Utility.Get2ByteIntFromArray(_body, pos, _length, out _par_tag) == false)
                        {
                            exit = true;
                            break;
                        }
                        pos += 2;
                        if (Utility.Get2ByteIntFromArray(_body, pos, _length, out _par_tag_length) == false)
                        {
                            exit = true;
                            break;
                        }
                        pos += 2;
                        switch (_par_tag)
                        {
                            case 0x020C: // 524
                                if (((pos + _par_tag_length - 1) <= _length) && (_par_tag_length == 2))
                                {
                                    byte[] temp = new byte[_par_tag_length];
                                    Array.Copy(_body, pos, temp, 0, _par_tag_length);
                                    pos += _par_tag_length;
                                    sar_msg_ref_num = BitConverter.ToInt16(temp, 0);
                                    logMessage(LogLevels.LogSteps, "sar_msg_ref_num : " + sar_msg_ref_num);
                                }
                                else
                                    exit = true;

                                break;
                            case 0x020E: // 526
                                if ((pos <= _length) && (_par_tag_length == 1))
                                {
                                    sar_total_segments = _body[pos];
                                    logMessage(LogLevels.LogSteps, "sar_total_segments : " + Convert.ToString(sar_total_segments));
                                    pos++;
                                }
                                else
                                    exit = true;

                                break;
                            case 0x020F: // 527
                                if ((pos <= _length) && (_par_tag_length == 1))
                                {
                                    sar_segment_seqnum = _body[pos];
                                    logMessage(LogLevels.LogSteps, "sar_segment_seqnum : " + Convert.ToString(sar_segment_seqnum));
                                    pos++;
                                }
                                else
                                    exit = true;

                                break;
                            case 0x0427: // 1063
                                if ((pos <= _length) && (_par_tag_length == 1))
                                {
                                    _message_state = _body[pos];
                                    logMessage(LogLevels.LogSteps, "Message state : " + Convert.ToString(_message_state));
                                    pos++;
                                }
                                else
                                    exit = true;

                                break;
                            case 0x001E: // 30
                                if ((pos + _par_tag_length - 1) <= _length)
                                {
                                    _receipted_message_id = new byte[_par_tag_length];
                                    Array.Copy(_body, pos, _receipted_message_id, 0, _par_tag_length);
                                    _receipted_message_id_len = Convert.ToByte(_par_tag_length);
                                    pos += _par_tag_length;
                                    logMessage(LogLevels.LogSteps, "Delivered message id : " + Utility.ConvertArrayToString(_receipted_message_id, _receipted_message_id_len));
                                }
                                else
                                    exit = true;
                                break;
                            default:
                                if ((pos + _par_tag_length - 1) <= _length)
                                    pos += _par_tag_length;
                                else
                                    exit = true;
                                logMessage(LogLevels.LogInfo, "_par_tag : " + Convert.ToString(_par_tag));
                                logMessage(LogLevels.LogInfo, "_par_tag_length : " + Convert.ToString(_par_tag_length));
                                break;

                        }
                    }
                    //logMessage(LogLevels.LogDebug, "Delivery Receipt Processing Exit value - " + Convert.ToString(exit));
                    if (exit)
                        isDeliveryReceipt = false;
                }

                if ((sar_msg_ref_num > 0) && ((sar_total_segments > 0) && ((sar_segment_seqnum > 0) && (isUdhiSet))))
                {
                    lock (sarMessages.SyncRoot)
                    {
                        SortedList tArr = new SortedList();
                        if (sarMessages.ContainsKey(sar_msg_ref_num))
                        {
                            tArr = (SortedList)sarMessages[sar_msg_ref_num];
                            if (tArr.ContainsKey(sar_segment_seqnum))
                                tArr[sar_segment_seqnum] = _short_message;
                            else
                                tArr.Add(sar_segment_seqnum, _short_message);
                            bool isFull = true;
                            byte i;
                            for (i = 1; i <= sar_total_segments; i++)
                            {
                                if (!tArr.ContainsKey(i))
                                {
                                    isFull = false;
                                    break;
                                }//if
                            }//for
                            if (!isFull)
                            {
                                sarMessages[sar_msg_ref_num] = tArr;
                                sendDeliverSmResp(sequence_number, StatusCodes.ESME_ROK);
                                return;
                            }
                            else
                            {
                                _sm_length = 0;
                                for (i = 1; i <= sar_total_segments; i++)
                                {
                                    _sm_length += ((byte[])tArr[i]).Length;
                                }
                                _short_message = new byte[_sm_length + 100];
                                _sm_length = 0;
                                for (i = 1; i <= sar_total_segments; i++)
                                {
                                    Array.Copy(((byte[])tArr[i]), 0, _short_message, _sm_length, ((byte[])tArr[i]).Length);
                                    _sm_length += ((byte[])tArr[i]).Length;
                                }
                                sarMessages.Remove(sar_msg_ref_num);
                            }
                        }//if
                        else
                        {
                            tArr.Add(sar_segment_seqnum, _short_message);
                            sarMessages.Add(sar_msg_ref_num, tArr);
                            sendDeliverSmResp(sequence_number, StatusCodes.ESME_ROK);
                            return;
                        }
                    }//lock
                }
                string to;
                string from;
                string textString;

                string hexString = "";

                byte messageState = 0;
                string receiptedMessageID = "";

                to = Encoding.ASCII.GetString(_dest_addr, 0, daLength);
                from = Encoding.ASCII.GetString(_source_addr, 0, saLength);
                if (_data_coding == 8) //USC2
                    textString = Encoding.BigEndianUnicode.GetString(_short_message, 0, _sm_length);
                else
                    textString = Encoding.UTF8.GetString(_short_message, 0, _sm_length);
                hexString = Utility.ConvertArrayToHexString(_short_message, _sm_length);

                if (isDeliveryReceipt)
                {
                    isDeliveryReceipt = true;
                    try
                    {
                        messageState = _message_state;
                        receiptedMessageID = Encoding.ASCII.GetString(_receipted_message_id, 0, _receipted_message_id_len - 1);
                    }
                    catch (Exception ex)
                    {
                        Dictionary<string, string> dictionaryText = Utility.ParseDeliveryMessageText(textString);
                        messageState = Utility.MessageDeliveryStatus(dictionaryText["stat"]);
                        receiptedMessageID = dictionaryText["id"];
                    }
                    receiptedMessageID = Helper.ParseDeliveryReportId(smsc.MessageIdType, receiptedMessageID);
                    ////if (!ReferenceEquals(tlvList, null))
                    ////{
                    ////    tlvList.TryGetValue("id", out receiptedMessageID);
                    ////    receiptedMessageID = receiptedMessageID.Replace(" ", "");
                    ////    //receiptedMessageID = Convert.ToInt32(receiptedMessageID, 16).ToString();
                    ////}
                    //String dd = String.Empty, sd = String.Empty;
                    //if (!ReferenceEquals(shortMessageValueList, null))
                    //{
                    //    try
                    //    {
                    //        string submitDate = shortMessageValueList["submit date"];
                    //        DateTime sd_dt = DateTime.ParseExact(submitDate, ConfigurationManager.AppSettings["DeliverySmDateFormat"], System.Globalization.CultureInfo.InvariantCulture);
                    //        sd = sd_dt.ToString("dd-MMM-yyyy HH:mm:ss");
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | Error parsing submit date" + ex.ToString());
                    //    }

                    //    try
                    //    {
                    //        string doneDate = shortMessageValueList["done date"];
                    //        DateTime dd_dt = DateTime.ParseExact(doneDate, ConfigurationManager.AppSettings["DeliverySmDateFormat"], System.Globalization.CultureInfo.InvariantCulture);
                    //        dd = dd_dt.ToString("dd-MMM-yyyy HH:mm:ss");
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | Error parsing done date" + ex.ToString());
                    //    }
                    //}
                    //logMessage(LogLevels.LogInfo, "decodeAndProcessDeliverSm | Received for message:" + receiptedMessageID + "\tstatus:" + Utility.MessageDeliveryStatus(messageState));
                    //#region [ Update Data ]
                    //string sql = String.Format(
                    //    @"INSERT OR IGNORE INTO [DeliveryReports] ([MessageId], [SubmitDate], [DoneDate], [Status])
                    //            VALUES ('{0}', '{1}', '{2}', '{3}')"
                    //    , receiptedMessageID
                    //    , sd
                    //    , dd
                    //    , Utility.MessageDeliveryStatus(messageState) //, tlvList["stat"]
                    //);
                    //LocalStorage.ExecuteNonQuery(sql);
                    //#endregion
                }
                //SMSC smsc = new SMSC(
                //    smsc.Name,
                //    smsc.VendorName,
                //    smsc.Host,
                //    smsc.Port,
                //    smsc.SystemId,
                //    smsc.Password,
                //    smsc.SystemType
                //);
                DeliverSmEventArgs evArg = new DeliverSmEventArgs(sequence_number, to, from, textString, hexString, _data_coding, _esm_class, isDeliveryReceipt, messageState, receiptedMessageID, smsc.VendorName, smsc.Name);
                processDeliverSm(evArg);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | " + ex.ToString());
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | PDU |" + BitConverter.ToString(_body));
                sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
            }

        }//decodeAndProcessDeliverSm

        private void decodeAndProcessDataSm(int sequence_number, byte[] _body, int _length)
        {
            if (_length < 17)
            {
                sendDeliverSmResp(sequence_number, StatusCodes.ESME_RINVCMDLEN);
                return;
            }
            try
            {
                bool isDeliveryReceipt = false;
                byte _source_addr_ton, _source_addr_npi, _dest_addr_ton, _dest_addr_npi;
                byte _priority_flag, _data_coding, _esm_class;
                byte[] _source_addr = new byte[21];
                byte[] _dest_addr = new byte[21];
                byte saLength, daLength;
                byte[] _short_message = new byte[254];
                int pos;
                ////
                /// For Body Print
                /////////////////////////////////////
                /// Message Delivery Params
                /// 
                byte[] _receipted_message_id = new byte[254];
                byte _receipted_message_id_len = 0;
                byte _message_state = 0;


                pos = 0;
                while ((pos < 5) && (_body[pos] != 0x00))
                    pos++;
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x04");
                    return;
                }
                _source_addr_ton = _body[++pos];
                _source_addr_npi = _body[++pos];
                pos++;
                saLength = 0;
                while ((saLength < 20) && (_body[pos] != 0x00))
                {
                    _source_addr[saLength] = _body[pos];
                    pos++;
                    saLength++;
                }
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x05");
                    return;
                }
                _dest_addr_ton = _body[++pos];
                _dest_addr_npi = _body[++pos];
                pos++;
                daLength = 0;
                while ((daLength < 20) && (_body[pos] != 0x00))
                {
                    _dest_addr[daLength] = _body[pos];
                    pos++;
                    daLength++;
                }
                if (_body[pos] != 0x00)
                {
                    sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
                    logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm returned UNKNOWNERR on 0x06");
                    return;
                }
                _esm_class = _body[++pos];
                switch (_esm_class)
                {
                    case 0x00:
                        break;
                    case 0x04:
                        //logMessage(LogLevels.LogSteps, "Delivery Receipt Received");
                        isDeliveryReceipt = true;
                        break;
                    default:
                        logMessage(LogLevels.LogSteps | LogLevels.LogWarnings, "Unknown esm_class for DATA_SM : " + Utility.GetHexFromByte(_esm_class));
                        break;
                }
                pos += 1;
                _priority_flag = _body[++pos];
                pos += 4;
                _data_coding = _body[++pos];
                pos += 1;
                if (isDeliveryReceipt)
                {
                    int _par_tag, _par_tag_length;
                    bool exit = false;
                    while ((pos < _length) && (exit == false))
                    {
                        if (Utility.Get2ByteIntFromArray(_body, pos, _length, out _par_tag) == false)
                        {
                            exit = true;
                            break;
                        }
                        pos += 2;
                        if (Utility.Get2ByteIntFromArray(_body, pos, _length, out _par_tag_length) == false)
                        {
                            exit = true;
                            break;
                        }
                        pos += 2;
                        switch (_par_tag)
                        {
                            case 0x0427:
                                if ((pos <= _length) && (_par_tag_length == 1))
                                {
                                    _message_state = _body[pos];
                                    //logMessage(LogLevels.LogSteps, "Message state : " + Convert.ToString(_message_state));
                                    pos++;
                                }
                                else
                                    exit = true;

                                break;
                            case 0x001E:
                                if ((pos + _par_tag_length - 1) <= _length)
                                {
                                    _receipted_message_id = new byte[_par_tag_length];
                                    Array.Copy(_body, pos, _receipted_message_id, 0, _par_tag_length);
                                    _receipted_message_id_len = Convert.ToByte(_par_tag_length);
                                    pos += _par_tag_length;
                                    //logMessage(LogLevels.LogSteps, "Delivered message id : " + Utility.ConvertArrayToString(_receipted_message_id, _receipted_message_id_len - 1));
                                }
                                else
                                    exit = true;
                                break;
                            default:
                                if ((pos + _par_tag_length - 1) <= _length)
                                    pos += _par_tag_length;
                                else
                                    exit = true;
                                logMessage(LogLevels.LogInfo, "_par_tag : " + Convert.ToString(_par_tag));
                                logMessage(LogLevels.LogInfo, "_par_tag_length : " + Convert.ToString(_par_tag_length));
                                break;

                        }
                    }
                    //logMessage(LogLevels.LogDebug, "Delivery Receipt Processing Exit value - " + Convert.ToString(exit));
                    if (exit)
                        isDeliveryReceipt = false;
                }

                string to;
                string from;
                string textString = "";

                string hexString = "";

                byte messageState = 0;
                string receiptedMessageID = "";

                to = Encoding.ASCII.GetString(_dest_addr, 0, daLength);
                from = Encoding.ASCII.GetString(_source_addr, 0, saLength);

                if (isDeliveryReceipt)
                {
                    isDeliveryReceipt = true;
                    messageState = _message_state;
                    receiptedMessageID = Encoding.ASCII.GetString(_receipted_message_id, 0, _receipted_message_id_len - 1); ;
                }

                //SMSC smsc = new SMSC(
                //    smsc.Name,
                //    smsc.VendorName,
                //    smsc.Host,
                //    smsc.Port,
                //    smsc.SystemId,
                //    smsc.Password,
                //    smsc.SystemType
                //);
                DeliverSmEventArgs evArg = new DeliverSmEventArgs(sequence_number, to, from, textString, hexString, _data_coding, _esm_class, isDeliveryReceipt, messageState, receiptedMessageID, smsc.VendorName, smsc.Name);
                processDeliverSm(evArg);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "decodeAndProcessDeliverSm | " + ex.ToString());
                sendDeliverSmResp(sequence_number, StatusCodes.ESME_RUNKNOWNERR);
            }

        }//decodeAndProcessDataSm

        private void checkSystemIntegrity(Object state)
        {
            try
            {
                if (mustBeConnected)
                {
                    if (connectionState == ConnectionStates.SMPP_BINDED)
                    {
                        if (enquireLinkSendTime <= enquireLinkResponseTime)
                        {
                            enquireLinkSendTime = DateTime.Now;
                            sendEnquireLink(smsc.SequenceNumber);
                            lastSeenConnected = DateTime.Now;
                        }
                        else
                        {
                            logMessage(LogLevels.LogSteps | LogLevels.LogErrors, "checkSystemIntegrity | ERROR #9001 - no response to Enquire Link");
                            tryToReconnect();
                        }
                    }
                    else
                    {
                        if (((TimeSpan)(DateTime.Now - lastSeenConnected)).TotalSeconds > KernelParameters.CanBeDisconnected)
                        {
                            logMessage(LogLevels.LogSteps | LogLevels.LogErrors, "checkSystemIntegrity | ERROR #9002 - diconnected more than " + Convert.ToString(KernelParameters.CanBeDisconnected) + " seconds");
                            lastSeenConnected = DateTime.Now.AddSeconds(KernelParameters.CanBeDisconnected);
                            tryToReconnect();
                        }
                    }
                }
                else
                {
                    if (connectionState == ConnectionStates.SMPP_UNBIND_SENT)
                    {
                        if (((TimeSpan)(DateTime.Now - lastPacketSentTime)).TotalSeconds > KernelParameters.WaitPacketResponse)
                        {
                            disconnectSocket();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "checkSystemIntegrity | " + ex.ToString());
            }

        }//checkSystemIntegrity

        public void sendDeliverSmResp(int sequence_number, int command_status)
        {
            try
            {
                byte[] _PDU = new byte[17];

                Utility.CopyIntToArray(17, _PDU, 0);

                Utility.CopyIntToArray(0x80000005, _PDU, 4);

                Utility.CopyIntToArray(command_status, _PDU, 8);

                Utility.CopyIntToArray(sequence_number, _PDU, 12);

                _PDU[16] = 0;

                Send(_PDU, 17);

            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendDeliverSmResp | " + ex.ToString());
            }
        }//sendDeliverSmResp

        private void sendGenericNack(int sequence_number, int command_status)
        {
            try
            {
                byte[] _PDU = new byte[16];

                Utility.CopyIntToArray(16, _PDU, 0);

                Utility.CopyIntToArray(0x80000000, _PDU, 4);

                Utility.CopyIntToArray(command_status, _PDU, 8);

                Utility.CopyIntToArray(sequence_number, _PDU, 12);

                Send(_PDU, 16);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendGenericNack | " + ex.ToString());
            }

        }//sendGenericNack

        private void sendEnquireLink(int sequence_number)
        {
            try
            {
                byte[] _PDU = new byte[16];
                Utility.CopyIntToArray(16, _PDU, 0);

                Utility.CopyIntToArray(0x00000015, _PDU, 4);

                Utility.CopyIntToArray(0x00000000, _PDU, 8);

                Utility.CopyIntToArray(sequence_number, _PDU, 12);

                Send(_PDU, 16);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendEnquireLink | " + ex.ToString());
            }
        }//sendEnquireLink

        private void sendEnquireLinkResp(int sequence_number)
        {
            try
            {
                byte[] _PDU = new byte[16];
                Utility.CopyIntToArray(16, _PDU, 0);

                Utility.CopyIntToArray(0x80000015, _PDU, 4);

                Utility.CopyIntToArray(0x00000000, _PDU, 8);

                Utility.CopyIntToArray(sequence_number, _PDU, 12);

                Send(_PDU, 16);
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "sendEnquireLink | " + ex.ToString());
            }
        }//sendEnquireLink

        private void sendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                //logMessage(LogLevels.LogSteps | LogLevels.LogPdu, "Sent " + bytesSent.ToString() + " bytes");
            }
            catch (Exception ex)
            {
                logMessage(LogLevels.LogExceptions, "Send | " + ex.ToString());
            }
        }//Send

        private byte getAddrTon(string address)
        {
            int i;
            for (i = 0; i < address.Length; i++)
                if (!Char.IsDigit(address, i))
                {
                    return AddressTons.Alphanumeric;
                }
            if (address.Length == nationalNumberLength)
                return AddressTons.National;
            if (address.Length > nationalNumberLength)
                return AddressTons.International;
            return AddressTons.Unknown;
        }//getAddrTon
        private byte getAddrNpi(string address)
        {
            int i;
            for (i = 0; i < address.Length; i++)
                if (!Char.IsDigit(address, i))
                {
                    return AddressNpis.Unknown;
                }
            return AddressNpis.ISDN;
        }//getAddrTon
        #endregion Private Functions

    }//SMPPClient


}
