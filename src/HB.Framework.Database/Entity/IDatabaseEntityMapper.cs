#nullable enable

using System;
using System.Collections.Generic;
using System.Data;

namespace HB.Framework.Database.Entity
{
    internal interface IDatabaseEntityMapper
    {
        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        IList<T> ToList<T>(IDataReader reader) where T : DatabaseEntity, new();

        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        void ToObject<T>(IDataReader reader, T item) where T : DatabaseEntity, new();

        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        IList<Tuple<TSource, TTarget?>> ToList<TSource, TTarget>(IDataReader reader)
            where TSource : DatabaseEntity, new()
            where TTarget : DatabaseEntity, new();

        /// <exception cref="HB.Framework.Database.DatabaseException"></exception>
        IList<Tuple<TSource, TTarget2?, TTarget3?>> ToList<TSource, TTarget2, TTarget3>(IDataReader reader)
            where TSource : DatabaseEntity, new()
            where TTarget2 : DatabaseEntity, new()
            where TTarget3 : DatabaseEntity, new();
    }
}