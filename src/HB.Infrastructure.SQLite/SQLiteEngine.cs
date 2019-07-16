﻿using HB.Framework.Database;
using HB.Framework.Database.Engine;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using System.Linq;

namespace HB.Infrastructure.SQLite
{
    internal partial class SQLiteEngine : IDatabaseEngine
    {
        #region 自身 & 构建

        private readonly SQLiteOptions _options;
        private Dictionary<string, string> _connectionStringDict;

        public IDatabaseSettings DatabaseSettings => _options.DatabaseSettings;

        public DatabaseEngineType EngineType => DatabaseEngineType.SQLite;

        public string FirstDefaultDatabaseName { get; private set; }

        private SQLiteEngine() { }

        public SQLiteEngine(SQLiteOptions options) : this()
        {
            //MySqlConnectorLogManager.Provider = new MicrosoftExtensionsLoggingLoggerProvider(loggerFactory);

            _options = options;

            SetConnectionStrings();
        }

        private void SetConnectionStrings()
        {
            _connectionStringDict = new Dictionary<string, string>();

            foreach (SchemaInfo schemaInfo in _options.Schemas)
            {
                if (FirstDefaultDatabaseName.IsNullOrEmpty())
                {
                    FirstDefaultDatabaseName = schemaInfo.SchemaName;
                }

                if (schemaInfo.IsMaster)
                {
                    _connectionStringDict[schemaInfo.SchemaName + "_1"] = schemaInfo.ConnectionString;

                    if (!_connectionStringDict.ContainsKey(schemaInfo.SchemaName + "_0"))
                    {
                        _connectionStringDict[schemaInfo.SchemaName + "_0"] = schemaInfo.ConnectionString;
                    }
                }
                else
                {
                    _connectionStringDict[schemaInfo.SchemaName + "_0"] = schemaInfo.ConnectionString;
                }
            }
        }

        private string GetConnectionString(string dbName, bool isMaster)
        {
            if (isMaster)
            {
                return _connectionStringDict[dbName + "_1"];
            }

            return _connectionStringDict[dbName + "_0"];
        }

        #endregion

        #region 创建功能

        public IDataParameter CreateParameter(string name, object value, DbType dbType)
        {
            SqliteParameter parameter = new SqliteParameter {
                ParameterName = name,
                Value = value,
                DbType = dbType
            };
            return parameter;
        }

        public IDataParameter CreateParameter(string name, object value)
        {
            SqliteParameter parameter = new SqliteParameter {
                ParameterName = name,
                Value = value
            };
            return parameter;
        }

        public IDbCommand CreateEmptyCommand()
        {
            SqliteCommand command = new SqliteCommand();
            return command;
        }

        #endregion

        #region 方言

        public string ParameterizedChar { get { return SQLiteUtility.ParameterizedChar; } }

        public string QuotedChar { get { return SQLiteUtility.QuotedChar; } }

        public string ReservedChar { get { return SQLiteUtility.ReservedChar; } }

        public string GetQuotedStatement(string name)
        {
            return SQLiteUtility.GetQuoted(name);
        }

        public string GetParameterizedStatement(string name)
        {
            return SQLiteUtility.GetParameterized(name);
        }

        public string GetReservedStatement(string name)
        {
            return SQLiteUtility.GetReserved(name);
        }

        public DbType GetDbType(Type type)
        {
            return SQLiteUtility.GetDbType(type);
        }

        public string GetDbTypeStatement(Type type)
        {
            return SQLiteUtility.GetDbTypeStatement(type);
        }

        public string GetDbValueStatement(object value, bool needQuoted)
        {
            return SQLiteUtility.GetDbValueStatement(value, needQuoted);
        }

        public bool IsValueNeedQuoted(Type type)
        {
            return SQLiteUtility.IsValueNeedQuoted(type);
        }

        #endregion

        #region 事务

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0067:Dispose objects before losing scope", Justification = "<Pending>")]
        public IDbTransaction BeginTransaction(string dbName, IsolationLevel isolationLevel)
        {
            SqliteConnection conn = new SqliteConnection(GetConnectionString(dbName, true));
            conn.Open();

            return conn.BeginTransaction(isolationLevel);
        }

        public void Commit(IDbTransaction transaction)
        {
            IDbConnection dbConnection = transaction.Connection;
            transaction.Commit();
            dbConnection.Close();
        }

        public void Rollback(IDbTransaction transaction)
        {
            IDbConnection dbConnection = transaction.Connection;
            transaction.Rollback();
            dbConnection.Close();
        }



        #endregion

        #region SystemInfo

        private const string systemInfoTableName = "tb_sys_info";

        private const string tbSysInfoCreate =
@"CREATE TABLE ""tb_sys_info"" (
    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
	""Name"" TEXT UNIQUE, 
	""Value"" TEXT
);
INSERT INTO ""tb_sys_info""(""Name"", ""Value"") VALUES('Version', '1');
INSERT INTO ""tb_sys_info""(""Name"", ""Value"") VALUES('DatabaseName', '{0}');";

        private const string tbSysInfoRetrieve = @"SELECT * FROM ""tb_sys_info"";";

        private const string tbSysInfoUpdateVersion = @"UPDATE ""tb_sys_info"" SET ""Value"" = '{0}' WHERE ""Name"" = 'Version';";

        private const string isTableExistsStatement = "SELECT count(1) FROM sqlite_master where type='table' and name='{0}';";

        public IEnumerable<string> GetDatabaseNames()
        {
            return _options.Schemas.Select(s => s.SchemaName);
        }

