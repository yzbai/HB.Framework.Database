using System;
using System.Collections;
using System.Data.Common;
using System.Runtime.Serialization;

namespace HB.Framework.Database
{
    public class DatabaseException : DbException
    {
        private IDictionary _data;

        public DatabaseError Error { get; set; }

        public int InnerNumber { get; set; }

        public string InnerSqlState { get; set; }

        public string EntityName { get; set; }

        public string Operation { get; set; }


        public DatabaseException(DatabaseException innerException,
             string operation, string entityName, string message)
            : base(message, innerException)
        {
            Error = innerException.Error;
            Operation = operation;
            EntityName = entityName;

            InnerNumber = innerException.InnerNumber;
            InnerSqlState = innerException.InnerSqlState;
        }

        public DatabaseException(DatabaseError error,
             string operation, string entityName, string message, Exception innerException = null)
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
