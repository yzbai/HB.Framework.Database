using HB.Framework.Database.Engine;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySqlConnector.Logging;
using HB.Framework.Database;
using System.Linq;
using Microsoft.Extensions.Options;

namespace HB.Infrastructure.MySQL
{
    /// <summary>
    /// MySql数据库
    /// </summary>
    internal class MySQLEngine : IDatabaseEngine
    {
        #region 自身 & 构建

        private readonly MySQLOptions _options;
        private Dictionary<string, string> _connectionStringDict;

        public DatabaseSettings DatabaseSettings => _options.DatabaseSettings;

        public DatabaseEngineType EngineType => DatabaseEngineType.MySQL;

        public string FirstDefaultDatabaseName { get; private set; }

        private MySQLEngine() { }

        public MySQLEngine(IOptions<MySQLOptions> options) : this()
        {
            //MySqlConnectorLogManager.Provider = new MicrosoftExtensionsLoggingLoggerProvider(loggerFactory);

            _options = options.Value;

            SetConnectionStrings();
        }

        private void SetConnectionStrings()
        {
            _connectionStringDict = new Dictionary<string, string>();

            foreach (DatabaseConnectionSettings schemaInfo in _options.Connections)
            {
                if (FirstDefaultDatabaseName.IsNullOrEmpty())
                {
                    FirstDefaultDatabaseName = schemaInfo.DatabaseName;
                }

                if (schemaInfo.IsMaster)
                {
                    _connectionStringDict[schemaInfo.DatabaseName + "_1"] = schemaInfo.ConnectionString;

                    if (!_connectionStringDict.ContainsKey(schemaInfo.DatabaseName + "_0"))
                    {
                        _connectionStringDict[schemaInfo.DatabaseName + "_0"] = schemaInfo.ConnectionString;
                    }
                }
                else
                {
                    _connectionStringDict[schemaInfo.DatabaseName + "_0"] = schemaInfo.ConnectionString;
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
            MySqlParameter parameter = new MySqlParameter {
                ParameterName = name,
                Value = value,
                DbType = dbType
            };
            return parameter;
        }

        public IDataParameter CreateParameter(string name, object value)
        {
            MySqlParameter parameter = new MySqlParameter {
                ParameterName = name,
                Value = value
            };
            return parameter;
        }

        public IDbCommand CreateEmptyCommand()
        {
            MySqlCommand command = new MySqlCommand();
            return command;
        }

        #endregion

        #region 方言

        public string ParameterizedChar { get { return MySQLLocalism.ParameterizedChar; } }

        public string QuotedChar { get { return MySQLLocalism.QuotedChar; } }

        public string ReservedChar { get { return MySQLLocalism.ReservedChar; } }

        public string GetQuotedStatement(string name)
        {
            return MySQLLocalism.GetQuoted(name);
        }

        public string GetParameterizedStatement(string name)
        {
            return MySQLLocalism.GetParameterized(name);
        }

        public string GetReservedStatement(string name)
        {
            return MySQLLocalism.GetReserved(name);
        }

        public DbType GetDbType(Type type)
        {
            return MySQLLocalism.GetDbType(type);
        }

        public string GetDbTypeStatement(Type type)
        {
            return MySQLLocalism.GetDbTypeStatement(type);
        }

        public string GetDbValueStatement(object value, bool needQuoted)
        {
            return MySQLLocalism.GetDbValueStatement(value, needQuoted);
        }

        public bool IsValueNeedQuoted(Type type)
        {
            return MySQLLocalism.IsValueNeedQuoted(type);
        }

        #endregion

        #region SystemInfo

        private const string _systemInfoTableName = "tb_sys_info";

        private const string _tbSysInfoCreate =
@"CREATE TABLE `tb_sys_info` (
	`Id` int (11) NOT NULL AUTO_INCREMENT, 
	`Name` varchar(100) DEFAULT NULL, 
	`Value` varchar(1024) DEFAULT NULL,
	PRIMARY KEY(`Id`),
	UNIQUE KEY `Name_UNIQUE` (`Name`)
);
INSERT INTO `tb_sys_info`(`Name`, `Value`) VALUES('Version', '1');
INSERT INTO `tb_sys_info`(`Name`, `Value`) VALUES('DatabaseName', '{0}');";

        private const string _tbSysInfoRetrieve = @"SELECT * FROM `tb_sys_info`;";

        private const string _tbSysInfoUpdateVersion = @"UPDATE `tb_sys_info` SET `Value` = '{0}' WHERE `Name` = 'Version';";

        private const string _isTableExistsStatement = "SELECT count(1) FROM information_schema.TABLES WHERE table_name =@tableName and table_schema=@databaseName;";

        public IEnumerable<string> GetDatabaseNames()
        {
            return _options.Connections.Select(s => s.DatabaseName);
        }

        public async Task<bool> IsTableExistsAsync(string databaseName, string tableName, IDbTransaction transaction)
        {
            using IDbCommand command = CreateEmptyCommand();
            
            command.CommandType = CommandType.Text;
            command.CommandText = _isTableExistsStatement;
            command.Parameters.Add(CreateParameter("@tableName", tableName));
            command.Parameters.Add(CreateParameter("@databaseName", databaseName));

            object result = await ExecuteCommandScalarAsync(transaction, databaseName, command, true).ConfigureAwait(false);

            return Convert.ToBoolean(result, GlobalSettings.Culture);
        }

        public SystemInfo GetSystemInfo(string databaseName, IDbTransaction transaction)
        {
            if (!IsTableExistsAsync(databaseName, _systemInfoTableName, transaction))
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
                tuple = ExecuteSqlReader(transaction, databaseName, _tbSysInfoRetrieve, false);

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
                ExecuteSqlNonQuery(transaction, databaseName, string.Format(GlobalSettings.Culture, _tbSysInfoCreate, databaseName));
            }
            else
            {
                ExecuteSqlNonQuery(transaction, databaseName, string.Format(GlobalSettings.Culture, _tbSysInfoUpdateVersion, version));
            }
        }

