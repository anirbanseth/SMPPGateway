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
 * Licensed under the terms of the GNU Lesser General public static License:
 * 		http://www.opensource.org/licenses/lgpl-license.php
 * 
 * For further information visit:
 * 		http://easysmpp.sf.net/
 * 
 * 
 * "Support Open Source software. What about a donation today?"
 *
 * 
 * File Name: KernelParameters.cs
 * 
 * File Authors:
 * 		Balan Name, http://balan.name
 */
using System;

namespace SMSGateway.SMSCClient
{
    public class KernelParameters
    {
        public static int MaxBufferSize = 1048576; // 1MB

        public static int MaxPduSize = 131072;

        public static int ReconnectTimeout = 45000; // miliseconds

        public static int WaitPacketResponse = 30; //seconds

        public static int CanBeDisconnected = 180; //seconds - BETTER TO BE MORE THAN TryToReconnectTimeOut

        public static int NationalNumberLength = 8;

        public static int MaxUndeliverableMessages = 30;

        public static byte AskDeliveryReceipt = 0; //NoReceipt = 0;

        public static bool SplitLongText = false;

        public static bool UseEnquireLink = false;

        public static int EnquireLinkTimeout = 45000; //miliseconds

        public static uint MaxSequenceNumber = UInt32.MaxValue; //100000000; // 99999999 + 1
        public static byte MaxIdentificationNumber = Byte.MaxValue; //0xFF;

        //public static int waitForResponse = 30; // wait for submit_SM response in seconds

        public static int DeliveryLoadTimeout = 100;
        public static int DeliverySendTimeout = 5;
    }
}
