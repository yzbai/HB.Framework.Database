using System;
using System.Collections.Generic;
using System.Text;
using HB.Framework.Database.SQL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HB.Framework.Database.Test
{
    public class ServiceFixture
    {
        public IConfiguration Configuration { get; private set; }

        public IServiceProvider Services { get; private set; }

        public ServiceFixture()
        {
            var configurationBuilder = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("appsettings.json", optional: true);

            Configuration = configurationBuilder.Build();

            IServiceCollection services = new ServiceCollection();

            services.AddOptions();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddConsole();
                builder.AddDebug();
            });

            //Database
            services.AddMySQL(options =>
            {
                options.DatabaseSettings.Version = 1;

                options.Connections.Add(new DatabaseConnectionSettings
                {
                    DatabaseName = "test_db",
                    ConnectionString = "server=127.0.0.1;port=3306;user=admin;password=_admin;database=test_db;SslMode=None;DefaultCommandTimeout=3000;",
                    IsMaster = true
                });
            });

            Services = services.BuildServiceProvider();
        }

        public IDatabase Database => Services.GetRequiredService<IDatabase>();

    }
}
