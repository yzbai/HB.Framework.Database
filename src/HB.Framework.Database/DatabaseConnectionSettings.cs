namespace HB.Framework.Database
{
    public class DatabaseConnectionSettings
    {
        public bool IsMaster { get; set; } = true;
        public string DatabaseName { get; set; }
        public string ConnectionString { get; set; }
    }
}
