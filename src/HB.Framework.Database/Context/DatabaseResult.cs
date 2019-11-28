using System;
using System.Collections.Generic;
using System.Data.Common;

namespace HB.Framework.Database
{
    public partial class DatabaseResult
    {
        public IList<long> Ids { get; private set; } = new List<long>();

        public DatabaseException Exception { get; private set; }

        /// <summary>
        /// 当Status为Failed时，FailedReason有效
        /// </summary>
        public int FailedReason { get; set; } = 0;

        public DatabaseError Status { get; private set; }

        public bool IsSucceeded => Status == DatabaseError.Succeeded;

        public bool IsNotFound => Status == DatabaseError.NotFound;

        public bool IsNotWriteable => Status == DatabaseError.NotFound;

        private DatabaseResult() { }

        public void AddId(long id)
        {
            Ids.Add(id);
        }

        public static DatabaseResult Create(DatabaseException exception)
        {
            return new DatabaseResult
            {
                Exception = exception,
                Status = exception.Error,
                FailedReason = exception.ErrorCode
            };
        }

        public static DatabaseResult Succeeded()
        {
            return new DatabaseResult { Status = DatabaseError.Succeeded };
        }

        //public static DatabaseResult NotWriteable1(string operation, string entityName, string lastUser)
        //{
        //    return new DatabaseResult
        //    {
        //        Status = DatabaseError.NotWriteable,
        //        Exception = new Exception($"The Database: {databaseName} is not writeable when perform: {operation} on Entity: {entityName}")
        //    };
        //}

        //public static DatabaseResult NotWriteable1(DatabaseException exception)
        //{
        //    return new DatabaseResult { Status = DatabaseError.NotWriteable, Exception = exception };
        //}


        //public static DatabaseResult NotFound1(string operation, string entityName, string lastUser, Exception exception)
        //{
        //    return new DatabaseResult { Status = DatabaseError.NotFound, Exception = exception };
        //}

        //public static DatabaseResult NotFound1(string message)
        //{
        //    return new DatabaseResult { Status = DatabaseError.NotFound, Exception = new Exception(message) };
        //}

        //public static DatabaseResult ArgumentError1(Exception exception)
        //{
        //    throw new NotImplementedException();
        //}

        //internal static DatabaseResult EntityNotValid1(string v)
        //{
        //    throw new NotImplementedException();
        //}

        //public static DatabaseResult Fail1(string operation, string entityName, string lastUser, Exception exception)
        //{
        //    DatabaseResult rt = new DatabaseResult { Status = DatabaseError.Failed, Exception = ex };

        //    if (ex is DbException)
        //    {
        //        rt.FailedReason = ((DbException)ex).ErrorCode;
        //    }

        //    return rt;
        //}

        //internal static DatabaseResult NewIdError1(string operation, string entityName, string lastUser)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
