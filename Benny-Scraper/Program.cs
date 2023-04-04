using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.DataAccess.DbInitializer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Benny_Scraper
{
    internal class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"
        static async Task Main(string[] args)
        {
            Logger.Info("Starting Benny Scraper");
            // Database Injections https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-usage
            using IHost host = Host.CreateDefaultBuilder(args)
               .ConfigureServices((hostContext, services) =>
                   // Services here
                   new StartUp(hostContext.Configuration).ConfigureServices(services)
               ).Build();

            Logger.Info("Initializing Database");
            IDbInitializer dbInitializer = host.Services.GetRequiredService<IDbInitializer>();
            dbInitializer.Initialize();
            Logger.Info("Database Initialized");

            INovelProcessor novelProcessor = host.Services.GetRequiredService<INovelProcessor>();

            // Uri help https://www.dotnetperls.com/uri#:~:text=URI%20stands%20for%20Universal%20Resource,strings%20starting%20with%20%22http.%22
            Uri novelTableOfContentUri = new Uri("https://novelfull.com/paragon-of-sin.html");

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
}