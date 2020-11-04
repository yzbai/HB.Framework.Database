#nullable enable

using HB.Framework.Database.Engine;
using HB.Framework.Database.Entity;
using HB.Framework.Database.Properties;
using HB.Framework.Database.SQL;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace HB.Framework.Database
{
    /// <summary>
    /// 实现单表的数据库与内存映射
    /// 数据库 Write/Read Controller
    /// 要求：每张表必须有一个主键，且主键必须为int。
    /// 异常处理设置：DAL层处理DbException,其他Exception直接扔出。每个数据库执行者，只扔出异常。
    /// 异常处理，只用在写操作上。
    /// 乐观锁用在写操作上，交由各个数据库执行者实施，Version方式。
    /// 批量操作，采用事务方式，也交由各个数据库执行者实施。
    /// </summary>
    internal class DefaultDatabase : IDatabase
    {
        private readonly DatabaseCommonSettings _databaseSettings;
        private readonly IDatabaseEngine _databaseEngine;
        private readonly IDatabaseEntityDefFactory _entityDefFactory;
        private readonly IDatabaseEntityMapper _modelMapper;
        private readonly ISQLBuilder _sqlBuilder;
        private readonly ILogger _logger;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="databaseEngine"></param>
        /// <param name="modelDefFactory"></param>
        /// <param name="modelMapper"></param>
        /// <param name="sqlBuilder"></param>
        /// <exception cref="System.ArgumentException"></exception>
        public DefaultDatabase(
            IDatabaseEngine databaseEngine,
            IDatabaseEntityDefFactory modelDefFactory,
            IDatabaseEntityMapper modelMapper,
            ISQLBuilder sqlBuilder,
            ILogger<DefaultDatabase> logger)
        {
            _databaseSettings = databaseEngine.DatabaseSettings;
            _databaseEngine = databaseEngine;
            _entityDefFactory = modelDefFactory;
            _modelMapper = modelMapper;
            _sqlBuilder = sqlBuilder;
            _logger = logger;

            if (_databaseSettings.Version < 0)
            {
                throw new ArgumentException(Resources.VersionShouldBePositiveMessage);
            }
        }

        #region Initialize

        /// <summary>
        /// InitializeAsync
        /// </summary>
        /// <param name="migrations"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task InitializeAsync(IEnumerable<Migration>? migrations = null)
        {
            if (_databaseSettings.AutomaticCreateTable)
            {
                await AutoCreateTablesIfBrandNewAsync().ConfigureAwait(false);

                _logger.LogInformation("Database Auto Create Tables Finished.");
            }

            if (migrations != null && migrations.Any())
            {
                await MigarateAsync(migrations).ConfigureAwait(false);

                _logger.LogInformation("Database Migarate Finished.");
            }

            _logger.LogInformation("Database Initialize Finished. Good To Go!");
        }

        /// <summary>
        /// AutoCreateTablesIfBrandNewAsync
        /// </summary>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        private async Task AutoCreateTablesIfBrandNewAsync()
        {
            await _databaseEngine.GetDatabaseNames().ForEachAsync(
                async databaseName =>
                {
                    TransactionContext transactionContext = await BeginTransactionAsync(databaseName, IsolationLevel.Serializable).ConfigureAwait(false);

                    try
                    {
                        SystemInfo sys = await GetSystemInfoAsync(databaseName, transactionContext.Transaction).ConfigureAwait(false);
                        //表明是新数据库
                        if (sys.Version == 0)
                        {
                            if (_databaseSettings.Version != 1)
                            {
                                await RollbackAsync(transactionContext).ConfigureAwait(false);
                                throw new DatabaseException(ServerErrorCode.DatabaseTableCreateError,
                                                            "",
                                                            $"Database:{databaseName} does not exists, database Version must be 1");
                            }

                            await CreateTablesByDatabaseAsync(databaseName, transactionContext).ConfigureAwait(false);

                            await UpdateSystemVersionAsync(databaseName, 1, transactionContext.Transaction).ConfigureAwait(false);
                        }

                        await CommitAsync(transactionContext).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await RollbackAsync(transactionContext).ConfigureAwait(false);

                        if (ex is DatabaseException)
                        {
                            throw;
                        }

                        throw new DatabaseException(ServerErrorCode.DatabaseTableCreateError, null, $"Database:{databaseName}", ex);
                    }

                }).ConfigureAwait(false);
        }

        /// <summary>
        /// CreateTableAsync
        /// </summary>
        /// <param name="def"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        private Task<int> CreateTableAsync(DatabaseEntityDef def, TransactionContext transContext)
        {
            if (!def.IsTableModel)
            {
                throw new DatabaseException(ServerErrorCode.DatabaseNotATableModel, def.EntityFullName);
            }

            IDbCommand command = _sqlBuilder.CreateTableCommand(def.EntityType, false);

            return _databaseEngine.ExecuteCommandNonQueryAsync(transContext.Transaction, def.DatabaseName!, command);
        }

        /// <summary>
        /// CreateTablesByDatabaseAsync
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="transactionContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        private async Task CreateTablesByDatabaseAsync(string databaseName, TransactionContext transactionContext)
        {
            await _entityDefFactory
                .GetAllDefsByDatabase(databaseName)
                .ForEachAsync(async def => await CreateTableAsync(def, transactionContext).ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Migarate
        /// </summary>
        /// <param name="migrations"></param>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        private async Task MigarateAsync(IEnumerable<Migration> migrations)
        {
            if (migrations != null && migrations.Any(m => m.NewVersion <= m.OldVersion))
            {
                throw new DatabaseException(ServerErrorCode.DatabaseMigrateError, "", Resources.MigrationVersionErrorMessage);
            }

            await _databaseEngine.GetDatabaseNames().ForEachAsync(async databaseName =>
             {

                 TransactionContext transactionContext = await BeginTransactionAsync(databaseName, IsolationLevel.Serializable).ConfigureAwait(false);

                 try
                 {
                     SystemInfo sys = await GetSystemInfoAsync(databaseName, transactionContext.Transaction).ConfigureAwait(false);

                     if (sys.Version < _databaseSettings.Version)
                     {
                         if (migrations == null)
                         {
                             throw new DatabaseException(ServerErrorCode.DatabaseMigrateError, "", $"Lack Migrations for {sys.DatabaseName}");
                         }

                         IOrderedEnumerable<Migration> curOrderedMigrations = migrations
                             .Where(m => m.TargetSchema.Equals(sys.DatabaseName, GlobalSettings.ComparisonIgnoreCase))
                             .OrderBy(m => m.OldVersion);

                         if (curOrderedMigrations == null)
                         {
                             throw new DatabaseException(ServerErrorCode.DatabaseMigrateError, "", $"Lack Migrations for {sys.DatabaseName}");
                         }

                         if (!CheckMigration(sys.Version, _databaseSettings.Version, curOrderedMigrations))
                         {
                             throw new DatabaseException(ServerErrorCode.DatabaseMigrateError, "", $"Can not perform Migration on ${sys.DatabaseName}, because the migrations provided is not sufficient.");
                         }

                         await curOrderedMigrations.ForEachAsync(async migration =>
                         {
                             IDbCommand command = _databaseEngine.CreateEmptyCommand();
                             command.CommandType = CommandType.Text;
                             command.CommandText = migration.SqlStatement;
                             await _databaseEngine.ExecuteCommandNonQueryAsync(transactionContext.Transaction, databaseName, command).ConfigureAwait(false);
                         }).ConfigureAwait(false);

                         await UpdateSystemVersionAsync(sys.DatabaseName, _databaseSettings.Version, transactionContext.Transaction).ConfigureAwait(false);
                     }

                     await CommitAsync(transactionContext).ConfigureAwait(false);
                 }
                 catch (Exception ex)
                 {
                     await RollbackAsync(transactionContext).ConfigureAwait(false);
                     //throw new DatabaseException(DatabaseError.MigrateError, "", $"Migration Failed at Database:{databaseName}", ex);

                     if (ex is DatabaseException)
                     {
                         throw;
                     }

                     throw new DatabaseException(ServerErrorCode.DatabaseMigrateError, null, $"Database:{databaseName}", ex);
                 }
             }).ConfigureAwait(false);
        }

        private static bool CheckMigration(int startVersion, int endVersion, IOrderedEnumerable<Migration> curOrderedMigrations)
        {
            int curVersion = curOrderedMigrations.ElementAt(0).OldVersion;

            if (curVersion != startVersion) { return false; }

            foreach (Migration migration in curOrderedMigrations)
            {
                if (curVersion != migration.OldVersion)
                {
                    return false;
                }

                curVersion = migration.NewVersion;
            }

            return curVersion == endVersion;
        }

        #endregion

        #region SystemInfo

        /// <summary>
        /// IsTableExistsAsync
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="tableName"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        /// <exception cref="InvalidCastException">Ignore.</exception>
        private async Task<bool> IsTableExistsAsync(string databaseName, string tableName, IDbTransaction transaction)
        {
            using IDbCommand command = _sqlBuilder.CreateIsTableExistCommand(databaseName, tableName);

            object result = await _databaseEngine.ExecuteCommandScalarAsync(transaction, databaseName, command, true).ConfigureAwait(false);

            return Convert.ToBoolean(result, GlobalSettings.Culture);
        }

        /// <summary>
        /// GetSystemInfoAsync
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        public async Task<SystemInfo> GetSystemInfoAsync(string databaseName, IDbTransaction transaction)
        {
            bool isExisted = await IsTableExistsAsync(databaseName, SystemInfoNames.SystemInfoTableName, transaction).ConfigureAwait(false);

            if (!isExisted)
            {
                return new SystemInfo(databaseName) { Version = 0 };
            }

            using IDbCommand command = _sqlBuilder.CreateRetrieveSystemInfoCommand();

            using IDataReader reader = await _databaseEngine.ExecuteCommandReaderAsync(transaction, databaseName, command, false).ConfigureAwait(false);

            SystemInfo systemInfo = new SystemInfo(databaseName);

            while (reader.Read())
            {
                systemInfo.Set(reader["Name"].ToString(), reader["Value"].ToString());
            }

            return systemInfo;
        }


        /// <summary>
        /// UpdateSystemVersionAsync
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="version"></param>
        /// <param name="transaction"></param>
        /// <returns></returns>
        /// <exception cref="DatabaseException"></exception>
        public async Task UpdateSystemVersionAsync(string databaseName, int version, IDbTransaction transaction)
        {
            using IDbCommand command = _sqlBuilder.CreateUpdateSystemVersionCommand(databaseName, version);

            await _databaseEngine.ExecuteCommandNonQueryAsync(transaction, databaseName, command).ConfigureAwait(false);
        }

        #endregion

        #region 条件构造

        public SelectExpression<T> Select<T>() where T : DatabaseEntity, new()
        {
            return _sqlBuilder.NewSelect<T>();
        }

        public FromExpression<T> From<T>() where T : DatabaseEntity, new()
        {
            return _sqlBuilder.NewFrom<T>();
        }

        public WhereExpression<T> Where<T>() where T : DatabaseEntity, new()
        {
            return _sqlBuilder.NewWhere<T>();
        }

        #endregion

        #region 表创建SQL

        //public string GetTableCreateCommand(Type type, bool addDropStatement)
        //{

        //    IDbCommand command = _databaseEngine.CreateEmptyCommand();
        //    command.CommandType = CommandType.Text;
        //    command.CommandText = _sqlBuilder.CreateTableCommand(type, addDropStatement);
        //}

        #endregion

        #region 单表查询, Select, From, Where

        /// <summary>
        /// ScalarAsync
        /// </summary>
        /// <param name="selectCondition"></param>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task<T?> ScalarAsync<T>(SelectExpression<T>? selectCondition, FromExpression<T>? fromCondition, WhereExpression<T>? whereCondition, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            IEnumerable<T> lst = await RetrieveAsync<T>(selectCondition, fromCondition, whereCondition, transContext).ConfigureAwait(false);

            if (lst.IsNullOrEmpty())
            {
                return null;
            }

            if (lst.Count() > 1)
            {
                string detail = $"Scalar retrieve return more than one result. Select:{selectCondition}, From:{fromCondition}, Where:{whereCondition}";
                DatabaseException exception = new DatabaseException(ServerErrorCode.DatabaseFoundTooMuch, typeof(T).FullName, detail);
                //_logger.LogException(exception);

                throw exception;
            }

            return lst.ElementAt(0);
        }

        /// <summary>
        /// RetrieveAsync
        /// </summary>
        /// <param name="selectCondition"></param>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task<IEnumerable<TSelect>> RetrieveAsync<TSelect, TFrom, TWhere>(SelectExpression<TSelect>? selectCondition, FromExpression<TFrom>? fromCondition, WhereExpression<TWhere>? whereCondition, TransactionContext? transContext = null)
            where TSelect : DatabaseEntity, new()
            where TFrom : DatabaseEntity, new()
            where TWhere : DatabaseEntity, new()
        {
            #region Argument Adjusting

            if (selectCondition != null)
            {
                selectCondition.Select(t => t.Id).Select(t => t.Deleted).Select(t => t.LastTime).Select(t => t.LastUser).Select(t => t.Version);
            }

            if (whereCondition == null)
            {
                whereCondition = Where<TWhere>();
            }

            whereCondition.And(t => t.Deleted == false).And<TSelect>(ts => ts.Deleted == false).And<TFrom>(tf => tf.Deleted == false);

            #endregion

            IList<TSelect> result;
            IDbCommand? command = null;
            IDataReader? reader = null;
            DatabaseEntityDef selectDef = _entityDefFactory.GetDef<TSelect>();

            try
            {
                command = _sqlBuilder.CreateRetrieveCommand(selectCondition, fromCondition, whereCondition);

                reader = await _databaseEngine.ExecuteCommandReaderAsync(transContext?.Transaction, selectDef.DatabaseName!, command, transContext != null).ConfigureAwait(false);

                result = _modelMapper.ToList<TSelect>(reader);
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                //if (ex is DatabaseException)
                //{
                //    throw;
                //}

                string detail = $"select:{selectCondition}, from:{fromCondition}, where:{whereCondition}";
                throw new DatabaseException(ServerErrorCode.DatabaseError, selectDef.EntityFullName, detail, ex);
            }
            finally
            {
                reader?.Dispose();
                command?.Dispose();
            }

            return result;
        }


        /// <summary>
        /// RetrieveAsync
        /// </summary>
        /// <param name="selectCondition"></param>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task<IEnumerable<T>> RetrieveAsync<T>(SelectExpression<T>? selectCondition, FromExpression<T>? fromCondition, WhereExpression<T>? whereCondition, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            #region Argument Adjusting

            if (selectCondition != null)
            {
                selectCondition.Select(t => t.Id).Select(t => t.Deleted).Select(t => t.LastTime).Select(t => t.LastUser).Select(t => t.Version);
            }

            if (whereCondition == null)
            {
                whereCondition = Where<T>();
            }

            whereCondition.And(t => t.Deleted == false);

            #endregion

            IList<T> result;
            IDbCommand? command = null;
            IDataReader? reader = null;
            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            try
            {
                command = _sqlBuilder.CreateRetrieveCommand<T>(selectCondition, fromCondition, whereCondition);

                reader = await _databaseEngine.ExecuteCommandReaderAsync(transContext?.Transaction, entityDef.DatabaseName!, command, transContext != null).ConfigureAwait(false);
                result = _modelMapper.ToList<T>(reader);
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                //if (ex is DatabaseException)
                //{
                //    throw;
                //}

                string detail = $"select:{selectCondition}, from:{fromCondition}, where:{whereCondition}";

                throw new DatabaseException(ServerErrorCode.DatabaseError, entityDef.EntityFullName, detail, ex);
            }
            finally
            {
                reader?.Dispose();
                command?.Dispose();
            }

            return result;
        }

        public Task<IEnumerable<T>> PageAsync<T>(SelectExpression<T>? selectCondition, FromExpression<T>? fromCondition, WhereExpression<T>? whereCondition, long pageNumber, long perPageCount, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            #region Argument Adjusting

            if (selectCondition != null)
            {
                selectCondition.Select(t => t.Id).Select(t => t.Deleted).Select(t => t.LastTime).Select(t => t.LastUser).Select(t => t.Version);
            }

            if (whereCondition == null)
            {
                whereCondition = Where<T>();
            }

            whereCondition.And(t => t.Deleted == false);

            #endregion

            whereCondition.Limit((pageNumber - 1) * perPageCount, perPageCount);

            return RetrieveAsync<T>(selectCondition, fromCondition, whereCondition, transContext);
        }

        /// <summary>
        /// CountAsync
        /// </summary>
        /// <param name="selectCondition"></param>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task<long> CountAsync<T>(SelectExpression<T>? selectCondition, FromExpression<T>? fromCondition, WhereExpression<T>? whereCondition, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            #region Argument Adjusting

            if (selectCondition != null)
            {
                selectCondition.Select(t => t.Id).Select(t => t.Deleted).Select(t => t.LastTime).Select(t => t.LastUser).Select(t => t.Version);
            }

            if (whereCondition == null)
            {
                whereCondition = Where<T>();
            }

            whereCondition.And(t => t.Deleted == false);

            #endregion

            long count = -1;

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();
            try
            {
                IDbCommand command = _sqlBuilder.CreateCountCommand(fromCondition, whereCondition);
                object countObj = await _databaseEngine.ExecuteCommandScalarAsync(transContext?.Transaction, entityDef.DatabaseName!, command, transContext != null).ConfigureAwait(false);
                count = Convert.ToInt32(countObj, GlobalSettings.Culture);
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                string detail = $"select:{selectCondition}, from:{fromCondition}, where:{whereCondition}";
                throw new DatabaseException(ServerErrorCode.DatabaseError, entityDef.EntityFullName, detail, ex);
            }

            return count;
        }

        #endregion

        #region 单表查询, From, Where

        /// <summary>
        /// ScalarAsync
        /// </summary>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public Task<T?> ScalarAsync<T>(FromExpression<T>? fromCondition, WhereExpression<T>? whereCondition, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return ScalarAsync(null, fromCondition, whereCondition, transContext);
        }

        /// <summary>
        /// RetrieveAsync
        /// </summary>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public Task<IEnumerable<T>> RetrieveAsync<T>(FromExpression<T>? fromCondition, WhereExpression<T>? whereCondition, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return RetrieveAsync(null, fromCondition, whereCondition, transContext);
        }

        public Task<IEnumerable<T>> PageAsync<T>(FromExpression<T>? fromCondition, WhereExpression<T>? whereCondition, long pageNumber, long perPageCount, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return PageAsync(null, fromCondition, whereCondition, pageNumber, perPageCount, transContext);
        }

        /// <summary>
        /// CountAsync
        /// </summary>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public Task<long> CountAsync<T>(FromExpression<T>? fromCondition, WhereExpression<T>? whereCondition, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return CountAsync(null, fromCondition, whereCondition, transContext);
        }

        #endregion

        #region 单表查询, Where

        public Task<IEnumerable<T>> RetrieveAllAsync<T>(TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return RetrieveAsync<T>(null, null, null, transContext);
        }

        /// <summary>
        /// ScalarAsync
        /// </summary>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public Task<T?> ScalarAsync<T>(WhereExpression<T>? whereCondition, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return ScalarAsync(null, null, whereCondition, transContext);
        }

        /// <summary>
        /// RetrieveAsync
        /// </summary>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public Task<IEnumerable<T>> RetrieveAsync<T>(WhereExpression<T>? whereCondition, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return RetrieveAsync(null, null, whereCondition, transContext);
        }

        public Task<IEnumerable<T>> PageAsync<T>(WhereExpression<T>? whereCondition, long pageNumber, long perPageCount, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return PageAsync(null, null, whereCondition, pageNumber, perPageCount, transContext);
        }

        public Task<IEnumerable<T>> PageAsync<T>(long pageNumber, long perPageCount, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return PageAsync<T>(null, null, null, pageNumber, perPageCount, transContext);
        }

        /// <summary>
        /// CountAsync
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public Task<long> CountAsync<T>(WhereExpression<T>? condition, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return CountAsync(null, null, condition, transContext);
        }

        public Task<long> CountAsync<T>(TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return CountAsync<T>(null, null, null, transContext);
        }

        #endregion

        #region 单表查询, Expression Where

        public Task<T?> ScalarAsync<T>(long id, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            return ScalarAsync<T>(t => t.Id == id && t.Deleted == false, transContext);
        }

        //public Task<T> RetrieveScalaAsyncr<T>(Expression<Func<T, bool>> whereExpr, DatabaseTransactionContext transContext = false) where T : DatabaseEntity, new();
        /// <summary>
        /// ScalarAsync
        /// </summary>
        /// <param name="whereExpr"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public Task<T?> ScalarAsync<T>(Expression<Func<T, bool>> whereExpr, TransactionContext? transContext) where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();
            whereCondition.Where(whereExpr);

            return ScalarAsync(null, null, whereCondition, transContext);
        }

        /// <summary>
        /// RetrieveAsync
        /// </summary>
        /// <param name="whereExpr"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public Task<IEnumerable<T>> RetrieveAsync<T>(Expression<Func<T, bool>> whereExpr, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();
            whereCondition.Where(whereExpr);

            return RetrieveAsync(null, null, whereCondition, transContext);
        }

        public Task<IEnumerable<T>> PageAsync<T>(Expression<Func<T, bool>> whereExpr, long pageNumber, long perPageCount, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();

            return PageAsync(null, null, whereCondition, pageNumber, perPageCount, transContext);
        }

        /// <summary>
        /// CountAsync
        /// </summary>
        /// <param name="whereExpr"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public Task<long> CountAsync<T>(Expression<Func<T, bool>> whereExpr, TransactionContext? transContext)
            where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();
            whereCondition.Where(whereExpr);

            return CountAsync(null, null, whereCondition, transContext);
        }

        #endregion

        #region 双表查询

        /// <summary>
        /// RetrieveAsync
        /// </summary>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task<IEnumerable<Tuple<TSource, TTarget?>>> RetrieveAsync<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource>? whereCondition, TransactionContext? transContext)
            where TSource : DatabaseEntity, new()
            where TTarget : DatabaseEntity, new()
        {
            if (whereCondition == null)
            {
                whereCondition = Where<TSource>();
            }

            switch (fromCondition.JoinType)
            {
                case SqlJoinType.LEFT:
                    whereCondition.And(t => t.Deleted == false);
                    break;
                case SqlJoinType.RIGHT:
                    whereCondition.And<TTarget>(t => t.Deleted == false);
                    break;
                case SqlJoinType.INNER:
                    whereCondition.And(t => t.Deleted == false).And<TTarget>(t => t.Deleted == false);
                    break;
                case SqlJoinType.FULL:
                    break;
                case SqlJoinType.CROSS:
                    whereCondition.And(t => t.Deleted == false).And<TTarget>(t => t.Deleted == false);
                    break;
            }

            IList<Tuple<TSource, TTarget?>> result;
            IDbCommand? command = null;
            IDataReader? reader = null;
            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<TSource>();

            try
            {
                command = _sqlBuilder.CreateRetrieveCommand<TSource, TTarget>(fromCondition, whereCondition);
                reader = await _databaseEngine.ExecuteCommandReaderAsync(transContext?.Transaction, entityDef.DatabaseName!, command, transContext != null).ConfigureAwait(false);
                result = _modelMapper.ToList<TSource, TTarget>(reader);
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                string detail = $"from:{fromCondition}, where:{whereCondition}";
                DatabaseException exception = new DatabaseException(ServerErrorCode.DatabaseError, entityDef.EntityFullName, detail, ex);

                //_logger.LogException(exception);

                throw exception;
            }
            finally
            {
                reader?.Dispose();
                command?.Dispose();
            }

            return result;
        }

        public Task<IEnumerable<Tuple<TSource, TTarget?>>> PageAsync<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource>? whereCondition, long pageNumber, long perPageCount, TransactionContext? transContext)
            where TSource : DatabaseEntity, new()
            where TTarget : DatabaseEntity, new()
        {
            if (whereCondition == null)
            {
                whereCondition = Where<TSource>();
            }

            whereCondition.Limit((pageNumber - 1) * perPageCount, perPageCount);

            return RetrieveAsync<TSource, TTarget>(fromCondition, whereCondition, transContext);
        }

        /// <summary>
        /// ScalarAsync
        /// </summary>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task<Tuple<TSource, TTarget?>?> ScalarAsync<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource>? whereCondition, TransactionContext? transContext)
            where TSource : DatabaseEntity, new()
            where TTarget : DatabaseEntity, new()
        {
            IEnumerable<Tuple<TSource, TTarget?>> lst = await RetrieveAsync<TSource, TTarget>(fromCondition, whereCondition, transContext).ConfigureAwait(false);

            if (lst.IsNullOrEmpty())
            {
                return null;
            }

            if (lst.Count() > 1)
            {
                string message = $"Scalar retrieve return more than one result. From:{fromCondition}, Where:{whereCondition}";
                DatabaseException exception = new DatabaseException(ServerErrorCode.DatabaseFoundTooMuch, typeof(TSource).FullName, message);
                //_logger.LogException(exception);

                throw exception;
            }

            return lst.ElementAt(0);
        }

        #endregion

        #region 三表查询

        /// <summary>
        /// RetrieveAsync
        /// </summary>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task<IEnumerable<Tuple<TSource, TTarget1?, TTarget2?>>> RetrieveAsync<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource>? whereCondition, TransactionContext? transContext)
            where TSource : DatabaseEntity, new()
            where TTarget1 : DatabaseEntity, new()
            where TTarget2 : DatabaseEntity, new()
        {
            if (whereCondition == null)
            {
                whereCondition = Where<TSource>();
            }

            switch (fromCondition.JoinType)
            {
                case SqlJoinType.LEFT:
                    whereCondition.And(t => t.Deleted == false);
                    break;
                case SqlJoinType.RIGHT:
                    whereCondition.And<TTarget2>(t => t.Deleted == false);
                    break;
                case SqlJoinType.INNER:
                    whereCondition.And(t => t.Deleted == false).And<TTarget1>(t => t.Deleted == false).And<TTarget2>(t => t.Deleted == false);
                    break;
                case SqlJoinType.FULL:
                    break;
                case SqlJoinType.CROSS:
                    whereCondition.And(t => t.Deleted == false).And<TTarget1>(t => t.Deleted == false).And<TTarget2>(t => t.Deleted == false);
                    break;
            }


            IList<Tuple<TSource, TTarget1?, TTarget2?>> result;
            IDbCommand? command = null;
            IDataReader? reader = null;
            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<TSource>();

            try
            {
                command = _sqlBuilder.CreateRetrieveCommand<TSource, TTarget1, TTarget2>(fromCondition, whereCondition);
                reader = await _databaseEngine.ExecuteCommandReaderAsync(transContext?.Transaction, entityDef.DatabaseName!, command, transContext != null).ConfigureAwait(false);
                result = _modelMapper.ToList<TSource, TTarget1, TTarget2>(reader);
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                string detail = $"from:{fromCondition}, where:{whereCondition}";
                throw new DatabaseException(ServerErrorCode.DatabaseError, entityDef.EntityFullName, detail, ex);
            }
            finally
            {
                reader?.Dispose();
                command?.Dispose();
            }


            return result;
        }

        public Task<IEnumerable<Tuple<TSource, TTarget1?, TTarget2?>>> PageAsync<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource>? whereCondition, long pageNumber, long perPageCount, TransactionContext? transContext)
            where TSource : DatabaseEntity, new()
            where TTarget1 : DatabaseEntity, new()
            where TTarget2 : DatabaseEntity, new()
        {
            if (whereCondition == null)
            {
                whereCondition = Where<TSource>();
            }

            whereCondition.Limit((pageNumber - 1) * perPageCount, perPageCount);

            return RetrieveAsync<TSource, TTarget1, TTarget2>(fromCondition, whereCondition, transContext);
        }

        /// <summary>
        /// ScalarAsync
        /// </summary>
        /// <param name="fromCondition"></param>
        /// <param name="whereCondition"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task<Tuple<TSource, TTarget1?, TTarget2?>?> ScalarAsync<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource>? whereCondition, TransactionContext? transContext)
            where TSource : DatabaseEntity, new()
            where TTarget1 : DatabaseEntity, new()
            where TTarget2 : DatabaseEntity, new()
        {
            IEnumerable<Tuple<TSource, TTarget1?, TTarget2?>> lst = await RetrieveAsync<TSource, TTarget1, TTarget2>(fromCondition, whereCondition, transContext).ConfigureAwait(false);

            if (lst.IsNullOrEmpty())
            {
                return null;
            }

            if (lst.Count() > 1)
            {
                string message = $"Scalar retrieve return more than one result. From:{fromCondition}, Where:{whereCondition}";
                DatabaseException exception = new DatabaseException(ServerErrorCode.DatabaseFoundTooMuch, typeof(TSource).FullName, message);
                //_logger.LogException(exception);

                throw exception;
            }

            return lst.ElementAt(0);
        }

        #endregion

        #region 单体更改(Write)

        /// <summary>
        /// 增加,并且item被重新赋值
        /// </summary>
        /// <exception cref="HB.Framework.Common.ValidateErrorException"></exception>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task AddAsync<T>(T item, TransactionContext? transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.NotValid(item);

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(ServerErrorCode.DatabaseNotWriteable, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
            }

            IDbCommand? dbCommand = null;
            IDataReader? reader = null;

            try
            {
                dbCommand = _sqlBuilder.CreateAddCommand(item, "default");

                reader = await _databaseEngine.ExecuteCommandReaderAsync(transContext?.Transaction, entityDef.DatabaseName!, dbCommand, true).ConfigureAwait(false);

                _modelMapper.ToObject(reader, item);
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                string detail = $"Item:{SerializeUtil.ToJson(item)}";

                throw new DatabaseException(ServerErrorCode.DatabaseError, entityDef.EntityFullName, detail, ex); ;
            }
            finally
            {
                reader?.Dispose();
                dbCommand?.Dispose();
            }
        }

        /// <summary>
        /// 删除, Version控制
        /// </summary>
        /// <exception cref="HB.Framework.Common.ValidateErrorException"></exception>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task DeleteAsync<T>(T item, TransactionContext? transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.NotValid(item);

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(ServerErrorCode.DatabaseNotWriteable, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
            }

            long id = item.Id;
            long version = item.Version;
            WhereExpression<T> condition = Where<T>().Where(t => t.Id == id && t.Deleted == false && t.Version == version);

            try
            {
                IDbCommand dbCommand = _sqlBuilder.CreateDeleteCommand(condition, "default");

                long rows = await _databaseEngine.ExecuteCommandNonQueryAsync(transContext?.Transaction, entityDef.DatabaseName!, dbCommand).ConfigureAwait(false);

                if (rows == 1)
                {
                    return;
                }
                else if (rows == 0)
                {
                    throw new DatabaseException(ServerErrorCode.DatabaseNotFound, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
                }

                throw new DatabaseException(ServerErrorCode.DatabaseFoundTooMuch, entityDef.EntityFullName, $"Multiple Rows Affected instead of one. Something go wrong. Entity:{SerializeUtil.ToJson(item)}");
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                string detail = $"Item:{SerializeUtil.ToJson(item)}";
                throw new DatabaseException(ServerErrorCode.DatabaseError, entityDef.EntityFullName, detail, ex);
            }
        }

        /// <summary>
        ///  修改，建议每次修改前先select，并放置在一个事务中。
        ///  版本控制，如果item中Version未赋值，会无法更改
        /// </summary>
        /// <exception cref="HB.Framework.Common.ValidateErrorException"></exception>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task UpdateAsync<T>(T item, TransactionContext? transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.NotValid(item);

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(ServerErrorCode.DatabaseNotWriteable, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
            }

            WhereExpression<T> condition = Where<T>();

            long id = item.Id;
            long version = item.Version;

            condition.Where(t => t.Id == id).And(t => t.Deleted == false);

            //版本控制
            condition.And(t => t.Version == version);

            try
            {
                IDbCommand dbCommand = _sqlBuilder.CreateUpdateCommand(condition, item, "default");
                long rows = await _databaseEngine.ExecuteCommandNonQueryAsync(transContext?.Transaction, entityDef.DatabaseName!, dbCommand).ConfigureAwait(false);

                if (rows == 1)
                {
                    item.Version++;
                    return;
                }
                else if (rows == 0)
                {
                    throw new DatabaseException(ServerErrorCode.DatabaseNotFound, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
                }

                throw new DatabaseException(ServerErrorCode.DatabaseFoundTooMuch, entityDef.EntityFullName, $"Multiple Rows Affected instead of one. Something go wrong. Entity:{SerializeUtil.ToJson(item)}");
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                string detail = $"Item:{SerializeUtil.ToJson(item)}";
                throw new DatabaseException(ServerErrorCode.DatabaseError, entityDef.EntityFullName, detail, ex);
            }
        }

        #endregion

        #region 批量更改(Write)

        /// <summary>
        /// BatchAddAsync
        /// </summary>
        /// <param name="items"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Common.ValidateErrorException"></exception>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task<IEnumerable<long>> BatchAddAsync<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.NotValid(items);

            if (!items.Any())
            {
                return new List<long>();
            }

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(ServerErrorCode.DatabaseNotWriteable, entityDef.EntityFullName, $"Items:{SerializeUtil.ToJson(items)}");
            }

            IDbCommand? dbCommand = null;
            IDataReader? reader = null;

            try
            {
                IList<long> newIds = new List<long>();

                dbCommand = _sqlBuilder.CreateBatchAddCommand(items, "default");
                reader = await _databaseEngine.ExecuteCommandReaderAsync(
                    transContext.Transaction,
                    entityDef.DatabaseName!,
                    dbCommand,
                    true).ConfigureAwait(false);

                while (reader.Read())
                {
                    //int newId = reader.GetInt32(0);

                    //if (newId <= 0)
                    //{
                    //    return DatabaseResult.NewIdError(databaseName: entityDef.DatabaseName, operation: "BatchAddAsync", entityName: entityDef.EntityFullName, lastUser: lastUser);
                    //}

                    newIds.Add(reader.GetInt64(0));
                }

                if (newIds.Count != items.Count())
                {
                    throw new DatabaseException(ServerErrorCode.DatabaseNotMatch, entityDef.EntityFullName, $"Items:{SerializeUtil.ToJson(items)}");
                }

                return newIds;
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                string detail = $"Items:{SerializeUtil.ToJson(items)}";
                throw new DatabaseException(ServerErrorCode.DatabaseError, entityDef.EntityFullName, detail, ex);
            }
            finally
            {
                reader?.Dispose();
                dbCommand?.Dispose();
            }
        }

        /// <summary>
        /// 批量更改
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Common.ValidateErrorException"></exception>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task BatchUpdateAsync<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.NotValid(items);

            if (!items.Any())
            {
                return;
            }

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(ServerErrorCode.DatabaseNotWriteable, entityDef.EntityFullName, $"Items:{SerializeUtil.ToJson(items)}");
            }

            IDbCommand? dbCommand = null;
            IDataReader? reader = null;

            try
            {
                dbCommand = _sqlBuilder.CreateBatchUpdateCommand(items, "default");
                reader = await _databaseEngine.ExecuteCommandReaderAsync(
                    transContext.Transaction,
                    entityDef.DatabaseName!,
                    dbCommand,
                    true).ConfigureAwait(false);

                int count = 0;

                while (reader.Read())
                {
                    int matched = reader.GetInt32(0);

                    if (matched != 1)
                    {
                        throw new DatabaseException(ServerErrorCode.DatabaseNotFound, entityDef.EntityFullName, $"BatchUpdate wrong, not found the {" + count + "}th data item. Items:{SerializeUtil.ToJson(items)}");
                    }

                    count++;
                }

                if (count != items.Count())
                    throw new DatabaseException(ServerErrorCode.DatabaseNotFound, entityDef.EntityFullName, $"BatchUpdate wrong number return. Some data item not found. Items:{SerializeUtil.ToJson(items)}");
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                string detail = $"Items:{SerializeUtil.ToJson(items)}";
                throw new DatabaseException(ServerErrorCode.DatabaseError, entityDef.EntityFullName, detail, ex);
            }
            finally
            {
                reader?.Dispose();
                dbCommand?.Dispose();
            }
        }

        /// <summary>
        /// BatchDeleteAsync
        /// </summary>
        /// <param name="items"></param>
        /// <param name="transContext"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Common.ValidateErrorException"></exception>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        /// <exception cref="IndexOutOfRangeException">Ignore.</exception>
        public async Task BatchDeleteAsync<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.NotValid(items);

            if (!items.Any())
            {
                return;
            }

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(ServerErrorCode.DatabaseNotWriteable, entityDef.EntityFullName, $"Items:{SerializeUtil.ToJson(items)}");
            }

            IDbCommand? dbCommand = null;
            IDataReader? reader = null;

            try
            {
                dbCommand = _sqlBuilder.CreateBatchDeleteCommand(items, "default");
                reader = await _databaseEngine.ExecuteCommandReaderAsync(
                    transContext.Transaction,
                    entityDef.DatabaseName!,
                    dbCommand,
                    true).ConfigureAwait(false);

                int count = 0;

                while (reader.Read())
                {
                    int affected = reader.GetInt32(0);

                    if (affected != 1)
                    {
                        throw new DatabaseException(ServerErrorCode.DatabaseNotFound, entityDef.EntityFullName, $"BatchDelete wrong, not found the {" + count + "}th data item. Items:{SerializeUtil.ToJson(items)}");
                    }

                    count++;
                }

                if (count != items.Count())
                {
                    throw new DatabaseException(ServerErrorCode.DatabaseNotFound, entityDef.EntityFullName, $"BatchDelete wrong number return. Some data item not found. Items:{SerializeUtil.ToJson(items)}");
                }
            }
            catch (Exception ex) when (!(ex is DatabaseException))
            {
                string detail = $"Items:{SerializeUtil.ToJson(items)}";
                throw new DatabaseException(ServerErrorCode.DatabaseError, entityDef.EntityFullName, detail, ex);
            }
            finally
            {
                reader?.Dispose();
                dbCommand?.Dispose();
            }
        }

        #endregion

        #region 事务

        public async Task<TransactionContext> BeginTransactionAsync(string databaseName, IsolationLevel isolationLevel)
        {
            IDbTransaction dbTransaction = await _databaseEngine.BeginTransactionAsync(databaseName, isolationLevel).ConfigureAwait(false);

            return new TransactionContext(dbTransaction, TransactionStatus.InTransaction);
        }

        /// <summary>
        /// BeginTransactionAsync
        /// </summary>
        /// <param name="isolationLevel"></param>
        /// <returns></returns>
        /// <exception cref="HB.Framework.Database.DatabaseException">Ignore.</exception>
        public Task<TransactionContext> BeginTransactionAsync<T>(IsolationLevel isolationLevel) where T : DatabaseEntity
        {
            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.IsTableModel)
            {
                throw new DatabaseException(ServerErrorCode.DatabaseNotATableModel, entityDef.EntityFullName);
            }

            return BeginTransactionAsync(entityDef.DatabaseName!, isolationLevel);
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task CommitAsync(TransactionContext context)
        {
            //if (context == null || context.Transaction == null)
            //{
            //    throw new ArgumentNullException(nameof(context));
            //}

            if (context.Status == TransactionStatus.Commited)
            {
                return;
            }

            if (context.Status != TransactionStatus.InTransaction)
            {
                throw new DatabaseException(ServerErrorCode.DatabaseTransactionError, Resources.TransactionAlreadyFinishedMessage);
            }

            try
            {
                IDbConnection conn = context.Transaction.Connection;

                await _databaseEngine.CommitAsync(context.Transaction).ConfigureAwait(false);
                //context.Transaction.Commit();

                context.Transaction.Dispose();

                if (conn != null && conn.State != ConnectionState.Closed)
                {
                    conn.Dispose();
                }

                context.Status = TransactionStatus.Commited;
            }
            catch
            {
                context.Status = TransactionStatus.Failed;
                throw;
            }
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        public async Task RollbackAsync(TransactionContext context)
        {
            //if (context == null || context.Transaction == null)
            //{
            //    throw new ArgumentNullException(nameof(context));
            //}

            if (context.Status == TransactionStatus.Rollbacked)
            {
                return;
            }

            if (context.Status != TransactionStatus.InTransaction)
            {
                throw new DatabaseException(ServerErrorCode.DatabaseTransactionError, Resources.TransactionAlreadyFinishedMessage);
            }

            try
            {
                IDbConnection conn = context.Transaction.Connection;

                await _databaseEngine.RollbackAsync(context.Transaction).ConfigureAwait(false);
                //context.Transaction.Rollback();

                context.Transaction.Dispose();

                if (conn != null && conn.State != ConnectionState.Closed)
                {
                    conn.Dispose();
                }

                context.Status = TransactionStatus.Rollbacked;
            }
            catch
            {
                context.Status = TransactionStatus.Failed;
                throw;
            }
        }

        #endregion
    }
}
