using HB.Framework.Database.SQL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using HB.Framework.Database.Entity;
using HB.Framework.Database.Engine;
using System.Data.Common;
using Microsoft.Extensions.Logging;

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
    internal partial class DefaultDatabase : IDatabase
    {
        private static readonly object _lockerObj = new object();

        private bool _initialized = false;

        private readonly DatabaseSettings _databaseSettings;
        private readonly IDatabaseEngine _databaseEngine;
        private readonly IDatabaseEntityDefFactory _entityDefFactory;
        private readonly IDatabaseEntityMapper _modelMapper;
        private readonly ISQLBuilder _sqlBuilder;
        //private readonly ILogger _logger;

        //public IDatabaseEngine DatabaseEngine { get { return _databaseEngine; } }

        public DefaultDatabase(
            IDatabaseEngine databaseEngine, 
            IDatabaseEntityDefFactory modelDefFactory, 
            IDatabaseEntityMapper modelMapper, 
            ISQLBuilder sqlBuilder/*, ILogger<DefaultDatabase> logger*/)
        {
            _databaseSettings = databaseEngine.DatabaseSettings;
            _databaseEngine = databaseEngine;
            _entityDefFactory = modelDefFactory;
            _modelMapper = modelMapper;
            _sqlBuilder = sqlBuilder;
            //_logger = logger;

            if (_databaseSettings.Version < 0)
            {
                throw new ArgumentException("Database Version should greater than 0");
            }
        }

        #region Initialize

        public void Initialize(IList<Migration> migrations = null)
        {
            if (!_initialized)
            {
                lock (_lockerObj)
                {
                    if (!_initialized)
                    {
                        _initialized = true;

                        if (_databaseSettings.AutomaticCreateTable)
                        {
                            AutoCreateTablesIfBrandNew();
                        }

                        Migarate(migrations);
                    }
                }
            }
        }

        private void AutoCreateTablesIfBrandNew()
        {
            _databaseEngine.GetDatabaseNames().ForEach(databaseName => {

                TransactionContext transactionContext = BeginTransaction(databaseName, IsolationLevel.Serializable);

                try
                {
                    SystemInfo sys = _databaseEngine.GetSystemInfo(databaseName, transactionContext.Transaction);
                    //表明是新数据库
                    if (sys.Version == 0)
                    {
                        if (_databaseSettings.Version != 1)
                        {
                            Rollback(transactionContext);
                            throw new DatabaseException(DatabaseError.TableCreateError, "", $"Database:{databaseName} does not exists, database Version must be 1");
                        }

                        CreateTablesByDatabase(databaseName, transactionContext);

                        _databaseEngine.UpdateSystemVersion(databaseName, 1, transactionContext.Transaction);
                    }

                    Commit(transactionContext);
                }
                catch (Exception ex)
                {
                    Rollback(transactionContext);
                    throw new DatabaseException(DatabaseError.TableCreateError, "", $"Auto Create Table Failed, Database:{databaseName}, Reason:{ex.Message}", ex);
                }

            });
        }

        private int CreateTable(DatabaseEntityDef def, TransactionContext transContext)
        {
            string sql = GetTableCreateStatement(def.EntityType, false);

            //_logger.LogInformation($"Entity Table {def.TableName} going to create. SQL : {sql}");

            return _databaseEngine.ExecuteSqlNonQuery(transContext.Transaction, def.DatabaseName, sql);
        }

        private void CreateTablesByDatabase(string databaseName, TransactionContext transactionContext)
        {
            _entityDefFactory
                .GetAllDefsByDatabase(databaseName)
                .ForEach(def => CreateTable(def, transactionContext));
        }

        private void Migarate(IList<Migration> migrations)
        {
            if (migrations != null && migrations.Any(m => m.NewVersion <= m.OldVersion))
            {
                throw new DatabaseException(DatabaseError.MigrateError,"", $"oldVersion should always lower than newVersions in Database Migrations");
            }

            _databaseEngine.GetDatabaseNames().ForEach(databaseName => {

                TransactionContext transactionContext = BeginTransaction(databaseName, IsolationLevel.Serializable);

                try
                {
                    SystemInfo sys = _databaseEngine.GetSystemInfo(databaseName, transactionContext.Transaction);

                    if (sys.Version < _databaseSettings.Version)
                    {
                        if (migrations == null)
                        {
                            throw new DatabaseException(DatabaseError.MigrateError,  "", $"Lack Migrations for {sys.DatabaseName}");
                        }

                        IOrderedEnumerable<Migration> curOrderedMigrations = migrations
                            .Where(m => m.TargetSchema.Equals(sys.DatabaseName, GlobalSettings.ComparisonIgnoreCase))
                            .OrderBy(m => m.OldVersion);

                        if (curOrderedMigrations == null)
                        {
                            throw new DatabaseException(DatabaseError.MigrateError, "", $"Lack Migrations for {sys.DatabaseName}");
                        }

                        if (!CheckMigration(sys.Version, _databaseSettings.Version, curOrderedMigrations))
                        {
                            throw new DatabaseException(DatabaseError.MigrateError,  "", $"Can not perform Migration on ${sys.DatabaseName}, because the migrations provided is not sufficient.");
                        }

                        curOrderedMigrations.ForEach(migration => _databaseEngine.ExecuteSqlNonQuery(transactionContext.Transaction, databaseName, migration.SqlStatement));

                        _databaseEngine.UpdateSystemVersion(sys.DatabaseName, _databaseSettings.Version, transactionContext.Transaction);
                    }

                    Commit(transactionContext);
                }
                catch (Exception ex)
                {
                    Rollback(transactionContext);
                    throw new DatabaseException(DatabaseError.MigrateError, "", $"Migration Failed at Database:{databaseName}", ex);
                }
            });
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

        #region 单表查询, Select, From, Where

        public IList<TSelect> Retrieve<TSelect, TFrom, TWhere>(SelectExpression<TSelect> selectCondition, FromExpression<TFrom> fromCondition, WhereExpression<TWhere> whereCondition, TransactionContext transContext = null)
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

            IList<TSelect> result = null;
            IDbCommand command = null;
            IDataReader reader = null;
            DatabaseEntityDef selectDef = _entityDefFactory.GetDef<TSelect>();

            try
            {
                command = _sqlBuilder.CreateRetrieveCommand(selectCondition, fromCondition, whereCondition);
                reader = _databaseEngine.ExecuteCommandReader(transContext?.Transaction, selectDef.DatabaseName, command, transContext != null);
                result = _modelMapper.ToList<TSelect>(reader);
            }
            catch (Exception ex)
            {
                string message = $"select:{selectCondition.ToString()}, from:{fromCondition.ToString()}, where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(ex, "Retrieve", selectDef.EntityFullName, message);

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

        public T Scalar<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            IList<T> lst = Retrieve<T>(selectCondition, fromCondition, whereCondition, transContext);

            if (lst.IsNullOrEmpty())
            {
                return null;
            }

            if (lst.Count > 1)
            {
                string message = $"Scalar retrieve return more than one result. Select:{selectCondition.ToString()}, From:{fromCondition.ToString()}, Where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(DatabaseError.FoundTooMuch, typeof(T).FullName, message);
                //_logger.LogException(exception);

                throw exception;
            }

            return lst[0];
        }

        public IList<T> Retrieve<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
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

            IList<T> result = null;
            IDbCommand command = null;
            IDataReader reader = null;
            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            try
            {
                command = _sqlBuilder.CreateRetrieveCommand<T>(selectCondition, fromCondition, whereCondition);
                reader = _databaseEngine.ExecuteCommandReader(transContext?.Transaction, entityDef.DatabaseName, command, transContext != null);
                result = _modelMapper.ToList<T>(reader);
            }
            catch (Exception ex)
            {
                string message = $"select:{selectCondition.ToString()}, from:{fromCondition.ToString()}, where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(ex, "Retrieve", entityDef.EntityFullName, message);

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

        public IList<T> Page<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
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

            return Retrieve<T>(selectCondition, fromCondition, whereCondition, transContext);
        }

        public long Count<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
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
                object countObj = _databaseEngine.ExecuteCommandScalar(transContext?.Transaction, entityDef.DatabaseName, command, transContext != null);
                count = Convert.ToInt32(countObj, GlobalSettings.Culture);
            }
            catch (Exception ex)
            {
                string message = $"select:{selectCondition.ToString()}, from:{fromCondition.ToString()}, where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(ex, "Count", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }

            return count;
        }

        #endregion

        #region 单表查询, From, Where

        public T Scalar<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Scalar(null, fromCondition, whereCondition, transContext);
        }

        public IList<T> Retrieve<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Retrieve(null, fromCondition, whereCondition, transContext);
        }

        public IList<T> Page<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Page(null, fromCondition, whereCondition, pageNumber, perPageCount, transContext);
        }

        public long Count<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Count(null, fromCondition, whereCondition, transContext);
        }

        #endregion

        #region 单表查询, Where

        public IList<T> RetrieveAll<T>(TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Retrieve<T>(null, null, null, transContext);
        }

        public T Scalar<T>(WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Scalar(null, null, whereCondition, transContext);
        }

        public IList<T> Retrieve<T>(WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Retrieve(null, null, whereCondition, transContext);
        }

        public IList<T> Page<T>(WhereExpression<T> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Page(null, null, whereCondition, pageNumber, perPageCount, transContext);
        }

        public IList<T> Page<T>(long pageNumber, long perPageCount, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Page<T>(null, null, null, pageNumber, perPageCount, transContext);
        }

        public long Count<T>(WhereExpression<T> condition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Count(null, null, condition, transContext);
        }

        public long Count<T>(TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Count<T>(null, null, null, transContext);
        }

        #endregion

        #region 单表查询, Expression Where

        public T Scalar<T>(long id, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return Scalar<T>(t => t.Id == id && t.Deleted == false, transContext);
        }

        public T Scalar<T>(Expression<Func<T, bool>> whereExpr, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();
            whereCondition.Where(whereExpr);

            return Scalar(null, null, whereCondition, transContext);
        }

        public IList<T> Retrieve<T>(Expression<Func<T, bool>> whereExpr, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();
            whereCondition.Where(whereExpr);

            return Retrieve(null, null, whereCondition, transContext);
        }

        public IList<T> Page<T>(Expression<Func<T, bool>> whereExpr, long pageNumber, long perPageCount, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>().Where(whereExpr);

            return Page(null, null, whereCondition, pageNumber, perPageCount, transContext);
        }

        public long Count<T>(Expression<Func<T, bool>> whereExpr, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();
            whereCondition.Where(whereExpr);

            return Count(null, null, whereCondition, transContext);
        }

        #endregion

        #region 双表查询

        public IList<Tuple<TSource, TTarget>> Retrieve<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
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

            IList<Tuple<TSource, TTarget>> result = null;
            IDbCommand command = null;
            IDataReader reader = null;
            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<TSource>();

            try
            {
                command = _sqlBuilder.CreateRetrieveCommand<TSource, TTarget>(fromCondition, whereCondition);
                reader = _databaseEngine.ExecuteCommandReader(transContext?.Transaction, entityDef.DatabaseName, command, transContext != null);
                result = _modelMapper.ToList<TSource, TTarget>(reader);
            }
            catch (Exception ex)
            {
                string message = $"from:{fromCondition.ToString()}, where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(ex, "Retrieve", entityDef.EntityFullName, message);

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

        public IList<Tuple<TSource, TTarget>> Page<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget : DatabaseEntity, new()
        {
            if (whereCondition == null)
            {
                whereCondition = Where<TSource>();
            }

            whereCondition.Limit((pageNumber - 1) * perPageCount, perPageCount);

            return Retrieve<TSource, TTarget>(fromCondition, whereCondition, transContext);
        }

        public Tuple<TSource, TTarget> Scalar<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget : DatabaseEntity, new()
        {
            IList<Tuple<TSource, TTarget>> lst = Retrieve<TSource, TTarget>(fromCondition, whereCondition, transContext);

            if (lst.IsNullOrEmpty())
            {
                return null;
            }

            if (lst.Count > 1)
            {
                string message = $"Scalar retrieve return more than one result. From:{fromCondition.ToString()}, Where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(DatabaseError.FoundTooMuch, typeof(TSource).FullName, message);
                //_logger.LogException(exception);

                throw exception;
            }

            return lst[0];
        }

        #endregion

        #region 三表查询

        public IList<Tuple<TSource, TTarget1, TTarget2>> Retrieve<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
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


            IList<Tuple<TSource, TTarget1, TTarget2>> result = null;
            IDbCommand command = null;
            IDataReader reader = null;
            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<TSource>();

            try
            {
                command = _sqlBuilder.CreateRetrieveCommand<TSource, TTarget1, TTarget2>(fromCondition, whereCondition);
                reader = _databaseEngine.ExecuteCommandReader(transContext?.Transaction, entityDef.DatabaseName, command, transContext != null);
                result = _modelMapper.ToList<TSource, TTarget1, TTarget2>(reader);
            }
            catch (Exception ex)
            {
                string message = $"from:{fromCondition.ToString()}, where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(ex, "Retrieve", entityDef.EntityFullName, message);

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

        public IList<Tuple<TSource, TTarget1, TTarget2>> Page<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget1 : DatabaseEntity, new()
            where TTarget2 : DatabaseEntity, new()
        {
            if (whereCondition == null)
            {
                whereCondition = Where<TSource>();
            }

            whereCondition.Limit((pageNumber - 1) * perPageCount, perPageCount);

            return Retrieve<TSource, TTarget1, TTarget2>(fromCondition, whereCondition, transContext);
        }

        public Tuple<TSource, TTarget1, TTarget2> Scalar<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget1 : DatabaseEntity, new()
            where TTarget2 : DatabaseEntity, new()
        {
            IList<Tuple<TSource, TTarget1, TTarget2>> lst = Retrieve<TSource, TTarget1, TTarget2>(fromCondition, whereCondition, transContext);

            if (lst.IsNullOrEmpty())
            {
                return null;
            }

            if (lst.Count > 1)
            {
                string message = $"Scalar retrieve return more than one result. From:{fromCondition.ToString()}, Where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(DatabaseError.FoundTooMuch, typeof(TSource).FullName, message);
                //_logger.LogException(exception);

                throw exception;
            }

            return lst[0];
        }

        #endregion

        #region 单体更改(Write)

        /// <summary>
        /// 增加,并且item被重新赋值
        /// </summary>
        public void Add<T>(T item, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.NullOrNotValid(item, nameof(item));

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(DatabaseError.NotWriteable, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
            }

            IDbCommand command = null;
            IDataReader reader = null;

            try
            {
                command = _sqlBuilder.CreateAddCommand(item, "default");

                reader = _databaseEngine.ExecuteCommandReader(transContext?.Transaction, entityDef.DatabaseName, command, true);

                _modelMapper.ToObject(reader, item);

            }
            catch (Exception ex)
            {
                string message = $"Item:{SerializeUtil.ToJson(item)}";
                DatabaseException exception = new DatabaseException(ex, "Add", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }
            finally
            {
                reader?.Dispose();
                command?.Dispose();
            }
        }

        /// <summary>
        /// 删除, Version控制
        /// </summary>
        public void Delete<T>(T item, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.NullOrNotValid(item, nameof(item));

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(DatabaseError.NotWriteable, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
            }

            long id = item.Id;
            long version = item.Version;
            WhereExpression<T> condition = Where<T>().Where(t => t.Id == id && t.Deleted == false && t.Version == version);

            try
            {
                IDbCommand dbCommand = _sqlBuilder.CreateDeleteCommand(condition, "default");

                long rows = _databaseEngine.ExecuteCommandNonQuery(transContext?.Transaction, entityDef.DatabaseName, dbCommand);

                if (rows == 1)
                {
                    return;
                }
                else if (rows == 0)
                {
                    throw new DatabaseException(DatabaseError.NotFound, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
                }

                throw new DatabaseException(DatabaseError.FoundTooMuch, entityDef.EntityFullName, $"Multiple Rows Affected instead of one. Something go wrong. Entity:{SerializeUtil.ToJson(item)}");
            }
            catch (Exception ex)
            {
                string message = $"Item:{SerializeUtil.ToJson(item)}";
                DatabaseException exception = new DatabaseException(ex, entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }
        }

        /// <summary>
        ///  修改，建议每次修改前先select，并放置在一个事务中。
        ///  版本控制，如果item中Version未赋值，会无法更改
        /// </summary>
        public void Update<T>(T item, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.NullOrNotValid(item, nameof(item));

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(DatabaseError.NotWriteable, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
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

                long rows = _databaseEngine.ExecuteCommandNonQuery(transContext?.Transaction, entityDef.DatabaseName, dbCommand);

                if (rows == 1)
                {
                    item.Version++;
                    return;
                }
                else if (rows == 0)
                {
                    throw new DatabaseException(DatabaseError.NotFound, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
                }

                throw new DatabaseException(DatabaseError.FoundTooMuch, entityDef.EntityFullName, $"Multiple Rows Affected instead of one. Something go wrong. Entity:{SerializeUtil.ToJson(item)}");
            }
            catch (Exception ex)
            {
                string message = $"Item:{SerializeUtil.ToJson(item)}";
                DatabaseException exception = new DatabaseException(ex, "Update", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }
        }

        #endregion

        #region 批量更改(Write)


        /// <summary>
        /// 批量添加,返回新产生的ID列表
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public IEnumerable<long> BatchAdd<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.Null(transContext, nameof(transContext));
            ThrowIf.NullOrNotValid(items, nameof(items));

            if (!items.Any())
            {
                return null;
            }

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(DatabaseError.NotWriteable, entityDef.EntityFullName, $"Items:{SerializeUtil.ToJson(items)}");
            }

            IDbCommand command = null;
            IDataReader reader = null;

            try
            {
                IList<long> newIds = new List<long>();

                command = _sqlBuilder.CreateBatchAddStatement(items, "default");

                reader = _databaseEngine.ExecuteCommandReader(
                    transContext.Transaction,
                    entityDef.DatabaseName,
                    command,
                    true);

                while (reader.Read())
                {
                    //int newId = reader.GetInt32(0);

                    //if (newId <= 0)
                    //{
                    //    throw new DatabaseException("BatchAdd wrong new id return.");
                    //}

                    newIds.Add(reader.GetInt64(0));
                }

                if (newIds.Count != items.Count())
                {
                    throw new DatabaseException(DatabaseError.NotMatch, entityDef.EntityFullName, $"Items:{SerializeUtil.ToJson(items)}");
                }

                return newIds;
            }
            catch (Exception ex)
            {
                string message = $"Items:{SerializeUtil.ToJson(items)}";
                DatabaseException exception = new DatabaseException(ex, "BatchAdd", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }
            finally
            {
                reader?.Dispose();
                command?.Dispose();
            }
        }

        /// <summary>
        /// 批量更改
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public void BatchUpdate<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.Null(transContext, nameof(transContext));
            ThrowIf.NullOrNotValid(items, nameof(items));

            if (!items.Any())
            {
                return;
            }

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(DatabaseError.NotWriteable, entityDef.EntityFullName, $"Items:{SerializeUtil.ToJson(items)}");
            }

            IDbCommand command = null;
            IDataReader reader = null;

            try
            {
                command = _sqlBuilder.CreateBatchUpdateStatement(items, "default");

                reader = _databaseEngine.ExecuteCommandReader(
                    transContext.Transaction,
                    entityDef.DatabaseName,
                    command,
                    true);

                int count = 0;

                while (reader.Read())
                {
                    int matched = reader.GetInt32(0);

                    if (matched != 1)
                    {
                        throw new DatabaseException(DatabaseError.NotFound, entityDef.EntityFullName, $"BatchUpdate wrong, not found the {" + count + "}th data item. Items:{SerializeUtil.ToJson(items)}");
                    }

                    count++;
                }

                if (count != items.Count())
                    throw new DatabaseException(DatabaseError.NotFound, entityDef.EntityFullName, $"BatchUpdate wrong number return. Some data item not found. Items:{SerializeUtil.ToJson(items)}");
            }
            catch (Exception ex)
            {
                string message = $"Items:{SerializeUtil.ToJson(items)}";
                DatabaseException exception = new DatabaseException(ex, "BatchUpdate", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }
            finally
            {
                reader?.Dispose();
                command?.Dispose();
            }
        }

        public void BatchDelete<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.Null(transContext, nameof(transContext));
            ThrowIf.NullOrNotValid(items, nameof(items));

            if (!items.Any())
            {
                return;
            }

            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(DatabaseError.NotWriteable, entityDef.EntityFullName, $"Items:{SerializeUtil.ToJson(items)}");
            }

            IDbCommand command = null;
            IDataReader reader = null;

            try
            {
                command = _sqlBuilder.CreateBatchDeleteStatement(items, "default");

                reader = _databaseEngine.ExecuteCommandReader(
                    transContext.Transaction,
                    entityDef.DatabaseName,
                    command,
                    true);

                int count = 0;

                while (reader.Read())
                {
                    int affected = reader.GetInt32(0);

                    if (affected != 1)
                    {
                        throw new DatabaseException(DatabaseError.NotFound, entityDef.EntityFullName, $"BatchDelete wrong, not found the {" + count + "}th data item. Items:{SerializeUtil.ToJson(items)}");
                    }

                    count++;
                }

                if (count != items.Count())
                {
                    throw new DatabaseException(DatabaseError.NotFound, entityDef.EntityFullName, $"BatchDelete wrong number return. Some data item not found. Items:{SerializeUtil.ToJson(items)}");
                }
            }
            catch (Exception ex)
            {
                string message = $"Items:{SerializeUtil.ToJson(items)}";
                DatabaseException exception = new DatabaseException(ex, "BatchDelete", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }
            finally
            {
                reader?.Dispose();
                command?.Dispose();
            }
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

        public string GetTableCreateStatement(Type type, bool addDropStatement)
        {
            return _sqlBuilder.GetTableCreateStatement(type, addDropStatement);
        }

        #endregion

        #region 事务

        public TransactionContext BeginTransaction(string databaseName, IsolationLevel isolationLevel)
        {
            IDbTransaction dbTransaction = _databaseEngine.BeginTransaction(databaseName, isolationLevel);

            return new TransactionContext() {
                Transaction = dbTransaction,
                Status = TransactionStatus.InTransaction
            };
        }

        public TransactionContext BeginTransaction<T>(IsolationLevel isolationLevel) where T : DatabaseEntity
        {
            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            return BeginTransaction(entityDef.DatabaseName, isolationLevel);
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        public void Commit(TransactionContext context)
        {
            if (context == null || context.Transaction == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Status == TransactionStatus.Commited)
            {
                return;
            }

            if (context.Status != TransactionStatus.InTransaction)
            {
                throw new DatabaseException(DatabaseError.TransactionError, "", "use a already finished transactioncontenxt");
            }

            try
            {
                IDbConnection conn = context.Transaction.Connection;
                _databaseEngine.Commit(context.Transaction);
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
        public void Rollback(TransactionContext context)
        {
            if (context == null || context.Transaction == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Status == TransactionStatus.Rollbacked)
            {
                return;
            }

            if (context.Status != TransactionStatus.InTransaction)
            {
                throw new DatabaseException(DatabaseError.TransactionError, "", "use a already finished transactioncontenxt");
            }

            try
            {
                IDbConnection conn = context.Transaction.Connection;

                _databaseEngine.Rollback(context.Transaction);

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
