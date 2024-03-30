using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
//using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.Configuration;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using System.Xml.Linq;

namespace SMSGateway.DataManager.General
{
    public class MySqlDbManager : IDisposable
    {
        #region Enum: Parameter Directions
        /// <summary>
        /// Enum for Parameter Directions
        /// </summary>
        public enum QueryParameterDirection : int
        {
            /// <summary>
            /// The parameter is an input parameter.
            /// </summary>
            Input = 1,
            /// <summary>
            /// The parameter is capable of both input and output.
            /// </summary>
            Output = 2,
            /// <summary>
            /// The parameter represents a return value from an 
            /// operation such as a stored procedure, built-in
            /// function, or user-defined function.
            /// </summary>
            Return = 3,
            InputOutput = 4
        }
        #endregion

        #region FIELDS
        private string strCommandText = string.Empty;
        private bool blnSP = true;
        private ArrayList oParameters = new ArrayList();
        private bool blnLocalConn = true;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a Stored Procedure query with
        /// the given Stored Procedure name.
        /// </summary>
        /// <param name="StoredProcName">Name of the Stored Procedure.</param>
        public MySqlDbManager(string StoredProcName)
            : this(StoredProcName, false)
        {
        }
        /// <summary>
        /// Initializes a query with the given Stored Procedure
        /// name or the query text and the query type (Text or Stored
        /// Procedure).
        /// </summary>
        /// <param name="SqlString">Query Text/ Stored Procedure name.</param>
        /// <param name="IsTextQuery">True->Text Query, False->Stored Procedure</param>
        public MySqlDbManager(string SqlString, bool IsTextQuery)
        {
            blnSP = !IsTextQuery;
            strCommandText = SqlString;
        }
        #endregion

