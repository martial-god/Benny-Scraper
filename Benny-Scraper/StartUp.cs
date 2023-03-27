using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.DbInitializer;
using Benny_Scraper.DataAccess.Repository;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;
using Benny_Scraper.Services;
using Benny_Scraper.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Benny_Scraper
{
    public class Startup
    {
        private static readonly string _appSettings = "appsettings.json";
        private static readonly string  _connectionType = "DefaultConnection";
        public void ConfigureServices(IServiceCollection services)
        {
            // Add services here for dependency injection
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(GetConnectionString()));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IDbInitializer, DbInitializer>();
            services.AddScoped<INovelService, NovelService>();
            services.AddMemoryCache();

        }

        private static string GetConnectionString()
        {
            // to resolve issue with the methods not being part of the ConfigurationBuilder() 
            //https://stackoverflow.com/questions/57158388/configurationbuilder-does-not-contain-a-definition-for-addjsonfile
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(_appSettings, optional: true, reloadOnChange: true);

            string connectionString = builder.Build().GetConnectionString(_connectionType);
            return connectionString;
        }

        private static void ConfigureLogger()
        {
            var builder = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddTransient<Program>();

                })
                .ConfigureLogging(logBuilder =>
                {
                    logBuilder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    logBuilder.AddLog4Net("log4net.config");

                }).UseConsoleLifetime();
        }

        static void ExemplifyServiceLifetime(IServiceProvider hostProvider, string lifetime)
        {
            using IServiceScope serviceScope = hostProvider.CreateScope();

            IServiceProvider provider = serviceScope.ServiceProvider;
            IUnitOfWork unitOfWork = provider.GetRequiredService<IUnitOfWork>();
            IDbInitializer dbInitializer = provider.GetRequiredService<IDbInitializer>();
            dbInitializer.Initialize();
            INovelService startUp1 = provider.GetRequiredService<INovelService>();

            //startUp.ReportServiceLifetimeDetails(
            //    $"{lifetime}: Call 1 to provider.GetRequiredService<ServiceLifetimeLogger>()");

            Console.WriteLine("...");
            var novel = new Novel
            {
                Title = "Test2",
                Url = @"https://novelfull.com",
                DateCreated = DateTime.Now,
                SiteName = "novelfull",
            };

            INovelService startUp = new NovelService(unitOfWork);
            startUp1.CreateAsync(novel);
        }
    }
}
