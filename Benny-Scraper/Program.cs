using Autofac;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.DataAccess.DbInitializer;
using Microsoft.Extensions.Configuration;

namespace Benny_Scraper
{
    internal class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static IContainer Container { get; set; }
        // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"
        static async Task Main(string[] args)
        {
            var configuration = BuildConfiguration();
            // Pass the built configuration to the StartUp class
            var startUp = new StartUp(configuration);

            // Database Injections
            var builder = new ContainerBuilder();
            startUp.ConfigureServices(builder);

            Container = builder.Build();

            await RunAsync();
        }

        private static async Task RunAsync()
        {
            using (var scope = Container.BeginLifetimeScope())
            {
                Logger.Info("Initializing Database");
                IDbInitializer dbInitializer = scope.Resolve<IDbInitializer>();
                dbInitializer.Initialize();
                Logger.Info("Database Initialized");

                INovelProcessor novelProcessor = scope.Resolve<INovelProcessor>();

                // Uri help https://www.dotnetperls.com/uri#:~:text=URI%20stands%20for%20Universal%20Resource,strings%20starting%20with%20%22http.%22
                Uri novelTableOfContentUri = new Uri("https://novelfull.com/supremacy-games.html");

                try
                {
                    await novelProcessor.ProcessNovelAsync(novelTableOfContentUri);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Exception when trying to process novel. {ex}");
                }
            }
        }

        private static IConfigurationRoot BuildConfiguration()
        {
            // Build the configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            return configuration;
        }
    }

}