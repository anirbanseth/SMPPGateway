using MySqlX.XDevAPI;
using SMSGateway.DataManager.General;
using SMSGateway.Entity;
using SMSGateway.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SMSGateway.DataManager
{
    public class SmppServerManager
    {

        public async Task<int> MarkDeliveries(long smpp_user_details_id, int count, string status, int retry_count, string newstatus)
        {
            string query = $"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED ; " +
                $"UPDATE smpp_server_texts " +
                $"SET status = @newstatus " +
                $"WHERE smpp_user_details_id = @smpp_user_details_id and retry_count <= @retry_count and status = @status " +
                $"ORDER BY retry_on LIMIT @count; ";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddIntegerBigPara("smpp_user_details_id", smpp_user_details_id);
            db.AddIntegerPara("retry_count", retry_count);
            db.AddVarcharPara("status", 5, status);
            db.AddVarcharPara("newstatus", 5, newstatus);
            db.AddIntegerPara("count", count);
            return await db.RunActionQueryAsync();
        }

        public async Task<List<SmppDelivery>> GetPendingDeliveries(long smpp_user_details_id, int count, string status, int retry_count, DateTime? cutOffDate = null)
        {
            //throw new NotImplementedException();
            //return new List<SmppDelivery>();
            string query = $"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED ; " +
                $"select " +
                $"smpp_server_text_id, smpp_server_session_id, smpp_server_user_details_id, smpp_user_details_id, " +
                $"service_type, " +
                $"source_addr_ton as dest_addr_ton, " +
                $"source_addr_npi as dest_addr_npi, " +
                $"source_addr as dest_addr, " +
                $"dest_addr_ton as source_addr_ton, " +
                $"dest_addr_npi as source_addr_npi, " +
                $"dest_addr as source_addr, " +
                $"esm_class, " +
                $"priority_flag, schedule_delivery_time, validity_period, registered_delivery, " +
                $"replace_if_present_flag, data_coding, sm_default_msg_id, short_message, " +
                $"message_identification, total_parts, part_number, pe_id, " +
                $"tm_id, template_id, message_id, send_sms_id, " +
                $"message_state, err_code, message_state_updated_on, status, " +
                $"retry_count, retry_on, createdon, updatedon " +
                $"from smpp_server_texts " +
                $"where 1=1 and smpp_user_details_id = @smpp_user_details_id and retry_count <= @retry_count and status = @status " +
                $"LIMIT @count; ";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddIntegerBigPara("smpp_user_details_id", smpp_user_details_id);
            db.AddIntegerPara("retry_count", retry_count);
            db.AddVarcharPara("status", 5, status);
            db.AddIntegerPara("count", count);
            DataTable dataTable = await  db.GetTableAsync();
            return ParseSmppDelivery(dataTable);
        }

        private List<SmppDelivery> ParseSmppDelivery(DataTable dataTable)
        {
            List<SmppDelivery> texts = new List<SmppDelivery>();
            if (!ReferenceEquals(dataTable , null))
            {
                for (int i = 0; i < dataTable.Rows.Count; i++)
                {
                    DataRow dr = dataTable.Rows[i];

                    SmppDelivery t = new SmppDelivery();
                    t.Id = dr.Field<Guid>("smpp_server_text_id");
                    t.SessionId = dr.Field<Guid>("smpp_server_session_id");
                    t.SmppUserId = dr.Field<long>("smpp_server_user_details_id");
                    t.UserId = dr.Field<long>("smpp_user_details_id");
                    t.ServiceType = dr.Field<string?>("service_type");
                    t.SourceAddressTon = dr.Field<byte>("source_addr_ton");
                    t.SourceAddressNpi = dr.Field<byte>("source_addr_npi");
                    t.SourceAddress = dr.Field<string?>("source_addr");
                    t.DestAddressTon = dr.Field<byte>("dest_addr_ton");
                    t.DestAddressNpi = dr.Field<byte>("dest_addr_npi");
                    t.DestAddress = dr.Field<string?>("dest_addr");
                    t.EsmClass = dr.Field<byte>("esm_class");
                    t.PriorityFlag = dr.Field<byte>("priority_flag");
                    t.ScheduledDeliveryTime = dr.Field<DateTime?>("schedule_delivery_time");
                    t.ValidityPeriod = dr.Field<DateTime?>("validity_period");
                    t.RegisteredDelivery = dr.Field<byte>("registered_delivery");
                    t.ReplaceIfPresentFlag = dr.Field<byte>("replace_if_present_flag");
                    t.DataCoding = dr.Field<byte>("data_coding");
                    t.SmDefaultMsgId = dr.Field<byte>("sm_default_msg_id");
                    t.ShortMessage = dr.Field<string?>("short_message");
                    t.MessageIdentification = dr.Field<byte?>("message_identification");
                    t.TotalParts = dr.Field<byte?>("total_parts");
                    t.PartNumber = dr.Field<byte?>("part_number");
                    t.PEID = dr.Field<string?>("pe_id");
                    t.TMID = dr.Field<string?>("tm_id");
                    t.TemplateId = dr.Field<string?>("template_id");
                    t.MessageId = dr.Field<string?>("message_id");
                    t.SendSmsId = dr.Field<ulong>("send_sms_id");
                    t.MessageState = (MessageState) dr.Field<byte>("message_state");
                    t.ErrorCode = dr.Field<string>("err_code");
                    t.MessageStateUpdatedOn = dr.Field<DateTime>("message_state_updated_on");
                    t.Status = dr.Field<string>("status");
                    t.RetryCount = dr.Field<byte>("retry_count");
                    t.RetryOn = dr.Field<DateTime>("retry_on");
                    t.CreatedOn = dr.Field<DateTime>("createdon");
                    t.UpdatedOn = dr.Field<DateTime>("updatedon");
                    texts.Add(t);
                }
            }

            return texts;
        }

        public Guid? SaveCommand(SmppCommand command)
        {
            //throw new NotImplementedException();
            return Guid.NewGuid();
        }

        public async Task<int> SaveDelivery(SmppDelivery smppDelivery)
        {
            //throw new NotImplementedException();
            string query = $"update smpp_server_texts " +
                $"set status = @status, updatedon = @updatedon " +
                $"WHERE smpp_server_text_id = @smpp_server_text_id;";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddVarcharPara("smpp_server_text_id", 36, smppDelivery.Id.ToString());
            db.AddVarcharPara("status", 5, smppDelivery.Status);
            db.AddIntegerPara("retry_count", smppDelivery.RetryCount);
            db.AddDateTimePara("retry_on", smppDelivery.RetryOn);
            db.AddDateTimePara("updatedon", smppDelivery.UpdatedOn);

            return await db.RunActionQueryAsync();

        }

        //public async Task<Guid> SaveSession(
        //    Guid? id, 
        //    long? smpp_server_user_details_id, 
        //    long? smpp_user_details_id,
        //    string? source_ip,
        //    DateTime? last_received,
        //    DateTime? start_date,
        //    DateTime? end_date,
        //    DateTime? createdon,
        //    DateTime? updateon            
        //)
        //{
        //    if ( id == null )
        //    {
        //        id = Guid.NewGuid();
        //    }
        //    return (Guid)id;
        //}

        public async Task<Guid?> SaveSession(
            SmppSession smppSession
        )
        {
            if (smppSession.Id == null)
            {
                smppSession.Id = Guid.NewGuid();
            }
            string query = $"insert into smpp_server_sessions (" +
                $"smpp_server_session_id, " +
                $"smpp_server_user_details_id, " +
                $"smpp_user_details_id, " +
                $"source_ip, " +
                $"last_received, " +
                $"start_date, " +
                $"end_date, " +
                $"createdon, " +
                $"updateon" +
                $") values (" +
                $"@smpp_server_session_id, " +
                $"@smpp_server_user_details_id, " +
                $"@smpp_user_details_id, " +
                $"@source_ip, " +
                $"@last_received, " +
                $"@start_date, " +
                $"@end_date, " +
                $"@createdon, " +
                $"@updateon" +
                $")" +
                $"ON DUPLICATE KEY " +
                $"UPDATE " +
                $"smpp_server_user_details_id = @smpp_server_user_details_id," +
                $"last_received = @last_received, " +
                $"end_date = @end_date, " +
                $"updateon = @updateon";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddVarcharPara("smpp_server_session_id", 36, smppSession.Id.ToString());
            db.AddIntegerBigPara("smpp_server_user_details_id", smppSession.SmppUserId);
            db.AddIntegerBigPara("smpp_user_details_id", smppSession.UserId);
            db.AddVarcharPara("source_ip", 100, smppSession.Address);
            db.AddDateTimePara("last_received", smppSession.LastRecieved);
            db.AddDateTimePara("start_date", smppSession.ValidFrom);
            db.AddDateTimePara("end_date", smppSession.ValidTo);
            db.AddDateTimePara("createdon", DateTime.Now);
            db.AddDateTimePara("updateon", DateTime.Now);
            if (db.RunActionQuery() > 0)
                return (Guid)smppSession.Id;

            return null;
        }

        public async Task<int> SaveSmsRecord(SmppSmsRecord smsRecord)
        {
            string query = $"insert into smpp_server_records (" +
                $"smpp_server_record_id, `from`, `to`, message, priority, data_coding, " +
                $"pe_id, tm_id, template_id, smpp_server_session_id, smpp_server_user_details_id, " +
                $"smpp_user_details_id, status, createdon, updatedon" +
                $") values (" +
                $"@smpp_server_record_id, @from, @to, @message, @priority, @data_coding, " +
                $"@pe_id, @tm_id, @template_id, @smpp_server_session_id, @smpp_server_user_details_id, " +
                $"@smpp_user_details_id, @status, @createdon, @updatedon" +
                $")";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddVarcharPara("smpp_server_record_id", 16, smsRecord.Id.ToString());
            db.AddVarcharPara("from", 21, smsRecord.From);
            db.AddVarcharPara("to", 21, smsRecord.To);
            db.AddVarcharPara("message", 1000, smsRecord.Message);
            db.AddIntegerPara("priority", smsRecord.Priority);
            db.AddIntegerPara("data_coding", smsRecord.DataCoding);
            db.AddVarcharPara("pe_id", 50, smsRecord.PeId);
            db.AddVarcharPara("tm_id", 50, smsRecord.TmId);
            db.AddVarcharPara("template_id", 50, smsRecord.TemplateId);
            db.AddVarcharPara("smpp_server_session_id", 36, smsRecord.SessionId.ToString());
            db.AddIntegerBigPara("smpp_server_user_details_id", smsRecord.SmppUserId);
            db.AddIntegerBigPara("smpp_user_details_id", smsRecord.UserId);
            db.AddVarcharPara("status", 10, smsRecord.Status);
            db.AddDateTimePara("createdon", smsRecord.CreatedOn);
            db.AddDateTimePara("updatedon", smsRecord.UpdatedOn);
            return await db.RunActionQueryAsync();
        }

        public async Task<int> SaveText(SmppText smppText)
        {
            string query = $"insert into smpp_server_texts (" +
                $"smpp_server_text_id, " +
                $"smpp_server_session_id, " +
                $"smpp_server_user_details_id, " +
                $"smpp_user_details_id, " +
                $"service_type, " +
                $"source_addr_ton, " +
                $"source_addr_npi, " +
                $"source_addr, " +
                $"dest_addr_ton, " +
                $"dest_addr_npi, " +
                $"dest_addr, " +
                $"esm_class, " +
                $"priority_flag, " +
                $"schedule_delivery_time, " +
                $"validity_period, " +
                $"registered_delivery, " +
                $"replace_if_present_flag, " +
                $"data_coding, " +
                $"sm_default_msg_id, " +
                $"short_message, " +
                $"message_identification, " +
                $"total_parts, " +
                $"part_number, " +
                $"pe_id, " +
                $"tm_id, " +
                $"template_id, " +
                $"message_id, " +
                $"send_sms_id, " +
                $"message_state, " +
                $"message_state_updated_on, " +
                $"status, " +
                $"retry_count, " +
                $"sms_cost, " +
                $"dlt_charge, " +
                $"createdon, " +
                $"updatedon" +
                $") values (" +
                $"@smpp_server_text_id, " +
                $"@smpp_server_session_id, " +
                $"@smpp_server_user_details_id, " +
                $"@smpp_user_details_id, " +
                $"@service_type, " +
                $"@source_addr_ton, " +
                $"@source_addr_npi, " +
                $"@source_addr, " +
                $"@dest_addr_ton, " +
                $"@dest_addr_npi, " +
                $"@dest_addr, " +
                $"@esm_class, " +
                $"@priority_flag, " +
                $"@schedule_delivery_time, " +
                $"@validity_period, " +
                $"@registered_delivery, " +
                $"@replace_if_present_flag, " +
                $"@data_coding, " +
                $"@sm_default_msg_id, " +
                $"@short_message, " +
                $"@message_identification, " +
                $"@total_parts, " +
                $"@part_number, " +
                $"@pe_id, " +
                $"@tm_id, " +
                $"@template_id, " +
                $"@message_id, " +
                $"@send_sms_id, " +
                $"@message_state, " +
                $"@message_state_updated_on, " +
                $"@status, " +
                $"@retry_count, " +
                $"@sms_cost, " +
                $"@dlt_charge, " +
                $"@createdon, " +
                $"@updatedon" +
                $") " +
                $"ON DUPLICATE KEY " +
                $"UPDATE " +
                $"send_sms_id = @send_sms_id, " +
                $"updatedon = @updatedon";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddVarcharPara("smpp_server_text_id", 36, smppText.Id.ToString());
            db.AddVarcharPara("smpp_server_session_id", 36, smppText.SessionId.ToString());
            db.AddIntegerBigPara("smpp_server_user_details_id", 0);
            db.AddIntegerBigPara("smpp_user_details_id",  smppText.SmppUserId);
            db.AddVarcharPara("service_type", 6, smppText.ServiceType);
            db.AddIntegerPara("source_addr_ton", smppText.SourceAddressTon);
            db.AddIntegerPara("source_addr_npi", smppText.SourceAddressNpi);
            db.AddVarcharPara("source_addr", 21, smppText.SourceAddress);
            db.AddIntegerPara("dest_addr_ton", smppText.DestAddressTon);
            db.AddIntegerPara("dest_addr_npi", smppText.SourceAddressNpi);
            db.AddVarcharPara("dest_addr", 21, smppText.DestAddress);
            db.AddIntegerPara("esm_class", smppText.EsmClass);
            db.AddIntegerPara("priority_flag", smppText.PriorityFlag);
            db.AddDateTimePara("schedule_delivery_time", smppText.ScheduledDeliveryTime);
            db.AddDateTimePara("validity_period", smppText.ValidityPeriod);
            db.AddIntegerPara("registered_delivery", smppText.RegisteredDelivery);
            db.AddIntegerPara("replace_if_present_flag", smppText.ReplaceIfPresentFlag);
            db.AddIntegerPara("data_coding", smppText.DataCoding);
            db.AddIntegerPara("sm_default_msg_id", smppText.SmDefaultMsgId);
            db.AddVarcharPara("short_message", 255, smppText.ShortMessage);
            db.AddIntegerPara("message_identification", smppText.MessageIdentification);
            db.AddIntegerPara("total_parts", smppText.TotalParts);
            db.AddIntegerPara("part_number", smppText.PartNumber);
            db.AddVarcharPara("pe_id", 50, smppText.PEID);
            db.AddVarcharPara("tm_id", 50, smppText.TMID);
            db.AddVarcharPara("template_id", 50, smppText.TemplateId);
            db.AddVarcharPara("message_id", 65, smppText.MessageId);
            db.AddIntegerBigPara("send_sms_id", smppText.SendSmsId);
            db.AddIntegerPara("message_state", (int) smppText.MessageState);
            db.AddDateTimePara("message_state_updated_on", smppText.MessageStateUpdatedOn);
            db.AddVarcharPara("status", 5, smppText.Status);
            db.AddIntegerPara("retry_count", 0);
            db.AddDecimalPara("sms_cost", 10, 2, smppText.SmsCost);
            db.AddDecimalPara("dlt_charge", 10,2, smppText.DltCharge);
            db.AddDateTimePara("createdon",  smppText.CreatedOn);
            db.AddDateTimePara("updatedon", smppText.UpdatedOn);

            return await db.RunActionQueryAsync();
        }

        public async Task<SmppSession> SearchSession(Guid? id)
        {
            string query = $"select " +
                $"smpp_server_session_id, smpp_server_user_details_id, smpp_user_details_id, source_ip, " +
                $"last_received, start_date, end_date, createdon, updateon " +
                $"FROM smpp_server_sessions " +
                $"WHERE smpp_server_session_id = @smpp_server_session_id";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddVarcharPara("smpp_server_session_id", 36, id.ToString());
            DataTable dataTable = await db.GetTableAsync();
            return ParseSmppSession(dataTable).FirstOrDefault();
        }

        private List<SmppSession> ParseSmppSession(DataTable dataTable)
        {
            List<SmppSession> sessions = new List<SmppSession>();
            if (!ReferenceEquals(dataTable, null))
            {
                for (int i = 0; i < dataTable.Rows.Count; i++)
                {
                    DataRow dr = dataTable.Rows[i];
                    SmppSession s = new SmppSession();
                    s.Id = dr.Field<Guid>("smpp_server_session_id");
                    s.SmppUserId = dr.Field<long>("smpp_server_user_details_id");
                    s.UserId = dr.Field<long>("smpp_user_details_id");
                    s.Address = dr.Field<string>("source_ip");
                    s.LastRecieved = dr.Field<DateTime>("last_received");
                    s.ValidFrom = dr.Field<DateTime>("start_date");
                    s.ValidTo = dr.Field<DateTime>("end_date");
                    //s. = dr.Field<DateTime>("createdon");
                    //s.DestAddressTon = dr.Field<DateTime>("updateon");
                    sessions.Add(s);
                }
            }
            return sessions;
        }

        public async Task<List<SmppUser>> SearchUser(SmppUserSearch smppUserSearch)
        {
            string query = $"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED ; " +
                $"select a.smpp_server_user_details_id, a.smpp_user_details_id, a.system_id, a.password, " +
                $"a.system_type, a.tps, a.concurrent_sessions, a.routes, a.whitelisted_ip, a.status, b.sms_cost, b.dlt_charge " +
                $"from smpp_server_user_details a " +
                $"join smpp_user_details b on b.smpp_user_details_id = a.smpp_user_details_id " +
                $"where 1=1 " +
                $"and b.status = 1 ";
            if (!String.IsNullOrEmpty(smppUserSearch.Status))
                query += $"and a.status = @status ";
                
            query += $"and system_id = @system_id " +
                $"and a.password = @password ";

            if (!String.IsNullOrEmpty(smppUserSearch.SystemType))
                query += $"and a.system_type = @system_type";

            MySqlDbManager db = new MySqlDbManager(query, true);
            db.AddVarcharPara("system_id", 16, smppUserSearch.SystemId);
            db.AddVarcharPara("password", 9, smppUserSearch.Password);
            db.AddVarcharPara("system_type", 13, smppUserSearch.SystemType);
            db.AddVarcharPara("status", 5, smppUserSearch.Status);
            DataTable dataTable = await db.GetTableAsync();

            return ParseSmppUser(dataTable);
        }

        private List<SmppUser> ParseSmppUser(DataTable dataTable)
        {
            List<SmppUser> users = new List<SmppUser>();
            if (!ReferenceEquals(dataTable, null))
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    SmppUser u = new SmppUser();
                    u.Id = row.Field<long>("smpp_server_user_details_id");
                    u.UserId = row.Field<long>("smpp_user_details_id");
                    u.SystemId = row.Field<string>("system_id");
                    u.Password = row.Field<string>("password");
                    u.SystemType = row.Field<string>("system_type");
                    u.TPS = row.Field<int>("tps");
                    u.ConcurrentSessions = row.Field<int>("concurrent_sessions");
                    u.Routes = row.Field<string>("routes");
                    u.WhitelistedIps = row.Field<string>("whitelisted_ip");
                    u.SmsCost = String.IsNullOrEmpty(row.Field<string>("sms_cost")) ? 0 : decimal.Parse(row.Field<string>("sms_cost"));
                    u.DltCharge = String.IsNullOrEmpty(row.Field<string>("dlt_charge")) ? 0 : decimal.Parse( row.Field<string>("dlt_charge"));
                    u.Status = row.Field<string>("status");
                    users.Add(u);
                }
            }
            return users;
        }

        public async Task UpdateUserBalance()
        {
            MySqlDbManager db = new MySqlDbManager("proc_smpp_server_user_balance", false);
            db.AddDateTimePara("cur_date", DateTime.Now);
            await db.RunActionQueryAsync();
        }

        public async Task<Dictionary<long,decimal>> GetUserBalance(long[] users)
        {
            if (users.Length == 0)
                return new Dictionary<long, decimal>();

            string query = $"SET SESSION TRANSACTION ISOLATION LEVEL READ UNCOMMITTED ;" +
                $"select smpp_user_details_id, usr_balance " +
                $"from smpp_user_balance " +
                $"where to_date = '9999-12-31 23:59:59' " +
                $"and smpp_user_details_id in ({String.Join(",", users.Select(x => x.ToString()))});";

            MySqlDbManager db = new MySqlDbManager(query, true);
            DataTable dataTatable = await db.GetTableAsync();

            Dictionary<long, decimal> balance = new Dictionary<long, decimal>();
            foreach (DataRow row in dataTatable.Rows)
            {
                balance.Add(row.Field<long>("smpp_user_details_id"), (decimal) row.Field<float>("usr_balance"));
            }
            return balance;
        }
    }
}
