using System;
using System.Collections;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace HB.Framework.Database
{
    public class DatabaseException : DbException
    {
        private IDictionary _data;

        public DatabaseError Error { get; private set; }

        public int InnerNumber { get; private set; }

        public string InnerSqlState { get; private set; }

        public string EntityName { get; private set; }

        public string Operation { get; private set; }


        public DatabaseException(DatabaseException innerException,
             string entityName, string message, [CallerMemberName]string operation = "")
            : base(message, innerException)
        {
            Error = innerException.Error;
            Operation = operation;
            EntityName = entityName;

            InnerNumber = innerException.InnerNumber;
            InnerSqlState = innerException.InnerSqlState;
        }

        public DatabaseException(DatabaseError error,
             string entityName, string message, Exception innerException = null, [CallerMemberName]string operation = "")
            : base(message, innerException)
        {
            Error = error;
            Operation = operation;
            EntityName = entityName;
        }

        public DatabaseException(int number, string sqlState, string message, Exception innerException = null) : base(message, innerException)
        {
            InnerNumber = number;
            InnerSqlState = sqlState;
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
                _data["InnerNumber"] = InnerNumber;
                _data["InnerSqlState"] = InnerSqlState;
                _data["EntityName"] = EntityName;
                _data["Operation"] = Operation;

                return _data;
            }
        }
    }
}