        #endregion

        #region SP 能力

        public Task<Tuple<IDbCommand, IDataReader>> ExecuteSPReaderAsync(IDbTransaction Transaction, string dbName, string spName, IList<IDataParameter> dbParameters, bool useMaster = false)
        {
            if (Transaction == null)
            {
                return MySQLExecuter.ExecuteSPReaderAsync(GetConnectionString(dbName, useMaster), spName, dbParameters);
            }
            else
            {
                return MySQLExecuter.ExecuteSPReaderAsync((MySqlTransaction)Transaction, spName, dbParameters);
            }
        }

        public Task<object> ExecuteSPScalarAsync(IDbTransaction Transaction, string dbName, string spName, IList<IDataParameter> parameters, bool useMaster = false)
        {
            if (Transaction == null)
            {
                return MySQLExecuter.ExecuteSPScalarAsync(GetConnectionString(dbName, useMaster), spName, parameters);
            }
            else
            {
                return MySQLExecuter.ExecuteSPScalarAsync((MySqlTransaction)Transaction, spName, parameters);
            }
        }

        public Task<int> ExecuteSPNonQueryAsync(IDbTransaction Transaction, string dbName, string spName, IList<IDataParameter> parameters)
        {
            if (Transaction == null)
            {
                return MySQLExecuter.ExecuteSPNonQueryAsync(GetConnectionString(dbName, true), spName, parameters);
            }
            else
            {
                return MySQLExecuter.ExecuteSPNonQueryAsync((MySqlTransaction)Transaction, spName, parameters);
            }
        }

        #endregion

        #region Command 能力

        public Task<int> ExecuteCommandNonQueryAsync(IDbTransaction Transaction, string dbName, IDbCommand dbCommand)
        {
            if (Transaction == null)
            {
                return MySQLExecuter.ExecuteCommandNonQueryAsync(GetConnectionString(dbName, true), dbCommand);
            }
            else
            {
                return MySQLExecuter.ExecuteCommandNonQueryAsync((MySqlTransaction)Transaction, dbCommand);
            }
        }

        public Task<IDataReader> ExecuteCommandReaderAsync(IDbTransaction Transaction, string dbName, IDbCommand dbCommand, bool useMaster = false)
        {
            if (Transaction == null)
            {
                return MySQLExecuter.ExecuteCommandReaderAsync(GetConnectionString(dbName, useMaster), dbCommand);
            }
            else
            {
                return MySQLExecuter.ExecuteCommandReaderAsync((MySqlTransaction)Transaction, dbCommand);
            }
        }

        public Task<object> ExecuteCommandScalarAsync(IDbTransaction Transaction, string dbName, IDbCommand dbCommand, bool useMaster = false)
        {
            if (Transaction == null)
            {
                return MySQLExecuter.ExecuteCommandScalarAsync(GetConnectionString(dbName, useMaster), dbCommand);
            }
            else
            {
                return MySQLExecuter.ExecuteCommandScalarAsync((MySqlTransaction)Transaction, dbCommand);
            }
        }

        #endregion

        #region 事务

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "<Pending>")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0067:Dispose objects before losing scope", Justification = "<Pending>")]
        public async Task<IDbTransaction> BeginTransactionAsync(string dbName, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            MySqlConnection conn = new MySqlConnection(GetConnectionString(dbName, true));
            await conn.OpenAsync().ConfigureAwait(false);

            return await conn.BeginTransactionAsync(isolationLevel).ConfigureAwait(false);
        }

        public async Task CommitAsync(IDbTransaction transaction)
        {
            MySqlTransaction mySqlTransaction = transaction as MySqlTransaction;

            MySqlConnection connection = mySqlTransaction.Connection;

            try
            {
                await mySqlTransaction.CommitAsync().ConfigureAwait(false);
            }
            finally
            {
                connection.Close();
            }
        }

        public async Task RollbackAsync(IDbTransaction transaction)
        {
            MySqlTransaction mySqlTransaction = transaction as MySqlTransaction;

            MySqlConnection connection = mySqlTransaction.Connection;

            try
            {
                await mySqlTransaction.RollbackAsync().ConfigureAwait(false);
            }
            finally
            {
                connection.Close();
            }
        }

        #endregion
    }
}
