using System;
using System.Collections.Generic;
using System.Text;

namespace HB.Framework.Database
{
    public class Migration
    {
        public int OldVersion { get; set; }

        public int NewVersion { get; set; }

        public string SqlStatement { get; set; }

        public string TargetDatabaseName { get; set; }

        public Migration(string targetDatabaseName, int oldVersion, int newVersion, string sql)
        {
            //if (targetDatabaseName.IsNullOrEmpty())
            //{
            //    throw new ArgumentNullException(nameof(targetDatabaseName));
            //}

            if (oldVersion < 1)
            {
                throw new ArgumentException("version should greater than 1.");
            }

            if (newVersion != oldVersion + 1)
            {
                throw new ArgumentException("Now days, you can only take 1 step further each time.");
            }

            //if (sql.IsNullOrEmpty())
            //{
            //    throw new ArgumentNullException(nameof(sql));
            //}

            TargetDatabaseName = targetDatabaseName;
            OldVersion = oldVersion;
            NewVersion = newVersion;
            SqlStatement = sql;
        }
    }
}
