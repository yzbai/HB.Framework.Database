using HB.Framework.Database;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace HB.Infrastructure.SQLite
{
    public class SQLiteOptions : IOptions<SQLiteOptions>
    {
        public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();

        public IList<SchemaInfo> Schemas { get; } = new List<SchemaInfo>();

        public SQLiteOptions Value => this;
    }
}