        public bool IsTableExists(string databaseName, string tableName, IDbTransaction transaction)
        {
            string sql = string.Format(GlobalSettings.Culture, isTableExistsStatement, tableName);

            object result = ExecuteSqlScalar(transaction, databaseName, sql, false);

            return Convert.ToBoolean(result, GlobalSettings.Culture);
        }

        public SystemInfo GetSystemInfo(string databaseName, IDbTransaction transaction)
        {
            if (!IsTableExists(databaseName, systemInfoTableName, transaction))
            {
                return new SystemInfo
                {
                    DatabaseName = databaseName,
                    Version = 0
                };
            }

            Tuple<IDbCommand, IDataReader> tuple = null;

            try
            {
                tuple = ExecuteSqlReader(transaction, databaseName, tbSysInfoRetrieve, false);

                SystemInfo systemInfo = new SystemInfo { DatabaseName = databaseName };

                while (tuple.Item2.Read())
                {
                    systemInfo.Add(tuple.Item2["Name"].ToString(), tuple.Item2["Value"].ToString());
                }

                return systemInfo;
            }
            finally
            {
                tuple.Item2?.Dispose();
                tuple.Item1?.Dispose();
            }
        }

        public void UpdateSystemVersion(string databaseName, int version, IDbTransaction transaction)
        {
            if (version == 1)
            {
                //创建SystemInfo
                ExecuteSqlNonQuery(transaction, databaseName, string.Format(GlobalSettings.Culture, tbSysInfoCreate, databaseName));
            }
            else
            {
                ExecuteSqlNonQuery(transaction, databaseName, string.Format(GlobalSettings.Culture, tbSysInfoUpdateVersion, version));
            }
        }

        #endregion

        #region SP执行功能

        /// <summary>
        /// 使用完毕后必须Dispose
        /// </summary>
        /// <param name="Transaction"></param>
        /// <param name="spName"></param>
        /// <param name="dbParameters"></param>
        /// <returns></returns>
        public Tuple<IDbCommand,IDataReader> ExecuteSPReader(IDbTransaction Transaction, string dbName, string spName, IList<IDataParameter> dbParameters, bool useMaster)
        {
            throw new NotImplementedException("SQLite Not Support Stored Procedure");
        }

        public object ExecuteSPScalar(IDbTransaction Transaction, string dbName, string spName, IList<IDataParameter> parameters, bool useMaster)
        {
            throw new NotImplementedException("SQLite Not Support Stored Procedure");
        }

        public int ExecuteSPNonQuery(IDbTransaction Transaction, string dbName, string spName, IList<IDataParameter> parameters)
        {
            throw new NotImplementedException("SQLite Not Support Stored Procedure");
        }

        #endregion

        #region Command执行功能

        public int ExecuteCommandNonQuery(IDbTransaction Transaction, string dbName, IDbCommand dbCommand)
        {
            if (Transaction == null)
            {
                return SQLiteExecuter.ExecuteCommandNonQuery(GetConnectionString(dbName, true), dbCommand);
            }
            else
            {
                return SQLiteExecuter.ExecuteCommandNonQuery((SqliteTransaction)Transaction, dbCommand);
            }
        }

        /// <summary>
        /// 使用完毕后必须Dispose，必须使用using
        /// </summary>
        /// <param name="Transaction"></param>
        /// <param name="dbCommand"></param>
        /// <returns></returns>
        public IDataReader ExecuteCommandReader(IDbTransaction Transaction, string dbName, IDbCommand dbCommand, bool useMaster)
        {
            if (Transaction == null)
            {
                return SQLiteExecuter.ExecuteCommandReader(GetConnectionString(dbName, useMaster), dbCommand);
            }
            else
            {
                return SQLiteExecuter.ExecuteCommandReader((SqliteTransaction)Transaction, dbCommand);
            }
        }

        public object ExecuteCommandScalar(IDbTransaction Transaction, string dbName, IDbCommand dbCommand, bool useMaster)
        {
            if (Transaction == null)
            {
                return SQLiteExecuter.ExecuteCommandScalar(GetConnectionString(dbName, useMaster), dbCommand);
            }
            else
            {
                return SQLiteExecuter.ExecuteCommandScalar((SqliteTransaction)Transaction, dbCommand);
            }
        }

        #endregion

        #region SQL 执行能力

        public int ExecuteSqlNonQuery(IDbTransaction Transaction, string dbName, string SQL)
        {
            if (Transaction == null)
            {
                return SQLiteExecuter.ExecuteSqlNonQuery(GetConnectionString(dbName, true), SQL);
            }
            else
            {
                return SQLiteExecuter.ExecuteSqlNonQuery((SqliteTransaction)Transaction, SQL);
            }
        }

        /// <summary>
        /// 使用后必须Dispose，必须使用using.
        /// </summary>
        public Tuple<IDbCommand, IDataReader> ExecuteSqlReader(IDbTransaction Transaction, string dbName, string SQL, bool useMaster)
        {
            if (Transaction == null)
            {
                return SQLiteExecuter.ExecuteSqlReader(GetConnectionString(dbName, useMaster), SQL);
            }
            else
            {
                return SQLiteExecuter.ExecuteSqlReader((SqliteTransaction)Transaction, SQL);
            }
        }

        public object ExecuteSqlScalar(IDbTransaction Transaction, string dbName, string SQL, bool useMaster)
        {
            if (Transaction == null)
            {
                return SQLiteExecuter.ExecuteSqlScalar(GetConnectionString(dbName, useMaster), SQL);
            }
            else
            {
                return SQLiteExecuter.ExecuteSqlScalar((SqliteTransaction)Transaction, SQL);
            }
        }

        

        #endregion
    }
}