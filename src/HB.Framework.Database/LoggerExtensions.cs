using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace HB.Framework.Database
{
    public static class LoggerExtensions
    {
        public static void LogDatabaseException(this ILogger logger, DatabaseException exception)
        {
            ThrowIf.Null(logger, nameof(logger));
            ThrowIf.Null(exception, nameof(exception));

            logger.LogError($"Database Error: {exception.Error}, ErrorCode:{exception.ErrorCode}, EntityName:{exception.EntityName}, Operation:{exception.Operation}, Message:{exception.Message}");
        }

    }
}
