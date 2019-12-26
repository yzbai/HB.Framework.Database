using HB.Framework.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using XamarinSample.Services;
using XamarinSample.Views;

namespace XamarinSample
{
    public partial class App : Application
    {
        public static IDatabase Database { get; private set; }

        public App()
        {
            InitializeComponent();

            DependencyService.Register<MockDataStore>();

            Database = GetDatabase();

            MainPage = new AppShell();
        }

        private IDatabase GetDatabase()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddOptions();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddDebug();
            });

            services.AddSQLite(sqliteOptions => {
                string dbFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "test.db");

                sqliteOptions.DatabaseSettings.Version = 1;

                sqliteOptions.Connections.Add(new DatabaseConnectionSettings
                {
                    DatabaseName = "test.db",
                    IsMaster = true,
                    ConnectionString = $"Data Source={dbFile}"
                });
            });

            IServiceProvider serviceProvider = services.BuildServiceProvider();

            IDatabase database = serviceProvider.GetRequiredService<IDatabase>();

            database.InitializeAsync();

            return database;
        }
        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
