using HB.Framework.Database;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace HB.Infrastructure.MySQL
{
    public class MySQLOptions : IOptions<MySQLOptions>
    {
        public DatabaseSettings DatabaseSettings { get; set; } = new DatabaseSettings();

        public IList<DatabaseConnectionSettings> Connections { get; } = new List<DatabaseConnectionSettings>();

        public MySQLOptions Value => this;
    }
}