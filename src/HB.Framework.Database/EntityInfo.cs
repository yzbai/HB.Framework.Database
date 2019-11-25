namespace HB.Framework.Database
{
    /// <summary>
    /// 实体信息
    /// </summary>
    public class EntityInfo
    {
        public string EntityTypeFullName { get; set; }
        public string DatabaseName { get; set; }
        public string TableName { get; set; }
        public string Description { get; set; }
        public bool ReadOnly { get; set; } = false;

        public EntityInfo(string entityTypeFullName, string databaseName, string tableName, string description, bool readOnly = false)
        {
            EntityTypeFullName = entityTypeFullName;
            DatabaseName = databaseName;
            TableName = tableName;
            Description = description;
            ReadOnly = readOnly;
        }
    }
}
