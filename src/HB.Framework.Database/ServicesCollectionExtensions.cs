using HB.Framework.Database;
using HB.Framework.Database.Entity;
using HB.Framework.Database.SQL;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServicesCollectionExtensions
    {
        public static IServiceCollection AddDatabase(this IServiceCollection services)
        {
            services.AddSingleton<IDatabaseTypeConverterFactory, DatabaseTypeConverterFactory>();
            services.AddSingleton<IDatabaseEntityDefFactory, DefaultDatabaseEntityDefFactory>();
            services.AddSingleton<IDatabaseEntityMapper, DefaultDatabaseEntityMapper>();
            services.AddSingleton<ISQLBuilder, SQLBuilder>();
            services.AddSingleton<IDatabase, DefaultDatabase>();

            return services;
        }
    }
}
