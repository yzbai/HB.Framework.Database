#nullable enable

using System;

namespace HB.Framework.Database.Entity
{
    [AttributeUsage(AttributeTargets.Class)]
    public class EntitySchemaAttribute : Attribute
    {
        public string DatabaseName { get; set; }

        public string? TableName { get; set; }

        public string? Description { get; set; }

        public bool ReadOnly { get; set; }

        public string SuffixToRemove { get; set; } = "Entity";

        public EntitySchemaAttribute(string databaseName)
        {
            DatabaseName = databaseName;
        }
    }
}