        #region Run Query
        #region DataTable
        /// <summary>
        /// Executes the current query and returns the result
        /// in a DataTable object.
        /// </summary>
        /// <returns>The query result set</returns>
        public DataTable GetTable()
        {
            DataTable dt = null;

            MySqlCommand oCmd = new MySqlCommand();
            InitQuery(oCmd);
            //if (oCmd.CommandType == CommandType.StoredProcedure)
            //{
            //    oCmd.Parameters.Add("ResultSet", MySqlDbType.Structured).Direction=ParameterDirection.Output;                
            //}

            MySqlDataAdapter da = new MySqlDataAdapter(oCmd);
            DataSet ds = new DataSet();

            try
            {
                da.Fill(ds);

                if (null != ds && ds.Tables.Count > 0)
                {
                    dt = ds.Tables[0];
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (blnLocalConn)
                {
                    oConn.Close();
                }
                oCmd.Dispose();
            }

            return dt;
        }

        #endregion

        #region DataTableAsync
        /// <summary>
        /// Executes the current query and returns the result
        /// in a DataTable object.
        /// </summary>
        /// <returns>The query result set</returns>
        public async Task<DataTable> GetTableAsync()
        {
            return await Task.Run<DataTable>(() =>
            {
                DataTable dt = null;

                MySqlCommand oCmd = new MySqlCommand();
                InitQuery(oCmd);
                //if (oCmd.CommandType == CommandType.StoredProcedure)
                //{
                //    oCmd.Parameters.Add("ResultSet", MySqlDbType.Structured).Direction=ParameterDirection.Output;                
                //}

                MySqlDataAdapter da = new MySqlDataAdapter(oCmd);
                DataSet ds = new DataSet();

                try
                {
                    da.Fill(ds);

                    if (null != ds && ds.Tables.Count > 0)
                    {
                        dt = ds.Tables[0];
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    if (blnLocalConn)
                    {
                        oConn.Close();
                    }
                    oCmd.Dispose();
                }

                return dt;
            });

        }

        #endregion

        #region DataSet
        /// <summary>
        /// Executes the current query and returns the result Set
        /// in a DataSet object.
        /// </summary>
        /// <returns>The query result set</returns>
        public DataSet GetDataSet()
        {
            DataSet ds = new DataSet();
            MySqlCommand oCmd = new MySqlCommand();
            InitQuery(oCmd);
            //if (oCmd.CommandType == CommandType.StoredProcedure)
            //{
            //    for (int i = 0; i < recordsetNumber; i++)
            //    {
            //        oCmd.Parameters.Add("ResultSet" + i.ToString(), MySqlDbType.Structured).Direction = ParameterDirection.Output; 
            //    } 
            //}

            MySqlDataAdapter da = new MySqlDataAdapter(oCmd);

            try
            {
                da.Fill(ds);

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (blnLocalConn)
                {
                    oConn.Close();
                }
                oCmd.Dispose();
            }

            return ds;
        }
        #endregion

        #region Command
        /// <summary>
        /// Executes the current query and returns the result Set
        /// in a DataSet object.
        /// </summary>
        /// <returns>The query result set</returns>
        public MySqlCommand GetCommand()
        {
            DataSet ds = new DataSet();
            MySqlCommand oCmd = new MySqlCommand();
            InitQuery(oCmd);


            return oCmd;
        }
        #endregion

        #region NonQuery
        /// <summary>
        /// Executes a DML type query (with no result set).
        /// </summary>
        /// <returns>Number of affected rows.</returns>
        public int RunActionQuery()
        {
            int intRowsAffected = -1;

            MySqlCommand oCmd = new MySqlCommand();
            InitQuery(oCmd);

            try
            {
                intRowsAffected = oCmd.ExecuteNonQuery();
            }
            finally
            {
                if (blnLocalConn)
                {
                    oConn.Close();
                }
                oCmd.Dispose();
            }

            return intRowsAffected;
        }
        #endregion

        #region NonQueryAsync
        /// <summary>
        /// Executes a DML type query (with no result set).
        /// </summary>
        /// <returns>Number of affected rows.</returns>
        public async Task<int> RunActionQueryAsync()
        {
            int intRowsAffected = -1;

            MySqlCommand oCmd = new MySqlCommand();
            InitQuery(oCmd);

            try
            {
                intRowsAffected = await oCmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (blnLocalConn)
                {
                    oConn.Close();
                }
                oCmd.Dispose();
            }

            return intRowsAffected;
        }
        #endregion

        #region Scalar
        /// <summary>
        /// Executes the query, and returns the first column of the
        /// first row in the result set returned by the query. 
        /// Extra columns or rows are ignored.
        /// </summary>
        /// <returns>The first column of the first row in the result
        /// set, or a null reference if the result set is empty.</returns>
        public object GetScalar()
        {
            object oRetVal = null;

            MySqlCommand oCmd = new MySqlCommand();
            InitQuery(oCmd);

            try
            {
                oRetVal = oCmd.ExecuteScalar();
            }

            finally
            {
                if (blnLocalConn)
                {
                    oConn.Close();
                }
                oCmd.Dispose();
            }

            return oRetVal;
        }
        #endregion

        #region Reader
        public MySqlDataReader ExecuteReader()
        {
            MySqlDataReader reader = null;
            try
            {
                MySqlCommand oCmd = new MySqlCommand();
                InitQuery(oCmd);
                reader = oCmd.ExecuteReader();
            }
            catch { }
            return reader;
        }
        #endregion

        #region Initializes a Query
        /// <summary>
        /// Performs the initial tasks before executing a query.
        /// </summary>
        /// <param name="oCmd">Command object holding the query.</param>
        private void InitQuery(MySqlCommand oCmd)
        {
            // set Connection
            blnLocalConn = oConn == null;
            if (blnLocalConn)
            {
                oConn = new MySqlDataConnection();
                blnLocalConn = true;
                oConn.Open();
            }
            oCmd.Connection = oConn.oConn;

            // set Command
            oCmd.CommandTimeout = 0;
            oCmd.CommandText = strCommandText;
            oCmd.CommandType = blnSP ? CommandType.StoredProcedure : CommandType.Text;

            // set Parameters
            foreach (object oItem in oParameters)
            {
                //System.Diagnostics.Debug.Print(oItem.ToString() +" : "+ ((MySqlParameter)oItem).Value);
                oCmd.Parameters.Add((MySqlParameter)oItem);
            }
        }
        #endregion
        #endregion

        #region Parameter handling
        #region Type: Integer
        /// <summary>
        /// Adds an Integer type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddIntegerPara(string Name, int Value)
        {
            AddIntegerPara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds an Integer type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddIntegerPara(string Name, int? Value)
        {
            AddIntegerPara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds an Integer type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddIntegerPara(string Name, int Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Int32);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = Value;
            oParameters.Add(oPara);
        }
        /// <summary>
        /// Adds an Integer type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddIntegerPara(string Name, int? Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Int32);
            oPara.Direction = GetParaType(Direction);
            if (Value == null)
                oPara.Value = DBNull.Value;
            else
                oPara.Value = Value;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: Bigint
        public void AddIntegerBigPara(string Name, ulong Value)
        {
            AddIntegerBigPara(Name, (long) Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds an Integer type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddIntegerBigPara(string Name, long Value)
        {
            AddIntegerBigPara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds an Integer type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddIntegerBigPara(string Name, ulong? Value)
        {
            AddIntegerBigPara(Name, (long?) Value, QueryParameterDirection.Input);
        }
        public void AddIntegerBigPara(string Name, long? Value)
        {
            AddIntegerBigPara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds an Integer type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddIntegerBigPara(string Name, long Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Int64);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = Value;
            oParameters.Add(oPara);
        }

        /// <summary>
        /// Adds an Integer type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddIntegerBigPara(string Name, long? Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Int64);
            oPara.Direction = GetParaType(Direction);
            if (Value == null)
                oPara.Value = DBNull.Value;
            else
                oPara.Value = Value;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: Char
        /// <summary>
        /// Adds a Char type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddCharPara(string Name, int Size, char Value)
        {
            AddCharPara(Name, Size, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds a Char type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddCharPara(string Name, int Size, char Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value.Equals(null))
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.VarChar, Size);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = oValue;
            oParameters.Add(oPara);
        }
        /// <summary>
        /// Adds a Char type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddCharPara(string Name, int Size, char? Value)
        {
            AddCharPara(Name, Size, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds a Char type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddCharPara(string Name, int Size, char? Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value.Equals(null))
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.VarChar, Size);
            oPara.Direction = GetParaType(Direction);
            if (ReferenceEquals(Value, null))
                oPara.Value = DBNull.Value;
            else
                oPara.Value = oValue;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: Varchar
        /// <summary>
        /// Adds a Varchar type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddVarcharPara(string Name, int Size, string Value)
        {
            AddVarcharPara(Name, Size, Value, QueryParameterDirection.Input);
        }

        /// <summary>
        /// Adds a Varchar type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddVarcharPara(string Name, int Size, string Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value == null)
            {
                oValue = DBNull.Value;
            }
            else if (Value.Length == 0)
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.VarChar, Size);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = oValue;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: NVarchar
        /// <summary>
        /// Adds a Varchar type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddNVarcharPara(string Name, int Size, string Value)
        {
            AddNVarcharPara(Name, Size, Value, QueryParameterDirection.Input);
        }

        /// <summary>
        /// Adds a Varchar type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddNVarcharPara(string Name, int Size, string Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value == null)
            {
                oValue = DBNull.Value;
            }
            else if (Value.Length == 0)
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.VarString, Size);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = oValue;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: LongText
        /// <summary>
        /// Adds a Varchar type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddLongTextPara(string Name, int Size, string Value)
        {
            AddLongTextPara(Name, Size, Value, QueryParameterDirection.Input);
        }

        /// <summary>
        /// Adds a Varchar type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddLongTextPara(string Name, int Size, string Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value == null)
            {
                oValue = DBNull.Value;
            }
            else if (Value.Length == 0)
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.LongText, Size);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = oValue;
            oParameters.Add(oPara);
        }
        #endregion
        #region Type: Boolean
        /// <summary>
        /// Adds a Blob type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddBoolPara(string Name, bool Value)
        {
            AddBoolPara(Name, Value, QueryParameterDirection.Input);
        }

        /// <summary>
        /// Adds a Blob type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddBoolPara(string Name, bool Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value == null)
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Bit);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = oValue;
            oParameters.Add(oPara);
        }

        /// <summary>
        /// Adds a Blob type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddBoolPara(string Name, bool? Value)
        {
            AddBoolPara(Name, Value, QueryParameterDirection.Input);
        }

        /// <summary>
        /// Adds a Blob type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddBoolPara(string Name, bool? Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value == null)
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Bit);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = oValue;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: Image
        /// <summary>
        /// Adds a Clob type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddImagePara(string Name, string Value)
        {
            AddImagePara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds a Clob type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddImagePara(string Name, string Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value == null)
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Blob);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = oValue;
            oParameters.Add(oPara);
        }


