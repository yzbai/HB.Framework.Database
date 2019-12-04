namespace HB.Framework.Database
{
    public class DatabaseConnectionSettings
    {
        public string DatabaseName { get; set; }
        public string ConnectionString { get; set; }
        public bool IsMaster { get; set; } = true;
    }
}
