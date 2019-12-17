using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Threading.Tasks;
using HB.Framework.Database.Entity;
using HB.Framework.Database.SQL;
using Microsoft.Extensions.Logging;
using HB.Framework.Database.Properties;

namespace HB.Framework.Database
{
    internal partial class DefaultDatabase
    {
        #region 单表查询, Select, From, Where

        public async Task<T> ScalarAsync<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            IEnumerable<T> lst = await RetrieveAsync<T>(selectCondition, fromCondition, whereCondition, transContext).ConfigureAwait(false);

            if (lst.IsNullOrEmpty())
            {
                return null;
            }

            if (lst.Count() > 1)
            {
                string message = $"Scalar retrieve return more than one result. Select:{selectCondition.ToString()}, From:{fromCondition.ToString()}, Where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(DatabaseError.FoundTooMuch, typeof(T).FullName, message);
                //_logger.LogException(exception);

                throw exception;
            }

            return lst.ElementAt(0);
        }

        public async Task<IEnumerable<TSelect>> RetrieveAsync<TSelect, TFrom, TWhere>(SelectExpression<TSelect> selectCondition, FromExpression<TFrom> fromCondition, WhereExpression<TWhere> whereCondition, TransactionContext transContext = null)
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

                reader = await _databaseEngine.ExecuteCommandReaderAsync(transContext?.Transaction, selectDef.DatabaseName, command, transContext != null).ConfigureAwait(false);
                result = _modelMapper.ToList<TSelect>(reader);
            }
            catch (Exception ex)
            {
                string message = $"select:{selectCondition.ToString()}, from:{fromCondition.ToString()}, where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(ex, "RetrieveAsync", selectDef.EntityFullName, message);

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



        public async Task<IEnumerable<T>> RetrieveAsync<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
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

                reader = await _databaseEngine.ExecuteCommandReaderAsync(transContext?.Transaction, entityDef.DatabaseName, command, transContext != null).ConfigureAwait(false);
                result = _modelMapper.ToList<T>(reader);
            }
            catch (Exception ex)
            {
                string message = $"select:{selectCondition.ToString()}, from:{fromCondition.ToString()}, where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(ex, "RetrieveAsync", entityDef.EntityFullName, message);

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

        public Task<IEnumerable<T>> PageAsync<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
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

        public async Task<long> CountAsync<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
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
                object countObj = await _databaseEngine.ExecuteCommandScalarAsync(transContext?.Transaction, entityDef.DatabaseName, command, transContext != null).ConfigureAwait(false);
                count = Convert.ToInt32(countObj, GlobalSettings.Culture);
            }
            catch (Exception ex)
            {
                string message = $"select:{selectCondition.ToString()}, from:{fromCondition.ToString()}, where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(ex, "CountAsync", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }

            return count;
        }

        #endregion

        #region 单表查询, From, Where

        public Task<T> ScalarAsync<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return ScalarAsync(null, fromCondition, whereCondition, transContext);
        }

        public Task<IEnumerable<T>> RetrieveAsync<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return RetrieveAsync(null, fromCondition, whereCondition, transContext);
        }

        public Task<IEnumerable<T>> PageAsync<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return PageAsync(null, fromCondition, whereCondition, pageNumber, perPageCount, transContext);
        }

        public Task<long> CountAsync<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return CountAsync(null, fromCondition, whereCondition, transContext);
        }

        #endregion

        #region 单表查询, Where

        public Task<IEnumerable<T>> RetrieveAllAsync<T>(TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return RetrieveAsync<T>(null, null, null, transContext);
        }

        public Task<T> ScalarAsync<T>(WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return ScalarAsync(null, null, whereCondition, transContext);
        }

        public Task<IEnumerable<T>> RetrieveAsync<T>(WhereExpression<T> whereCondition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return RetrieveAsync(null, null, whereCondition, transContext);
        }

        public Task<IEnumerable<T>> PageAsync<T>(WhereExpression<T> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return PageAsync(null, null, whereCondition, pageNumber, perPageCount, transContext);
        }

        public Task<IEnumerable<T>> PageAsync<T>(long pageNumber, long perPageCount, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return PageAsync<T>(null, null, null, pageNumber, perPageCount, transContext);
        }

        public Task<long> CountAsync<T>(WhereExpression<T> condition, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return CountAsync(null, null, condition, transContext);
        }

        public Task<long> CountAsync<T>(TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return CountAsync<T>(null, null, null, transContext);
        }

        #endregion

        #region 单表查询, Expression Where

        public Task<T> ScalarAsync<T>(long id, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            return ScalarAsync<T>(t => t.Id == id && t.Deleted == false, transContext);
        }

        //public Task<T> RetrieveScalaAsyncr<T>(Expression<Func<T, bool>> whereExpr, DatabaseTransactionContext transContext = false) where T : DatabaseEntity, new();
        public Task<T> ScalarAsync<T>(Expression<Func<T, bool>> whereExpr, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();
            whereCondition.Where(whereExpr);

            return ScalarAsync(null, null, whereCondition, transContext);
        }

        public Task<IEnumerable<T>> RetrieveAsync<T>(Expression<Func<T, bool>> whereExpr, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();
            whereCondition.Where(whereExpr);

            return RetrieveAsync(null, null, whereCondition, transContext);
        }

        public Task<IEnumerable<T>> PageAsync<T>(Expression<Func<T, bool>> whereExpr, long pageNumber, long perPageCount, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();

            return PageAsync(null, null, whereCondition, pageNumber, perPageCount, transContext);
        }

        public Task<long> CountAsync<T>(Expression<Func<T, bool>> whereExpr, TransactionContext transContext)
            where T : DatabaseEntity, new()
        {
            WhereExpression<T> whereCondition = Where<T>();
            whereCondition.Where(whereExpr);

            return CountAsync(null, null, whereCondition, transContext);
        }

        #endregion

        #region 双表查询

        public async Task<IEnumerable<Tuple<TSource, TTarget>>> RetrieveAsync<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
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
                reader = await _databaseEngine.ExecuteCommandReaderAsync(transContext?.Transaction, entityDef.DatabaseName, command, transContext != null).ConfigureAwait(false);
                result = _modelMapper.ToList<TSource, TTarget>(reader);
            }
            catch (Exception ex)
            {
                string message = $"from:{fromCondition.ToString()}, where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(ex, "RetrieveAsync", entityDef.EntityFullName, message);

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

        public Task<IEnumerable<Tuple<TSource, TTarget>>> PageAsync<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
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

        public async Task<Tuple<TSource, TTarget>> ScalarAsync<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget : DatabaseEntity, new()
        {
            IEnumerable<Tuple<TSource, TTarget>> lst = await RetrieveAsync<TSource, TTarget>(fromCondition, whereCondition, transContext).ConfigureAwait(false);

            if (lst.IsNullOrEmpty())
            {
                return null;
            }

            if (lst.Count() > 1)
            {
                string message = $"Scalar retrieve return more than one result. From:{fromCondition.ToString()}, Where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(DatabaseError.FoundTooMuch, typeof(TSource).FullName, message);
                //_logger.LogException(exception);

                throw exception;
            }

            return lst.ElementAt(0);
        }

        #endregion

        #region 三表查询

        public async Task<IEnumerable<Tuple<TSource, TTarget1, TTarget2>>> RetrieveAsync<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
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
                reader = await _databaseEngine.ExecuteCommandReaderAsync(transContext?.Transaction, entityDef.DatabaseName, command, transContext != null).ConfigureAwait(false);
                result = _modelMapper.ToList<TSource, TTarget1, TTarget2>(reader);
            }
            catch (Exception ex)
            {
                string message = $"from:{fromCondition.ToString()}, where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(ex, "RetrieveAsync", entityDef.EntityFullName, message);

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

        public Task<IEnumerable<Tuple<TSource, TTarget1, TTarget2>>> PageAsync<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
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

        public async Task<Tuple<TSource, TTarget1, TTarget2>> ScalarAsync<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget1 : DatabaseEntity, new()
            where TTarget2 : DatabaseEntity, new()
        {
            IEnumerable<Tuple<TSource, TTarget1, TTarget2>> lst = await RetrieveAsync<TSource, TTarget1, TTarget2>(fromCondition, whereCondition, transContext).ConfigureAwait(false);

            if (lst.IsNullOrEmpty())
            {
                return null;
            }

            if (lst.Count() > 1)
            {
                string message = $"Scalar retrieve return more than one result. From:{fromCondition.ToString()}, Where:{whereCondition.ToString()}";
                DatabaseException exception = new DatabaseException(DatabaseError.FoundTooMuch, typeof(TSource).FullName, message);
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
        public async Task AddAsync<T>(T item, TransactionContext transContext) where T : DatabaseEntity, new()
        {
            ThrowIf.NullOrNotValid(item, nameof(item));
                
            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            if (!entityDef.DatabaseWriteable)
            {
                throw new DatabaseException(DatabaseError.NotWriteable, entityDef.EntityFullName, $"Entity:{SerializeUtil.ToJson(item)}");
            }

            IDbCommand dbCommand = null;
            IDataReader reader = null;

            try
            {
                dbCommand = _sqlBuilder.CreateAddCommand(item, "default");

                reader = await _databaseEngine.ExecuteCommandReaderAsync(transContext?.Transaction, entityDef.DatabaseName, dbCommand, true).ConfigureAwait(false);

                _modelMapper.ToObject(reader, item);
            }
            catch (Exception ex)
            {
                string message = $"Item:{SerializeUtil.ToJson(item)}";
                DatabaseException exception = new DatabaseException(ex, "AddAsync", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
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
        public async Task DeleteAsync<T>(T item, TransactionContext transContext) where T : DatabaseEntity, new()
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

                long rows = await _databaseEngine.ExecuteCommandNonQueryAsync(transContext?.Transaction, entityDef.DatabaseName, dbCommand).ConfigureAwait(false);

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
                DatabaseException exception = new DatabaseException(ex, "DeleteAsync", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }
        }

        /// <summary>
        ///  修改，建议每次修改前先select，并放置在一个事务中。
        ///  版本控制，如果item中Version未赋值，会无法更改
        /// </summary>
        public async Task UpdateAsync<T>(T item, TransactionContext transContext) where T : DatabaseEntity, new()
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
                long rows = await _databaseEngine.ExecuteCommandNonQueryAsync(transContext?.Transaction, entityDef.DatabaseName, dbCommand).ConfigureAwait(false);

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
                DatabaseException exception = new DatabaseException(ex, "UpdateAsync", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }
        }

        #endregion

        #region 批量更改(Write)

        public async Task<IEnumerable<long>> BatchAddAsync<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new()
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

            IDbCommand dbCommand = null;
            IDataReader reader = null;

            try
            {
                IList<long> newIds = new List<long>();

                dbCommand = _sqlBuilder.CreateBatchAddStatement(items, "default");
                reader = await _databaseEngine.ExecuteCommandReaderAsync(
                    transContext.Transaction,
                    entityDef.DatabaseName,
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
                    throw new DatabaseException(DatabaseError.NotMatch, entityDef.EntityFullName, $"Items:{SerializeUtil.ToJson(items)}");
                }

                return newIds;
            }
            catch (Exception ex)
            {
                string message = $"Items:{SerializeUtil.ToJson(items)}";
                DatabaseException exception = new DatabaseException(ex, "BatchAddAsync", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
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
        public async Task BatchUpdateAsync<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new()
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

            IDbCommand dbCommand = null;
            IDataReader reader = null;

            try
            {
                dbCommand = _sqlBuilder.CreateBatchUpdateStatement(items, "default");
                reader = await _databaseEngine.ExecuteCommandReaderAsync(
                    transContext.Transaction,
                    entityDef.DatabaseName,
                    dbCommand,
                    true).ConfigureAwait(false);

                int count = 0;

                while (reader.Read())
                {
                    int matched = reader.GetInt32(0);

                    if (matched != 1)
                    {
                        throw new DatabaseException(DatabaseError.NotFound, entityDef.EntityFullName,  $"BatchUpdate wrong, not found the {" + count + "}th data item. Items:{SerializeUtil.ToJson(items)}");
                    }

                    count++;
                }

                if (count != items.Count())
                    throw new DatabaseException(DatabaseError.NotFound, entityDef.EntityFullName, $"BatchUpdate wrong number return. Some data item not found. Items:{SerializeUtil.ToJson(items)}");
            }
            catch (Exception ex)
            {
                string message = $"Items:{SerializeUtil.ToJson(items)}";
                DatabaseException exception = new DatabaseException(ex, "BatchUpdateAsync", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
            }
            finally
            {
                reader?.Dispose();
                dbCommand?.Dispose();
            }
        }

        public async Task BatchDeleteAsync<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new()
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

            IDbCommand dbCommand = null;
            IDataReader reader = null;

            try
            {
                dbCommand = _sqlBuilder.CreateBatchDeleteStatement(items, "default");
                reader = await _databaseEngine.ExecuteCommandReaderAsync(
                    transContext.Transaction,
                    entityDef.DatabaseName,
                    dbCommand,
                    true).ConfigureAwait(false);

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
                DatabaseException exception = new DatabaseException(ex, "BatchDeleteAsync", entityDef.EntityFullName, message);

                //_logger.LogException(exception);

                throw exception;
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

            return new TransactionContext()
            {
                Transaction = dbTransaction,
                Status = TransactionStatus.InTransaction
            };
        }

        public Task<TransactionContext> BeginTransactionAsync<T>(IsolationLevel isolationLevel) where T : DatabaseEntity
        {
            DatabaseEntityDef entityDef = _entityDefFactory.GetDef<T>();

            return BeginTransactionAsync(entityDef.DatabaseName, isolationLevel);
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        public async Task CommitAsync(TransactionContext context)
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
                throw new DatabaseException( DatabaseError.TransactionError, "", Resources.TransactionAlreadyFinishedMessage);
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
        public async Task RollbackAsync(TransactionContext context)
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
                throw new DatabaseException(DatabaseError.TransactionError,  "", Resources.TransactionAlreadyFinishedMessage);
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