        #endregion

        #region Type: GUID
        /// <summary>
        /// Adds a Varchar type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddGuidPara(string Name, Guid Value)
        {
            AddGuidPara(Name, Value, QueryParameterDirection.Input);
        }
        public void AddGuidPara(string Name, Guid? Value)
        {
            AddGuidPara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds a Varchar type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Size">Size of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddGuidPara(string Name, Guid Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value == null)
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Guid, 38);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = oValue;
            oParameters.Add(oPara);
        }
        public void AddGuidPara(string Name, Guid? Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value == null)
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Guid, 38);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = oValue;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: DateTime
        /// <summary>
        /// Adds a DateTime type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddDateTimePara(string Name, DateTime Value)
        {
            AddDateTimePara(Name, Value, QueryParameterDirection.Input);
        }
        public void AddDateTimePara(string Name, DateTime? Value)
        {
            AddDateTimePara(Name, Value, QueryParameterDirection.Input);
        }

        /// <summary>
        /// Adds a DateTime type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddDateTimePara(string Name, DateTime Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.DateTime);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = Value;
            oParameters.Add(oPara);
        }

        public void AddDateTimePara(string Name, DateTime? Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.DateTime);
            oPara.Direction = GetParaType(Direction);
            if (Value == null)
                oPara.Value = DBNull.Value;
            else
                oPara.Value = Value;
            oParameters.Add(oPara);
        }

        #endregion

        #region Type: Timestamp
        /// <summary>
        /// Adds a DateTime type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddTimeStampPara(string Name, DateTime Value)
        {
            AddDateTimePara(Name, Value, QueryParameterDirection.Input);
        }
        public void AddTimeStampPara(string Name, DateTime? Value)
        {
            AddDateTimePara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds a Timestamp type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddTimeStampPara(string Name, DateTime Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.DateTime);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = Value.ToString("yyyy-MM-dd HH:mm:ss");
            oParameters.Add(oPara);
        }
        public void AddTimeStampPara(string Name, DateTime? Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Timestamp);
            oPara.Direction = GetParaType(Direction);
            if (Value == null)
                oPara.Value = DBNull.Value;
            else
                oPara.Value = ((DateTime)Value).ToString("yyyy-MM-dd HH:mm:ss");
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: Time
        /// <summary>
        /// Adds a DateTime type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddTimePara(string Name, TimeSpan Value)
        {
            AddTimePara(Name, Value, QueryParameterDirection.Input);
        }
        public void AddTimePara(string Name, TimeSpan? Value)
        {
            AddTimePara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds a DateTime type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddTimePara(string Name, TimeSpan Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Time);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = Value;
            oParameters.Add(oPara);
        }

        public void AddTimePara(string Name, TimeSpan? Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Time);
            oPara.Direction = GetParaType(Direction);
            if (Value == null)
                oPara.Value = DBNull.Value;
            else
                oPara.Value = Value;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: Decimal
        /// <summary>
        /// Adds a Decimal type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Scale">Scale of the decimal number</param>
        /// <param name="Precision">Precision of the decimal number</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddDecimalPara(string Name, byte Scale, byte Precision, decimal Value)
        {
            AddDecimalPara(Name, Scale, Precision, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds a Decimal type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Scale">Scale of the decimal number</param>
        /// <param name="Precision">Precision of the decimal number</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddDecimalPara(string Name, byte Scale, byte Precision, decimal? Value)
        {
            AddDecimalPara(Name, Scale, Precision, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds a Decimal type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Scale">Scale of the decimal number</param>
        /// <param name="Precision">Precision of the decimal number</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddDecimalPara(string Name, byte Scale, byte Precision, decimal Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Decimal);
            oPara.Scale = Scale;
            oPara.Precision = Precision;
            oPara.Direction = GetParaType(Direction);
            oPara.Value = Value;
            oParameters.Add(oPara);
        }
        /// <summary>
        /// Adds a Decimal type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Scale">Scale of the decimal number</param>
        /// <param name="Precision">Precision of the decimal number</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddDecimalPara(string Name, byte Scale, byte Precision, decimal? Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Decimal);
            oPara.Scale = Scale;
            oPara.Precision = Precision;
            oPara.Direction = GetParaType(Direction);
            if (Value == null)
                oPara.Value = DBNull.Value;
            else
                oPara.Value = Value;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: Float
        /// <summary>
        /// Adds a Float type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>

        /// <param name="Value">Value of the parameter.</param>
        public void AddFloatPara(string Name, float Value)
        {
            AddFloatPara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds the float para.
        /// </summary>
        /// <param name="Name">The name.</param>
        /// <param name="Value">The value.</param>
        /// <param name="Direction">The direction.</param>
        /// <author>Debajit Mukhopadhyay</author>
        /// <createdDate>19-Oct-09</createdDate>
        public void AddFloatPara(string Name, float Value, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Decimal);

            oPara.Direction = GetParaType(Direction);
            oPara.Value = Value;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: TimeStamp
        /// <summary>
        /// Adds a Timestamp type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddTimeStampPara(string Name, byte[] Value)
        {
            AddTimeStampPara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds a Timestamp type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddTimeStampPara(string Name, byte[] Value, QueryParameterDirection Direction)
        {
            object oValue = Value;
            if (Value == null)
            {
                oValue = DBNull.Value;
            }
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Timestamp);
            oPara.Direction = GetParaType(Direction);
            oPara.Value = oValue;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: VarBinary
        /// <summary>
        /// Adds a Binary type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        public void AddVarBinaryPara(string Name, byte[] Value)
        {
            AddVarBinaryPara(Name, Value, QueryParameterDirection.Input);
        }
        /// <summary>
        /// Adds a Binary type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        public void AddVarBinaryPara(string Name, byte[] oValue, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.VarBinary);
            oPara.Direction = GetParaType(Direction);
            if (oValue == null)
                oPara.Value = DBNull.Value;
            else
                oPara.Value = oValue;
            oParameters.Add(oPara);
        }
        #endregion

        #region Type: Structured
        /// <summary>
        /// Adds a Structured type input query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        //public void AddStructuredPara(string Name, DataTable Value)
        //{
        //    AddStructuredPara(Name, Value, QueryParameterDirection.Input);
        //}
        /// <summary>
        /// Adds a Structured type query parameter with
        /// the given direction type. 
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        /// <param name="Value">Value of the parameter.</param>
        /// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        //public void AddStructuredPara(string Name, DataTable oValue, QueryParameterDirection Direction)
        //{
        //    MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Structured);
        //    oPara.Direction = GetParaType(Direction);
        //    oPara.Value = oValue;
        //    this.oParameters.Add(oPara);
        //}
        #endregion

        #region Type: Geography
        ///// <summary>
        ///// Adds an Geography type input query parameter.
        ///// </summary>
        ///// <param name="Name">Name of the parameter.</param>
        ///// <param name="Value">Value of the parameter.</param>
        //public void AddGeographyPara(string Name, GeographyPoint Value)
        //{
        //    AddGeographyPara(Name, Value, QueryParameterDirection.Input);
        //}
        ///// <summary>
        ///// Adds an Geography type query parameter with
        ///// the given direction type. 
        ///// </summary>
        ///// <param name="Name">Name of the parameter.</param>
        ///// <param name="Value">Value of the parameter.</param>
        ///// <param name="Direction">Parameter Direction: Input/ Output/ Return</param>
        //public void AddGeographyPara(string Name, GeographyPoint Value, QueryParameterDirection Direction)
        //{
        //    MySqlParameter oPara = new MySqlParameter(Name, MySqlDbType.Udt);
        //    oPara.Direction = GetParaType(Direction);

        //    if (ReferenceEquals(Value, null))
        //        oPara.Value = DBNull.Value;
        //    else
        //    {
        //        SqlGeography d = SqlGeography.Point(Value.Latitude, Value.Longitude, 4326);
        //        oPara.UdtTypeName = "geography";
        //        oPara.Value = d;
        //    }
        //    this.oParameters.Add(oPara);
        //}
        #endregion

        #region Adds a NULL value Parameter
        /// <summary>
        /// Adds a NULL value query parameter.
        /// </summary>
        /// <param name="Name">Name of the parameter.</param>
        public void AddNullValuePara(string Name)
        {
            MySqlParameter oPara = new MySqlParameter(Name, DBNull.Value);
            oPara.Direction = ParameterDirection.Input;
            oParameters.Add(oPara);
        }

        /// <summary>
        /// Adds a NULL value query parameter with Parameter Direction
        /// </summary>
        /// <param name="Name">Name of parameter</param>
        /// <param name="Direction">Parameter Direction</param>
        public void AddNullValuePara(string Name, QueryParameterDirection Direction)
        {
            MySqlParameter oPara = new MySqlParameter(Name, DBNull.Value);
            oPara.Direction = GetParaType(Direction);
            oParameters.Add(oPara);
        }
        #endregion

        #region Adds the Return Parameter
        /// <summary>
        /// Adds the return parameter.
        /// </summary>
        public void AddReturnPara()
        {
            AddIntegerPara("ReturnIntPara", 0, QueryParameterDirection.Return);
        }
        #endregion

        #region Returns the value of the passed parameter
        /// <summary>
        /// Returns the value of a parameter.
        /// </summary>
        /// <param name="ParaName">Name of the parameter.</param>
        /// <returns>Value of the parameter.</returns>
        public object GetParaValue(string ParaName)
        {
            object oValue = null;
            MySqlParameter oPara = null;

            ParaName = ParaName.Trim().ToLower();
            foreach (object oItem in oParameters)
            {
                oPara = (MySqlParameter)oItem;
                if (oPara.ParameterName.ToLower() == ParaName)
                {
                    oValue = oPara.Value;
                    break;
                }
            }

            return oValue;
        }
        #endregion

        #region Returns the value of the Return Parameter
        /// <summary>
        /// Returns the value of the Return Parameter.
        /// </summary>
        /// <returns>The value of the Return Parameter.</returns>
        public object GetReturnParaValue()
        {
            return GetParaValue("ReturnIntPara");
        }
        #endregion

        #region Clears the parameters
        /// <summary>
        /// Clears the parameters collection.
        /// </summary>
        public void ClearParameters()
        {
            oParameters.Clear();
        }
        #endregion

        #region Converts enum to parameter direction
        /// <summary>
        /// Converts parameter direction enum to the underlying sql type
        /// </summary>
        /// <param name="Direction">Enum value to convert</param>
        /// <returns>Underlying SqlClient value corresponding to the passed Enum</returns>
        private ParameterDirection GetParaType(QueryParameterDirection Direction)
        {
            switch (Direction)
            {
                case QueryParameterDirection.Output:
                    return ParameterDirection.InputOutput;
                case QueryParameterDirection.Return:
                    return ParameterDirection.ReturnValue;
                case QueryParameterDirection.InputOutput:
                    return ParameterDirection.InputOutput;
                default:
                    return ParameterDirection.Input;
            }
        }
        #endregion
        #endregion

        #region Dispose
        /// <summary>
        /// Releases the resources.
        /// </summary>
        public void Dispose()
        {
            oConn.Dispose();
            oParameters.Clear();
        }
        #endregion

        #region Connection
        private MySqlDataConnection oConn = null;
        /// <summary>
        /// Write Only: Connection object to run the query. To be used
        /// in transactional operations involving multiple objects.
        /// Also used in performing multiple database operations using
        /// the same connection.
        /// </summary>
        public MySqlDataConnection Connection
        {
            set
            {
                oConn = value;
            }
        }
        #endregion

        #region [ Get Connection String ]
        protected string getConnectionString()
        {
            string objConnectionString = "";
            //objConnectionString = "Data Source=192.168.52.11;Initial Catalog=IFBSMS;User Id=ifb;Password=admin!@#$%;";
            //objConnectionString = "Data Source=192.168.52.210;Initial Catalog=IFBSMS;User Id=ifbsms;Password=admin!@#$%;";
            objConnectionString = ConfigurationManager.ConnectionStrings["ConnectionString"].ConnectionString;
            return objConnectionString;
        }
        #endregion

        //#region [ Execute SQL ]
        //public int ExecuteNonQuery(string sqlStr)
        //{
        //    int Status = 0;

        //    try
        //    {
        //        //createConnection();
        //        string connstr = getConnectionString();
        //        using (SqlConnection conn = new SqlConnection(connstr))
        //        {
        //            if (conn.State == ConnectionState.Open)
        //                conn.Close();
        //            conn.Open();

        //            MySqlCommand cmd = new MySqlCommand(sqlStr, conn);
        //            cmd.CommandTimeout = 0;
        //            Status = cmd.ExecuteNonQuery();
        //        }
        //        return Status;

        //    }
        //    catch (Exception ex)
        //    {
        //        return Status;
        //    }
        //}
        //#endregion

        //#region [ Get DataSet ]
        //public DataSet GetDataSet(string sql)
        //{
        //    DataSet ds;

        //    string connstr = getConnectionString();
        //    using (SqlConnection conn = new SqlConnection(connstr))
        //    {
        //        if (conn.State == ConnectionState.Open)
        //            conn.Close();
        //        conn.Open();

        //        MySqlCommand cmd = new MySqlCommand(sql, conn);
        //        MySqlDataAdapter da = new MySqlDataAdapter(cmd);
        //        ds = new DataSet();
        //        da.Fill(ds, "default");
        //    }
        //    return ds;
        //}
        //#endregion

        //#region [ Get DataTable ]
        //public DataTable GetDataTable(string sql)
        //{
        //    DataSet dataSet = GetDataSet(sql);
        //    return dataSet.Tables[0];
        //}
        //#endregion

        //#region [ Get Reader ]
        //public MySqlDataReader ExecuteReader(string sql)
        //{
        //    MySqlDataReader dr;
        //    try
        //    {
        //        //createConnection();
        //        string connstr = getConnectionString();
        //        using (SqlConnection conn = new SqlConnection(connstr))
        //        {
        //            if (conn.State == ConnectionState.Open)
        //                conn.Close();
        //            conn.Open();

        //            MySqlCommand cmd = new MySqlCommand(sql, conn);
        //            dr = cmd.ExecuteReader();
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        dr = null;
        //    }

        //    return dr;
        //}
        //#endregion

    }
}
