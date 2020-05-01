#nullable enable

namespace HB.Framework.Database
{
    public class DatabaseConnectionSettings
    {
        public string DatabaseName { get; private set; }
        public string ConnectionString { get; private set; }
        public bool IsMaster { get; private set; } = true;

        public DatabaseConnectionSettings(string databaseName, string connectionString, bool isMaster)
        {
            DatabaseName = databaseName;
            ConnectionString = connectionString;
            IsMaster = isMaster;
        }
    }
}
