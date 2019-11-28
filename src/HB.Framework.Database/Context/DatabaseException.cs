using System;
using System.Data.Common;
using System.Runtime.Serialization;

namespace HB.Framework.Database
{
    public class DatabaseException : DbException
    { 
        public DatabaseError Error { get; set; }

        public int InnerNumber { get; set; }

        public string SqlState { get; set; }

        public string EntityName { get; set; }

        public string Operation { get; set; }

        public DatabaseException(DatabaseError error, string operation, string entityName, string message) : base(message)
        {
            Error = error;
            Operation = operation;
            EntityName = entityName;
        }
    }
}
