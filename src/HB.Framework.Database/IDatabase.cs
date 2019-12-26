using HB.Framework.Database.Entity;
using HB.Framework.Database.SQL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace HB.Framework.Database
{
    public interface IDatabase
    {
        Task AddAsync<T>(T item, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<long>> BatchAddAsync<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new();
        Task BatchDeleteAsync<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new();
        Task BatchUpdateAsync<T>(IEnumerable<T> items, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<TransactionContext> BeginTransactionAsync(string databaseName, IsolationLevel isolationLevel);
        Task<TransactionContext> BeginTransactionAsync<T>(IsolationLevel isolationLevel) where T : DatabaseEntity;
        Task CommitAsync(TransactionContext context);
        Task<long> CountAsync<T>(Expression<Func<T, bool>> whereExpr, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<long> CountAsync<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<long> CountAsync<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<long> CountAsync<T>(TransactionContext transContext) where T : DatabaseEntity, new();
        Task<long> CountAsync<T>(WhereExpression<T> condition, TransactionContext transContext) where T : DatabaseEntity, new();
        Task DeleteAsync<T>(T item, TransactionContext transContext) where T : DatabaseEntity, new();
        FromExpression<T> From<T>() where T : DatabaseEntity, new();

        Task InitializeAsync(IEnumerable<Migration> migrations = null);

        Task<IEnumerable<T>> PageAsync<T>(Expression<Func<T, bool>> whereExpr, long pageNumber, long perPageCount, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<T>> PageAsync<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<T>> PageAsync<T>(long pageNumber, long perPageCount, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<T>> PageAsync<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<T>> PageAsync<T>(WhereExpression<T> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<Tuple<TSource, TTarget>>> PageAsync<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget : DatabaseEntity, new();
        Task<IEnumerable<Tuple<TSource, TTarget1, TTarget2>>> PageAsync<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, long pageNumber, long perPageCount, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget1 : DatabaseEntity, new()
            where TTarget2 : DatabaseEntity, new();
        Task<IEnumerable<T>> RetrieveAllAsync<T>(TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<T>> RetrieveAsync<T>(Expression<Func<T, bool>> whereExpr, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<T>> RetrieveAsync<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<T>> RetrieveAsync<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<T>> RetrieveAsync<T>(WhereExpression<T> whereCondition, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<IEnumerable<TSelect>> RetrieveAsync<TSelect, TFrom, TWhere>(SelectExpression<TSelect> selectCondition, FromExpression<TFrom> fromCondition, WhereExpression<TWhere> whereCondition, TransactionContext transContext = null)
            where TSelect : DatabaseEntity, new()
            where TFrom : DatabaseEntity, new()
            where TWhere : DatabaseEntity, new();
        Task<IEnumerable<Tuple<TSource, TTarget>>> RetrieveAsync<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget : DatabaseEntity, new();
        Task<IEnumerable<Tuple<TSource, TTarget1, TTarget2>>> RetrieveAsync<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget1 : DatabaseEntity, new()
            where TTarget2 : DatabaseEntity, new();
        Task RollbackAsync(TransactionContext context);
        Task<T> ScalarAsync<T>(Expression<Func<T, bool>> whereExpr, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<T> ScalarAsync<T>(FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<T> ScalarAsync<T>(long id, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<T> ScalarAsync<T>(SelectExpression<T> selectCondition, FromExpression<T> fromCondition, WhereExpression<T> whereCondition, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<T> ScalarAsync<T>(WhereExpression<T> whereCondition, TransactionContext transContext) where T : DatabaseEntity, new();
        Task<Tuple<TSource, TTarget>> ScalarAsync<TSource, TTarget>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget : DatabaseEntity, new();
        Task<Tuple<TSource, TTarget1, TTarget2>> ScalarAsync<TSource, TTarget1, TTarget2>(FromExpression<TSource> fromCondition, WhereExpression<TSource> whereCondition, TransactionContext transContext)
            where TSource : DatabaseEntity, new()
            where TTarget1 : DatabaseEntity, new()
            where TTarget2 : DatabaseEntity, new();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "<Pending>")]
        SelectExpression<T> Select<T>() where T : DatabaseEntity, new();
        Task UpdateAsync<T>(T item, TransactionContext transContext) where T : DatabaseEntity, new();
        WhereExpression<T> Where<T>() where T : DatabaseEntity, new();
    }
}