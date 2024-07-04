using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.Marshalling.IIUnknownCacheStrategy;

namespace GDJLibrary
{


    public class DBInfoMsSql : IDisposable
    {
        readonly string DBInfoName = "dbinfo.json";
        public List<DBTableInfo> DBTableInfos { get; set; } = new List<DBTableInfo>();
        public void Dispose()
        {
            if (SqlConnection != null)
            {
                if (SqlConnection.State == System.Data.ConnectionState.Open)
                {
                    SqlConnection.Close();
                }
                SqlConnection.Dispose();
            }
            GC.SuppressFinalize(this);
        }
        public string ConnectionString { get; set; } = string.Empty;
        public System.Data.SqlClient.SqlConnection? SqlConnection { get; private set; } = null;
        public DBInfoMsSql(string connectString)
        {
            DBInfoName = Path.Combine(GCommon.CurPath, "dbinfo.json");
            ConnectionString = connectString;
            SqlConnection = new SqlConnection(connectString);
            SqlConnection.Open();
        }
   
        public async Task ReadDBInfo(bool reread = false)
        {
            if (reread)
            {
                File.Delete(DBInfoName);
            }
            await Task.Factory.StartNew(() =>
            {
                if (File.Exists(DBInfoName))
                {
                    DBTableInfos = GCommon.LoadObjectFromJson<List<DBTableInfo>>(DBInfoName)!;
                }
                else
                {
                    var tables = GetTableNames();
                    foreach (var table in tables)
                    {
                        var cols = GetColumns(table);
                        var keys = GetKeyColumns(table);
                        DBTableInfos.Add(new DBTableInfo()
                        {
                            Columns = cols,
                            TableName = table
                        });
                        GCommon.Debug(table);
                    }
                    GCommon.SaveObjectToJson(DBTableInfos, DBInfoName);
                }

            });

        }
        public List<ColumnInfo> GetColumns(string tableName)
        {
            // 必须对字段进行正向排序
            List<ColumnInfo> rtns = new List<ColumnInfo>();
            string sqlstr = $"SELECT syscolumns.prec, syscolumns.scale, syscolumns.name as clmname, systypes.name as typename, systypes.length as length, systypes.allownulls as allownulls " +
                            $" FROM syscolumns, systypes WHERE syscolumns.xusertype = systypes.xusertype AND" +
                            $" syscolumns.id = object_id('{tableName}')  order by syscolumns.colorder";
            using (SqlCommand commond = new SqlCommand(sqlstr, SqlConnection))
            {
                SqlDataReader sdr = commond.ExecuteReader();
                while (sdr.Read())
                {
                    short prec = (short)sdr["prec"];
                    short? scale = null;
                    if(sdr["scale"] != DBNull.Value)
                    {
                        scale = (short)(int)sdr["scale"];
                    }
                    short[]? shorts = null;
                    if(scale is null)
                    {
                        shorts = new short[1] { prec };
                    }
                    else
                    {
                        shorts = new short[2] { prec, (short)scale };
                    }
                    rtns.Add(new ColumnInfo()
                    {
                        clmname = (string)sdr["clmname"],
                        typename = (string)sdr["typename"],
                        length = shorts,
                        isnullable = (bool)sdr["allownulls"],
                        dbType = GCommon.GetSqlDbType((string)sdr["typename"])
                    });
                }
                sdr.Close();
            }
            List<string> keys = GetKeyColumns(tableName);
            rtns.ForEach((col) =>
            {
                if (keys.Contains(col.clmname))
                {
                    col.IsKey = true;
                }
            });

            return rtns;
        }
        public List<string> GetKeyColumns(string tableName)
        {
            // 必须对字段进行正向排序
            List<string> rtns = new List<string>();
            string sqlstr = $"SELECT syscolumns.name as name" +
                            $" FROM syscolumns,sysobjects,sysindexes,sysindexkeys" +
                            $" WHERE syscolumns.id = object_id('{tableName}') AND sysobjects.xtype = 'PK' AND" +
                            $" sysobjects.parent_obj = syscolumns.id AND sysindexes.id = syscolumns.id AND" +
                            $" sysobjects.name = sysindexes.name AND sysindexkeys.id = syscolumns.id AND" +
                            $" sysindexkeys.indid = sysindexes.indid AND syscolumns.colid = sysindexkeys.colid  order by name";
            using (SqlCommand commond = new SqlCommand(sqlstr, SqlConnection))
            {
                SqlDataReader sdr = commond.ExecuteReader();
                while (sdr.Read())
                {
                    rtns.Add((string)sdr["name"]);
                }
                sdr.Close();
            }
            return rtns;
        }

        /// <summary>
        /// 循环获取数据库中的表信息
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="keyValues"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public bool LoopTableRowsForKey(string tableName, Dictionary<string, object> keyValues, Func<Dictionary<string, object>, bool> func)
        {
            var tableInfo = DBTableInfos.Find(item => item.TableName == tableName);
            bool rtn = false;
            if (tableInfo != null)
            {
                StringBuilder strSql = new StringBuilder();
                strSql.AppendLine($"SELECT *  FROM {tableInfo.TableName} WHERE 1=1");
                List<ColumnInfo> keys = tableInfo.Columns.FindAll(item => item.IsKey).ToList();
                if (keys.Count == 0)
                {
                    keys = tableInfo.Columns;
                }
                foreach (var item in keys)
                {
                    if (item.dbType == SqlDbType.Text)
                    {
                        strSql.AppendLine($" AND (CAST([{item.clmname}]  AS NVARCHAR(MAX))= @P_{item.clmname} OR ([{item.clmname}] IS NULL AND 1 = @N_{item.clmname}))");
                    }
                    else
                    {
                        strSql.AppendLine($" AND  ([{item.clmname}]  = @P_{item.clmname} OR ([{item.clmname}] IS NULL AND 1 = @N_{item.clmname}))");
                    }
                }
                using (SqlCommand command = new SqlCommand(strSql.ToString(), SqlConnection))
                {
                    foreach (var item in keys)
                    {
                        System.Diagnostics.Debug.WriteLine($"@P_{item.clmname}: [{keyValues[item.clmname]}]");
                        if (keyValues[item.clmname] is null)
                        {
                            command.Parameters.Add(new SqlParameter($"@P_{item.clmname}", keyValues[item.clmname]));
                            command.Parameters.Add(new SqlParameter($"@N_{item.clmname}", 1));
                        }
                        else
                        {
                            command.Parameters.Add(new SqlParameter($"@P_{item.clmname}", keyValues[item.clmname]));
                            command.Parameters.Add(new SqlParameter($"@N_{item.clmname}", 2));
                        }
                    }
                    SqlDataReader sdr = command.ExecuteReader();
                    while (sdr.Read())
                    {
                        Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
                        foreach (var item in tableInfo.Columns)
                        {
                            object? value = sdr.GetValue(item.clmname);
                            keyValuePairs.Add(item.clmname, value);
                        }
                        if (!func(keyValuePairs))
                        {
                            break;
                        }
                    }
                    sdr.Close();
                }
                rtn = true;
            }
            return rtn;
        }

        public List<string> GetTableNames()
        {
            List<string> tableNames = new List<string>();
            string sqlstr = "Select Name FROM SysObjects Where XType='U' ORDER BY Name";
            using (SqlCommand commond = new SqlCommand(sqlstr, SqlConnection))
            {
                SqlDataReader sdr = commond.ExecuteReader();
                while (sdr.Read())
                {
                    tableNames.Add((string)sdr["Name"]);
                }
                sdr.Close();
            }
            return tableNames;
        } 
    }
}
