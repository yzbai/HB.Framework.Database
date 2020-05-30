#nullable enable

using System;
using System.Collections;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace HB.Framework.Database
{
    public class DatabaseException : FrameworkException
    {
        public override FrameworkExceptionType ExceptionType { get => FrameworkExceptionType.Database; }

        //public DatabaseError Error { get; private set; }

        //public string? EntityName { get; private set; }

        //public string? Caller { get; private set; }

        //public string? Detail { get; set; }

        public DatabaseException(DatabaseError whatError, string? whereCaller, string? whoEntityName = null, string? detail = null, Exception? innerException = null)
            : this($"Database error:{whatError} at caller:{whereCaller}, entityName:{whoEntityName}, detail:{detail}", innerException)
        {
            //Error = whatError;
            //Caller = whereCaller;
            //EntityName = whoEntityName;
            //Detail = detail;
        }



        public DatabaseException()
        {
        }

        public DatabaseException(string? message) : base(message)
        {
        }

        public DatabaseException(string? message, Exception? innerException) : base(message, innerException)
        {
            //Error = DatabaseError.InnerError;
        }
    }
}
