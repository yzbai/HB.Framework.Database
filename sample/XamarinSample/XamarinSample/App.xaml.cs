using System;
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

            services.AddSQLite(sqliteOptions => {
                string dbFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "test.db");

                sqliteOptions.DatabaseSettings.Version = 1;

                sqliteOptions.Schemas.Add(new SchemaInfo
                {
                    SchemaName = "test.db",
                    IsMaster = true,
                    ConnectionString = $"Data Source={dbFile}"
                });
            });

            IServiceProvider serviceProvider = services.BuildServiceProvider();

            IDatabase database = serviceProvider.GetRequiredService<IDatabase>();

            database.Initialize();

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
