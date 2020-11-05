using HB.Framework.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace HB.Framework.DatabaseTests
{
    public class ServiceFixture
    {
        private readonly IServiceProvider _mySQLServices;

        private readonly IServiceProvider _sQLiteServices;

        public ServiceFixture()
        {
            _mySQLServices = BuildServices("MySQL");
            _sQLiteServices = BuildServices("SQLite");
        }

        private ServiceProvider BuildServices(string databaseType)
        {
            IServiceCollection services = new ServiceCollection();

            services.AddOptions();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddConsole();
                builder.AddDebug();
            });

            if (databaseType.Equals("MySQL", GlobalSettings.ComparisonIgnoreCase))
            {
                services.AddMySQL(options =>
                {
                    options.CommonSettings.Version = 1;

                    options.Connections.Add(new DatabaseConnectionSettings
                    {
                        DatabaseName = "test_db",
                        ConnectionString = "server=127.0.0.1;port=3306;user=admin;password=_admin;database=test_db;SslMode=None;DefaultCommandTimeout=3000;",
                        IsMaster = true
                    });
                });
            }

            if (databaseType.Equals("SQLite", GlobalSettings.ComparisonIgnoreCase))
            {
                services.AddSQLite(options =>
                {
                    options.CommonSettings.Version = 1;

                    options.Connections.Add(new DatabaseConnectionSettings
                    {
                        DatabaseName = "test.db",
                        ConnectionString = "Data Source=test.db",
                        IsMaster = true
                    });
                });
            }
            return services.BuildServiceProvider();
        }

        public IDatabase MySQL => _mySQLServices.GetRequiredService<IDatabase>();
        public IDatabase SQLite => _sQLiteServices.GetRequiredService<IDatabase>();

    }
}
