namespace HB.Framework.Database
{
    /// <summary>
    /// 数据库具体某个库的信息
    /// </summary>
    public class DatabaseConnectionSettings
    {
        /// <summary>
        /// 是否是主库
        /// </summary>
        public bool IsMaster { get; set; } = true;

        /// <summary>
        /// 库名称
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// 连接字符串
        /// </summary>
        public string ConnectionString { get; set; }

        public DatabaseConnectionSettings(string databaseName, string connectionString, bool isMaster = true)
        {
            DatabaseName = databaseName;
            ConnectionString = connectionString;
            IsMaster = isMaster;
        }
    }
}
