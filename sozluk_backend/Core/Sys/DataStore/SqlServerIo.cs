using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using System.Threading;
using sozluk_backend.Core.Sys.Logging;

namespace sozluk_backend.Core.Sys.DataStore
{
    class SqlServerIo
    {
        private SqlConnection conn;
        private SqlDataReader reader;
        private int affected;
        
        private readonly string ConnString =
            "Server=localhost; Database={0};" +
            "User Id=dba_io_user;Password={1};";

        
        public static SqlServerIo Create()
        {
            return new SqlServerIo();
        }
    
        public static void Release(SqlServerIo sqlIo)
        {
            sqlIo.CloseReader();
            sqlIo.Close();
            sqlIo = null;
        }

        public static long GetRecordCountQuick(string tableName)
        {
            long recordCount = -1;

            SqlServerIo sql;

            string query = "SELECT PA.ROWS As RecordCount FROM sys.objects As SO " +
                "INNER JOIN sys.partitions AS PA ON PA.object_id = SO.object_id " +
                "WHERE SO.type = 'U' AND PA.index_id < 2 AND SO.name = '{0}'";

            sql = Create();

            if (!sql.Ready)
                return -1;


            if (!sql.Execute(false, query, tableName))
            {
                Release(sql);
                return -1;
            }

            if (!sql.Read())
            {
                Release(sql);
                return -1;
            }

            recordCount = sql.GetValueOfColumn<long>("RecordCount");

            Release(sql);

            return recordCount;
        }

        private SqlServerIo()
        {
            string dbName;

            if (Config.Get().TestMode)
                dbName = "Sourtimes_test";
            else
                dbName = "Sourtimes";
            
            conn = new SqlConnection(string.Format(ConnString,dbName,Config.Get().DbPassword));

            try
            {
                conn.Open();
                
                while (conn.State == ConnectionState.Connecting)
                {
                    Thread.Sleep(1);
                }

            }
            catch (Exception e)
            {
                Log.Critical(e.Message);
                conn = null;
            }
        }

        public bool Ready
        {
            get
            {
                if (conn == null)
                    return false;

                if (conn.State == ConnectionState.Broken ||
                    conn.State == ConnectionState.Closed)
                {
                    return false;
                }

                return true;
            }
        }

        public bool Execute(bool nonQuery, string queryStringFormat, params object[] args)
        {
            bool result = false;
            SqlTransaction sqlTran = null;
            SqlCommand cmd = null;
            string query;

            ExecPerf perf = new ExecPerf();

            affected = 0;

            if (!Ready)
                return false;

            try
            {
                if (nonQuery)
                    sqlTran = conn.BeginTransaction();

                query = string.Format(queryStringFormat, args);

                cmd = new SqlCommand(query, this.conn, sqlTran);

                if (nonQuery)
                {
                    perf.Begin();
                    affected = cmd.ExecuteNonQuery();
                    perf.Time("SQL execution", TimeSpan.FromSeconds(3));
                }
                else
                {
                    CloseReader();

                    perf.Begin();
                    reader = cmd.ExecuteReader();
                    perf.Time("sql execution",TimeSpan.FromSeconds(8));
                    affected = reader.RecordsAffected;
                }

                if (sqlTran != null)
                    sqlTran.Commit();

                result = true;
            }
            catch (Exception e)
            {
                Log.Error("Sql exec error: {0}", e.Message);

                if (sqlTran != null)
                    sqlTran.Rollback();
            }

            return result;
        }

        public int RecordCount
        {
            get
            {
                return affected;
            }
        }

        public bool Read()
        {
            if (this.reader == null)
                throw new Exception("Reader not available");

            if (!this.reader.HasRows)
                return false;

            if (!this.reader.Read())
                return false;

            return true;
        }

        private void CloseReader()
        {
            if (this.reader != null)
            {
                this.reader.Close();
                this.reader = null;
            }
        }

        public T GetValueOfColumn<T>(string name)
        {
            int colIndex;
            object v;

            try
            {
                colIndex = reader.GetOrdinal(name);
            }
            catch (IndexOutOfRangeException)
            {
                return default(T);
            }

            v = this.reader[colIndex];

            if (this.reader.IsDBNull(colIndex))
                return default(T);

            return (T)Convert.ChangeType(v, typeof(T));
        }

        private void Close()
        {
            if (conn != null)
            {
                if (Ready)
                    conn.Close();

                conn.Dispose();
                conn = null;
            }
        }
        
    }
}
