using System;
using System.Collections.Generic;
using System.Text;

namespace HB.Framework.Database
{
    public class DatabaseSettings
    {
        public int Version { get; set; }

        public int DefaultVarcharLength { get; set; } = 200;

        public IList<EntitySchema> Entities { get; } = new List<EntitySchema>();

        public bool AutomaticCreateTable { get; set; } = true;

        public IList<string> AssembliesIncludeEntity { get; } = new List<string>();
    }

    public class SchemaInfo
    {
        public bool IsMaster { get; set; } = true;
        public string SchemaName { get; set; }
        public string ConnectionString { get; set; }
    }

    public class EntitySchema
    {
        //public string Assembly { get; set; }
        public string EntityTypeFullName { get; set; }
        public string DatabaseName { get; set; }
        public string TableName { get; set; }
        public string Description { get; set; }
        public bool ReadOnly { get; set; }
    }
}
