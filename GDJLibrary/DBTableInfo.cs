using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDJLibrary
{
    public class ColumnInfo
    { 
        public string clmname { get; set; } = string.Empty;
        public string typename { get; set; } = string.Empty;

        public short[]? length { get; set; } = null;
        public bool isnullable { get; set; }

        public SqlDbType dbType { get; set; }

        public bool IsKey { get; set; } = false;
    }
    public class DBTableInfo
    {
        /// <summary>
        /// 列信息
        /// </summary>
        public List<ColumnInfo> Columns { get; set; } = new List<ColumnInfo>();
        /// <summary>
        /// 表的名字
        /// </summary>
        public string TableName { get; set; } = string.Empty;
    }
}
