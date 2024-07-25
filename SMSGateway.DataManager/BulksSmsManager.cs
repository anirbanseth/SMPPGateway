using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1;
using SMSGateway.DataManager.General;
using SMSGateway.Entity;

namespace SMSGateway.DataManager
{
    public class BulksSmsManager
    {
        #region [ Mark messages ]
        public async Task<int> MarkMessages(string status, List<string> activeOperators, string newStatus)
        {
            string operatorsKey = !ReferenceEquals(activeOperators, null) && activeOperators.Any()
                ? String.Join(",", activeOperators.Select(x => $"\'{x}\'"))
                : $"''";

            string query = $"UPDATE send_sms " +
                $"SET status = @newStatus " +
                $"WHERE status = @status " +
                $"AND operator IN ({operatorsKey}) " +
                $"ORDER BY serial_number " +
                $"LIMIT 100;";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddVarcharPara("newStatus", 20, newStatus);
            db.AddVarcharPara("status", 20, status);
            return await db.RunActionQueryAsync();
        }
        #endregion

        #region [ Get Marked Messages ]
        public async Task<List<SmsMessage>> GetMarkedMessages(string status)
        {
            string query = $"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED ; " +
              $"SELECT destination, coding, message, senderid, enitityid, send_sms_id, " +
              $"templateid, piority, status, operator, retry_count, dlt_cost, sms_cost, " +
              $"sms_campaign_head_details_id, sms_campaign_details_id, smpp_user_details_id, " +
              $"sms_cost_mode, tm_id " +
              $"FROM send_sms WHERE status = @status; ";
            //$"AND operator = '$operator'"; 

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddVarcharPara("status", 20, status);
            DataTable dataTable = await db.GetTableAsync();

            return ParseSmsMessages(dataTable);
        }

