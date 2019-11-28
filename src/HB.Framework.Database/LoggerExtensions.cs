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

            StringBuilder stringBuilder = new StringBuilder();

            foreach (string key in exception.Data.Keys)
            {
                stringBuilder.Append($"{key}:{exception.Data[key].ToString()}, ");
            }

            if (exception.InnerException != null)
            {
                foreach (string key in exception.InnerException.Data)
                {
                    stringBuilder.Append($"{key}:{exception.InnerException.Data[key].ToString()}, ");
                }
            }

            logger.LogError($"Message:{exception.Message}, InnerMessage:{exception.InnerException?.Message} ## {stringBuilder.ToString()}");
        }

    }
}
