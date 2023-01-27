using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.DbInitializer;
using Benny_Scraper.DataAccess.Repository;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using log4net;
using log4net.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

//[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]


namespace Benny_Scraper
{
    internal class Program
    {
        //private static readonly ILog logger = LogManager.GetLogger(typeof(Program));
        //private const string _connectionString = "Server=localhost;Database=Test;TrustServerCertificate=True;Trusted_Connection=True;";
        // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"
        private readonly INovelService _startUpService;

        // Constructor Injection
        public Program(INovelService startUpService)
        {
            _startUpService = startUpService;
        }

        static async Task Main(string[] args)
        {
            // Database Injections https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-usage
            using IHost host = Host.CreateDefaultBuilder(args)
               .ConfigureServices(services =>
               {
                   // Services here
                   new Startup().ConfigureServices(services);
               }).Build();

            // run database initializer
            IDbInitializer dbInitializer = host.Services.GetRequiredService<IDbInitializer>();
            dbInitializer.Initialize();

            INovelService novelService = host.Services.GetRequiredService<INovelService>();
            

            var novelTableOfContentUrl = "https://novelfull.com/paragon-of-sin.html";
            var novelContext = await novelService.GetByUrlAsync(novelTableOfContentUrl);

            if (novelContext == null) // Novel is not in database so add it
            {
                IDriverFactory driverFactory = new DriverFactory(); // Instantiating an interface https://softwareengineering.stackexchange.com/questions/167808/instantiating-interfaces-in-c
                Task<IWebDriver> driver = driverFactory.CreateDriverAsync(1, true, "https://google.com");
                NovelPage novelPage = new NovelPage(driver.Result);
                Novel novel = await novelPage.BuildNovelAsync(novelTableOfContentUrl);
                await novelService.CreateAsync(novel);
                driverFactory.DisposeAllDrivers();
            }
            else // make changes or update novel and chapters
            {
                var currentChapter = novelContext?.CurrentChapter;
                var chapterContext = novelContext?.Chapters;

                if (currentChapter == null || chapterContext == null)
                    return;

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(novelTableOfContentUrl);
                    response.EnsureSuccessStatusCode();
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var htmlDocument = new HtmlDocument();
                    htmlDocument.LoadHtml(responseBody);

                    var titleElement = htmlDocument.DocumentNode.SelectSingleNode("//h3[@class='title']");
                    var title = titleElement.InnerText;
                    var chapterList = htmlDocument.DocumentNode.Descendants("div")
                        .Where(node => node.GetAttributeValue("class", "list-chapter")
                        .Equals("row")).ToList();
                }
                

            }
        }        
        
        private static void ConfigureLogger() {
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