        private List<SmsMessage> ParseSmsMessages(DataTable dataTable)
        {
            List<SmsMessage> messages = new List<SmsMessage>();
            if (!ReferenceEquals(dataTable, null))
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    SmsMessage m = new SmsMessage();
                    m.From = row.Field<string>("senderid");
                    m.To = row.Field<Int64>("destination").ToString();
                    m.Coding = row.Field<int>("coding");
                    m.Message = row.Field<string>("message");
                    m.From = row.Field<string>("senderid");
                    m.AskDeliveryReceipt = true;
                    m.Priority = (byte) row.Field<int>("piority");
                    m.PEID = row.Field<string>("enitityid");
                    m.TMID = row.Field<string>("tm_id");
                    m.TemplateId = row.Field<string>("templateid");
                    m.RefId = row.Field<Int64>("send_sms_id").ToString();
                    m.Operator = row.Field<string>("operator");
                    m.RetryIndex = row.Field<int>("retry_count");
                    m.AdditionalData["sms_campaign_head_details_id"] = row.Field<Int64>("sms_campaign_head_details_id");
                    m.AdditionalData["sms_campaign_details_id"] = row.Field<Int64>("sms_campaign_details_id");
                    m.AdditionalData["smpp_user_details_id"] = row.Field<Int32>("smpp_user_details_id");
                    m.AdditionalData["dlt_cost"] = row.Field<Decimal?>("dlt_cost");
                    m.AdditionalData["sms_cost"] = row.Field<Decimal?>("sms_cost");
                    m.AdditionalData["sms_cost_mode"] = row.Field<string>("sms_cost_mode");
                    messages.Add(m);
                }
            }
            return messages;
        }

        #endregion

        #region [ Update Send Sms ]
        public async Task<int> UpdateSendSmsById(
            List<long> id,
            string status
        )
        {
            string sql = $"UPDATE send_sms " +
                    $"SET   status = @status " +
                    $"WHERE send_sms_id IN ({String.Join(",", id.Select(x => x.ToString()))})";

            MySqlDbManager db = new MySqlDbManager(sql, true);
            db.AddVarcharPara("status", 20, status);
            return await db.RunActionQueryAsync();
        }
        #endregion

        #region [ Update Send Sms ]
        public async Task<int> UpdateSendSms(
            long send_sms_id,
            string status,
            int retry_count,
            string smpp,
            decimal dlt_cost,
            decimal sms_cost
        )
        {
            string sql = $"UPDATE send_sms " +
                $"SET dlt_cost = @dlt_cost, " +
                $"sms_cost = @sms_cost, " +
                $"status = @status, " +
                $"smpp = @smpp, " +
                $"retry_count = retry_count + 1 " +
                $"WHERE send_sms_id = @send_sms_id; ";

            MySqlDbManager db = new MySqlDbManager(sql, true);
            db.AddIntegerBigPara("send_sms_id", send_sms_id);
            db.AddDecimalPara("dlt_cost", 5, 10, dlt_cost);
            db.AddDecimalPara("sms_cost", 5, 10, sms_cost);
            db.AddVarcharPara("status", 20, status);
            db.AddIntegerPara("retry_count", retry_count);
            db.AddVarcharPara("smpp", 20, smpp);
            return await db.RunActionQueryAsync();
        }
        #endregion

        #region [ Update Submit Sms ]
        //public async Task<int> UpdateSubmitSmsById(
        //    List<long> id,
        //    string status
        //)
        //{
        //    string sql = $"UPDATE submit_sms " +
        //            $"SET   status = @status" +
        //            $"WHERE submit_sms_id IN ({ String.Join(",", id.Select(x => x.ToString())) })";

        //    MySqlDbManager db = new MySqlDbManager(sql, true);
        //    db.AddVarcharPara("status", 20, status);
        //    return await db.RunActionQueryAsync();
        //}
        #endregion

        #region [ Save Send SMS ]
        public async Task<ulong> SaveSendSms(
            //long send_sms_id,
            long? sms_campaign_head_details_id,
            long? sms_campaign_details_id,
            int? smpp_user_details_id,
            string message,
            string senderid,
            string? enitityid,
            string? templateid,
            long destination,
            int piority,
            string? message_id,
            long? submit_sms_id,
            int coding,
            int? smsc_details_id,
            DateTime create_date,
            string status,
            decimal dlt_cost,
            decimal sms_cost,
            int? serial_number,
            string @operator,
            string smpp,
            string from,
            int? session_id,
            int retry_count,
            char? sms_cost_mode,
            string source,
            string? tm_id
        )
        {
            string query = $"insert into send_sms (" +
                //$"send_sms_id, " +
                $"sms_campaign_head_details_id, " +
                $"sms_campaign_details_id, " +
                $"smpp_user_details_id, " +
                $"message, " +
                $"senderid, " +
                $"enitityid, " +
                $"templateid, " +
                $"destination, " +
                $"piority, " +
                $"message_id, " +
                $"submit_sms_id, " +
                $"coding, " +
                $"smsc_details_id, " +
                $"create_date, " +
                $"status, " +
                $"dlt_cost, " +
                $"sms_cost, " +
                $"serial_number, " +
                $"operator, " +
                $"smpp, " +
                $"`from`, " +
                $"session_id, " +
                $"retry_count, " +
                $"sms_cost_mode, " +
                $"source, " +
                $"tm_id" +
                $") values (" +
                //$"@send_sms_id, " +
                $"@sms_campaign_head_details_id, " +
                $"@sms_campaign_details_id, " +
                $"@smpp_user_details_id, " +
                $"@message, " +
                $"@senderid, " +
                $"@enitityid, " +
                $"@templateid, " +
                $"@destination, " +
                $"@piority, " +
                $"@message_id, " +
                $"@submit_sms_id, " +
                $"@coding, " +
                $"@smsc_details_id, " +
                $"@create_date, " +
                $"@status, " +
                $"@dlt_cost, " +
                $"@sms_cost, " +
                $"@serial_number, " +
                $"@operator, " +
                $"@smpp, " +
                $"@from, " +
                $"@session_id, " +
                $"@retry_count, " +
                $"@sms_cost_mode, " +
                $"@source, " +
                $"@tm_id" +
                $"); select LAST_INSERT_ID() ;";

            MySqlDbManager db = new MySqlDbManager(query, true);
            //db.AddIntegerBigPara("send_sms_id", send_sms_id);
            db.AddIntegerBigPara("sms_campaign_head_details_id", sms_campaign_head_details_id);
            db.AddIntegerBigPara("sms_campaign_details_id", sms_campaign_details_id);
            db.AddIntegerPara("smpp_user_details_id", smpp_user_details_id);
            db.AddVarcharPara("message", 1000, message);
            db.AddVarcharPara("senderid", 10, senderid);
            db.AddVarcharPara("enitityid", 200, enitityid);
            db.AddVarcharPara("templateid", 200, templateid);
            db.AddIntegerBigPara("destination", destination);
            db.AddIntegerPara("piority", piority);
            db.AddVarcharPara("message_id", 255, message_id);
            db.AddIntegerBigPara("submit_sms_id", submit_sms_id);
            db.AddIntegerPara("coding", coding);
            db.AddIntegerPara("smsc_details_id", smsc_details_id);
            db.AddDateTimePara("create_date", create_date);
            db.AddVarcharPara("status", 20, status);
            db.AddDecimalPara("dlt_cost", 10, 5, dlt_cost);
            db.AddDecimalPara("sms_cost", 10, 5, sms_cost);
            db.AddIntegerPara("serial_number", serial_number);
            db.AddVarcharPara("operator", 20, @operator);
            db.AddVarcharPara("smpp", 20, smpp);
            db.AddVarcharPara("from", 20, from);
            db.AddIntegerPara("session_id", session_id);
            db.AddIntegerPara("retry_count", retry_count);
            db.AddCharPara("sms_cost_mode", 1, sms_cost_mode);
            db.AddVarcharPara("source", 3, source);
            db.AddNVarcharPara("tm_id", 255, tm_id);
            DataTable dataTable = await db.GetTableAsync();

            if (!ReferenceEquals(dataTable, null))
                return (ulong)dataTable.Rows[0][0];

            return 0;
        }
        #endregion

        #region [ Save Sent Sms ]
        public async Task SaveSentSms(
            long sent_sms_id,
            long send_sms_s1_id,
            long send_sms_p1_id,
            long send_sms_id,
            long sms_campaign_head_details_id,
            long sms_campaign_details_id,
            int smpp_user_details_id,
            string message,
            string senderid,
            string enitityid,
            string templateid,
            string destination,
            int piority,
            int coding,
            string smsc_details_id,
            DateTime create_date,
            string status,
            string dlt_cost,
            string sms_cost,
            DateTime p1_move_date,
            DateTime s1_move_date,
            DateTime move_date,
            string pdu_id,
            string sequence_id,
            string message_id,
            string smpp_instance,
            int retry_index,
            string sms_cost_mode,
            string tm_id
        )
        {
            string query = $"INSERT INTO sent_sms( " +
                $" send_sms_s1_id, send_sms_p1_id, send_sms_id, sms_campaign_head_details_id, sms_campaign_details_id " +
                $", smpp_user_details_id, message, senderid, enitityid, templateid, destination, piority, coding, smsc_details_id " +
                $", create_date, status, dlt_cost, sms_cost, p1_move_date, s1_move_date, move_date, pdu_id, sequence_id, message_id " +
                $", smpp_instance, retry_index, sms_cost_mode, tm_id)VALUES(" +
                $" @send_sms_s1_id, @send_sms_p1_id, @send_sms_id, @sms_campaign_head_details_id, @sms_campaign_details_id " +
                $", @smpp_user_details_id, @message, @senderid, @enitityid, @templateid, @destination, @piority, @coding, @smsc_details_id " +
                $", @create_date, @status, @dlt_cost, @sms_cost, @p1_move_date, @s1_move_date, @move_date, @pdu_id, @sequence_id, @message_id " +
                $", @smpp_instance, @retry_index, @sms_cost_mode, @tm_id)";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddIntegerBigPara("sent_sms_id", sent_sms_id);
            db.AddIntegerBigPara("send_sms_s1_id", send_sms_s1_id);
            db.AddIntegerBigPara("send_sms_p1_id", send_sms_p1_id);
            db.AddIntegerBigPara("send_sms_id", send_sms_id);
            db.AddIntegerBigPara("sms_campaign_head_details_id", sms_campaign_head_details_id);
            db.AddIntegerBigPara("sms_campaign_details_id", sms_campaign_details_id);
            db.AddIntegerPara("smpp_user_details_id", smpp_user_details_id);
            db.AddLongTextPara("message", -1, message);
            db.AddVarcharPara("senderid", 200, senderid);
            db.AddVarcharPara("enitityid", 200, enitityid);
            db.AddVarcharPara("templateid", 200, templateid);
            db.AddVarcharPara("destination", 200, destination);
            db.AddIntegerPara("piority", piority);
            db.AddIntegerPara("coding", coding);
            db.AddVarcharPara("smsc_details_id", 200, smsc_details_id);
            db.AddTimeStampPara("create_date", create_date);
            db.AddVarcharPara("status", 200, status);
            db.AddVarcharPara("dlt_cost", 50, dlt_cost);
            db.AddVarcharPara("sms_cost", 50, sms_cost);
            db.AddTimeStampPara("p1_move_date", p1_move_date);
            db.AddTimeStampPara("s1_move_date", s1_move_date);
            db.AddTimeStampPara("move_date", move_date);
            db.AddVarcharPara("pdu_id", 50, pdu_id);
            db.AddVarcharPara("sequence_id", 50, sequence_id);
            db.AddVarcharPara("message_id", 50, message_id);
            db.AddVarcharPara("smpp_instance", 50, smpp_instance);
            db.AddIntegerPara("retry_index", retry_index);
            db.AddVarcharPara("sms_cost_mode", 1, sms_cost_mode);
            db.AddVarcharPara("tm_id", 255, tm_id);
            await db.RunActionQueryAsync();

        }
        #endregion

        #region [ Delivery Report ]
        public async Task SaveDeliveryReport(
                string message_id,
                string destination,
                string sender,
                string sms_dlr_status_id,
                string smsc_details_id,
                int smpp_user_details_id,
                string message,
                DateTime? submit_date,
                DateTime? dlr_status_date,
                string errorCode,
                string shortmessage,
                DateTime create_date
            )
        {
            //string query = $"INSERT INTO sms_dlr_all_status (" +
            //    $"message_id,destination,sender,sms_dlr_status_id," +
            //    $"smsc_details_id,smpp_user_details_id,message,submit_date,dlr_status_date,errorCode,shortmessage" +
            //    $") VALUES (" +
            //    $"'{message_id}','{destination}','{sender}','{sms_dlr_status_id}'," +
            //    $"'{smsc_details_id}',{smpp_user_details_id},'{message}','{submit_date}','{dlr_status_date}','{errorCode}','{shortmessage}'" +
            //    $");";


            string query = $"INSERT INTO sms_dlr_all_status (" +
                $"message_id,destination,sender,sms_dlr_status_id," +
                $"smsc_details_id,smpp_user_details_id,message,submit_date,dlr_status_date,errorCode,shortmessage, saved_on" +
                $") VALUES (" +
                $"@message_id, @destination, @sender, @sms_dlr_status_id," +
                $"@smsc_details_id,@smpp_user_details_id, @message, @submit_date, @dlr_status_date, @errorCode, @shortmessage, @saved_on" +
                $");";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddVarcharPara("message_id", 255, message_id);
            db.AddVarcharPara("destination", 24, destination);
            db.AddVarcharPara("sender", 24, sender);
            db.AddVarcharPara("sms_dlr_status_id", 10, sms_dlr_status_id);
            db.AddVarcharPara("smsc_details_id", 50, smsc_details_id);
            db.AddIntegerBigPara("smpp_user_details_id", smpp_user_details_id);
            db.AddVarcharPara("message", 200, message);
            db.AddTimeStampPara("submit_date", submit_date);
            db.AddTimeStampPara("dlr_status_date", dlr_status_date);
            db.AddVarcharPara("errorCode", 200, errorCode);
            db.AddVarcharPara("shortmessage", -1, shortmessage);
            db.AddDateTimePara("saved_on", create_date);
            await db.RunActionQueryAsync();

        }
        #endregion

        #region [ Delivery Report Processor ]
        public async Task<int> ProcessDeliveryReport()
        {
            MySqlDbManager db = new MySqlDbManager("proc_delivery_report");
            return await db.RunActionQueryAsync();
        }
        #endregion
    }
}
