#nullable enable

using System.Diagnostics.CodeAnalysis;

namespace HB.Framework.Database
{
    public class EntityInfo
    {
        public string EntityTypeFullName { get; set; }

        public string? DatabaseName { get; set; }
        
        public string? TableName { get; set; }
        
        public string? Description { get; set; }

        public bool ReadOnly { get; set; }

        public EntityInfo(string entityTypeFullName)
        {
            EntityTypeFullName = entityTypeFullName;
        }
    }
}
