#nullable enable

using System;
using System.Collections;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace HB.Framework.Database
{
    public class DatabaseException : FrameworkException
    {
        private IDictionary? _data;

        public override FrameworkExceptionType ExceptionType { get => FrameworkExceptionType.Database; }

        public DatabaseError Error { get; private set; }

        public string? EntityName { get; private set; }

        public string? Operation { get; private set; }

        public int DbExceptionNumber { get; private set; }

        public string? DbExceptionSqlState { get; private set; }

        public DatabaseException(Exception innerException, string entityName, string message, [CallerMemberName] string operation = "")
            : this(message, innerException)
        {
            

            Operation = operation;
            EntityName = entityName;

        }

        public DatabaseException(DatabaseError error, string? entityName, string? message = null, Exception? innerException = null, [CallerMemberName] string? operation = "")
            : this(message, innerException)
        {
            Error = error;
            Operation = operation;
            EntityName = entityName;
        }

        public DatabaseException(int dbExceptionNumber, string? dbExceptionSqlState, string? message, Exception? innerException = null) : this(message, innerException)
        {
            DbExceptionNumber = dbExceptionNumber;
            DbExceptionSqlState = dbExceptionSqlState;
            Error = DatabaseError.InnerError;
        }

        public override IDictionary Data
        {
            get
            {
                if (_data is null)
                {
                    _data = base.Data;
                }

                _data["DatabaseError"] = Error.ToString();
                _data["InnerNumber"] = DbExceptionNumber;
                _data["InnerSqlState"] = DbExceptionSqlState;
                _data["EntityName"] = EntityName;
                _data["Operation"] = Operation;

                return _data;
            }
        }

        public DatabaseException()
        {
        }

        public DatabaseException(string? message) : base(message)
        {
        }

        public DatabaseException(string? message, Exception? innerException) : base(message, innerException)
        {
            if (innerException is DatabaseException databaseException)
            {
                Error = databaseException.Error;
                DbExceptionNumber = databaseException.DbExceptionNumber;
                DbExceptionSqlState = databaseException.DbExceptionSqlState;

            }
            else
            {
                Error = DatabaseError.InnerError;
            }
        }
    }
}
