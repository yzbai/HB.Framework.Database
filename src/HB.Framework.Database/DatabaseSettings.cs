#nullable enable

using System.Collections.Generic;

namespace HB.Framework.Database
{
    public class DatabaseSettings
    {
        public int Version { get; set; }

        public int DefaultVarcharLength { get; set; } = 200;

        public IList<EntityInfo> Entities { get; } = new List<EntityInfo>();

        public bool AutomaticCreateTable { get; set; } = true;

        public IList<string> AssembliesIncludeEntity { get; } = new List<string>();
    }
}
