using HB.Framework.Database;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace HB.Infrastructure.SQLite
{
    public class SQLiteOptions : IOptions<SQLiteOptions>
    {
        public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();

        public IList<DatabaseConnectionSettings> Connections { get; } = new List<DatabaseConnectionSettings>();

        public SQLiteOptions Value => this;
    }
}
