using HB.Framework.Database;
using HB.Framework.Database.Engine;
using System;
using System.Collections.Generic;
using System.Text;

namespace HB.Infrastructure.MySQL
{
    public class MySQLBuilder
    {
        private readonly MySQLOptions _mysqlOptions;

        private MySQLBuilder() { }

        public MySQLBuilder (MySQLOptions mySQLOptions)
        {
            _mysqlOptions = mySQLOptions;

        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly", Justification = "<Pending>")]
        public IDatabaseEngine Build()
        {
            if (_mysqlOptions == null)
            {
                throw new ArgumentNullException("mySQLOptions");
            }

            return  new MySQLEngine(_mysqlOptions);
        }
    }
}
