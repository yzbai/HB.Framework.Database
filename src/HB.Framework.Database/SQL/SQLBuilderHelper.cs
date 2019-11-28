using HB.Framework.Database.Engine;
using HB.Framework.Database.Entity;
using System;
using System.Collections.Generic;
using System.Text;

namespace HB.Framework.Database.SQL
{
    internal partial class SQLBuilder
    {
        public static string CreateAddTemplate(DatabaseEntityDef definition, DatabaseEngineType engineType)
        {
            StringBuilder args = new StringBuilder();
            StringBuilder selectArgs = new StringBuilder();
            StringBuilder values = new StringBuilder();

            foreach (DatabaseEntityPropertyDef info in definition.Properties)
            {
                if (info.IsTableProperty)
                {
                    selectArgs.AppendFormat(GlobalSettings.Culture, "{0},", info.DbReservedName);

                    if (info.IsAutoIncrementPrimaryKey || info.PropertyName == "LastTime")
                    {
                        continue;
                    }

                    args.AppendFormat(GlobalSettings.Culture, "{0},", info.DbReservedName);

                    values.AppendFormat(GlobalSettings.Culture, " {0},", info.DbParameterizedName);

                }
            }

            if (selectArgs.Length > 0)
            {
                selectArgs.Remove(selectArgs.Length - 1, 1);
            }

            if (args.Length > 0)
            {
                args.Remove(args.Length - 1, 1);
            }

            if (values.Length > 0)
            {
                values.Remove(values.Length - 1, 1);
            }

            DatabaseEntityPropertyDef idProperty = definition.GetProperty("Id");

            return $"insert into {definition.DbTableReservedName}({args.ToString()}) values({values.ToString()});select {selectArgs.ToString()} from {definition.DbTableReservedName} where {idProperty.DbReservedName} = {GetLastInsertIdStatement(engineType)};";
        }

        public static string CreateUpdateTemplate(DatabaseEntityDef modelDef)
        {
            StringBuilder args = new StringBuilder();

            foreach (DatabaseEntityPropertyDef info in modelDef.Properties)
            {
                if (info.IsTableProperty)
                {
                    if (info.IsAutoIncrementPrimaryKey || info.PropertyName == "LastTime" || info.PropertyName == "Deleted")
                    {
                        continue;
                    }

                    args.AppendFormat(GlobalSettings.Culture, " {0}={1},", info.DbReservedName, info.DbParameterizedName);
                }
            }

            if (args.Length > 0)
            {
                args.Remove(args.Length - 1, 1);
            }

            string statement = string.Format(GlobalSettings.Culture, "UPDATE {0} SET {1}", modelDef.DbTableReservedName, args.ToString());

            return statement;
        }

        public static string CreateDeleteTemplate(DatabaseEntityDef modelDef)
        {
            DatabaseEntityPropertyDef deletedProperty = modelDef.GetProperty("Deleted");
            DatabaseEntityPropertyDef lastUserProperty = modelDef.GetProperty("LastUser");

            StringBuilder args = new StringBuilder();

            args.Append($"{deletedProperty.DbReservedName}=1,");
            args.Append($"{lastUserProperty.DbReservedName}={lastUserProperty.DbParameterizedName}");

            return $"UPDATE {modelDef.DbTableReservedName} SET {args.ToString()} ";
        }

        public static string TempTable_Insert(string tempTableName, string value, DatabaseEngineType databaseEngineType)
        {
            return databaseEngineType switch
            {
                DatabaseEngineType.MySQL => $"insert into `{tempTableName}`(`id`) values({value});",
                DatabaseEngineType.SQLite => $"insert into temp.{tempTableName}(\"id\") values({value});",
                _ => "",
            };
        }

        public static string TempTable_Select_All(string tempTableName, DatabaseEngineType databaseEngineType)
        {
            return databaseEngineType switch
            {
                DatabaseEngineType.MySQL => $"select `id` from `{tempTableName}`;",
                DatabaseEngineType.SQLite => $"select id from temp.{tempTableName};",
                _ => "",
            };
        }

        public static string TempTable_Drop(string tempTableName, DatabaseEngineType databaseEngineType)
        {
            return databaseEngineType switch
            {
                DatabaseEngineType.MySQL => $"drop temporary table if exists `{tempTableName}`;",
                DatabaseEngineType.SQLite => $"drop table if EXISTS temp.{tempTableName};",
                _ => "",
            };
        }

        public static string TempTable_Create(string tempTableName, DatabaseEngineType databaseEngineType)
        {
            return databaseEngineType switch
            {
                DatabaseEngineType.MySQL => $"create temporary table `{tempTableName}` ( `id` int not null);",
                DatabaseEngineType.SQLite => $"create temporary table {tempTableName} (\"id\" integer not null);",
                _ => "",
            };
        }

        public static string FoundChanges_Statement(DatabaseEngineType databaseEngineType)
        {
            return databaseEngineType switch
            {
                DatabaseEngineType.MySQL => $"row_count()", // $" found_rows() ",
                DatabaseEngineType.SQLite => $" changes() ",
                _ => "",
            };
        }

        public static string GetLastInsertIdStatement(DatabaseEngineType databaseEngineType)
        {
            return databaseEngineType switch
            {
                DatabaseEngineType.SQLite => "last_insert_rowid()",
                DatabaseEngineType.MySQL => "last_insert_id()",
                _ => "",
            };
        }
    }
}
