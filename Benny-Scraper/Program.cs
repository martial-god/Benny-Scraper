using Benny_Scraper.DataAccess.DbInitializer;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Interfaces;
using Benny_Scraper.Models;
using Benny_Scraper.Services;
using Benny_Scraper.Services.Interface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;

//[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]


namespace Benny_Scraper
{
    internal class Program
    {
        //private static readonly ILog logger = LogManager.GetLogger(typeof(Program));
        //private const string _connectionString = "Server=localhost;Database=Test;TrustServerCertificate=True;Trusted_Connection=True;";

        // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"
        static async Task Main(string[] args)
        {
            // Database Injections https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-usage
            using IHost host = Host.CreateDefaultBuilder(args)
               .ConfigureServices(services =>               
                   // Services here
                   new Startup().ConfigureServices(services)
               ).Build();

            // run database initializer
            IDbInitializer dbInitializer = host.Services.GetRequiredService<IDbInitializer>();
            dbInitializer.Initialize();

            INovelService novelService = host.Services.GetRequiredService<INovelService>();

            // Uri help https://www.dotnetperls.com/uri#:~:text=URI%20stands%20for%20Universal%20Resource,strings%20starting%20with%20%22http.%22
            Uri novelTableOfContentUri = new Uri("https://novelfull.com/paragon-of-sin.html");
            Novel novel = await novelService.GetByUrlAsync(novelTableOfContentUri);

            if (novel == null) // Novel is not in database so add it
            {
                IDriverFactory driverFactory = new DriverFactory(); // Instantiating an interface https://softwareengineering.stackexchange.com/questions/167808/instantiating-interfaces-in-c
                Task<IWebDriver> driver = driverFactory.CreateDriverAsync(1, true, "https://google.com");
                NovelPage novelPage = new NovelPage(driver.Result);
                Novel novelToAdd = await novelPage.BuildNovelAsync(novelTableOfContentUri);
                await novelService.CreateAsync(novelToAdd);
                driverFactory.DisposeAllDrivers();
            }
            else // make changes or update novelToAdd and newChapters
            {
                var currentChapter = novel?.CurrentChapter;
                var chapterContext = novel?.Chapters;

                if (currentChapter == null || chapterContext == null)
                    return;


                INovelPageScraper novelPageScraper = new NovelPageScraper();
                var latestChapter = await novelPageScraper.GetLatestChapterAsync("//ul[@class='l-chapters']//a", novelTableOfContentUri);
                bool isCurrentChapterNewest = string.Equals(currentChapter, latestChapter, comparisonType: StringComparison.OrdinalIgnoreCase);

                if (isCurrentChapterNewest)
                {
                    Logger.Log.Info($"{novel.Title} is currently at the latest chapter.\nCurrent Saved: {novel.CurrentChapter}");
                    return;
                }


                // get all newChapters after the current chapter up to the latest
                if (string.IsNullOrEmpty(novel.LastTableOfContentsUrl))
                {
                    Logger.Log.Info($"{novel.Title} does not have a LastTableOfContentsUrl.\nCurrent Saved: {novel.LastTableOfContentsUrl}");
                    return;
                }

                Uri lastTableOfContentsUrl = new Uri(novel.LastTableOfContentsUrl);
                var latestChapterData = await novelPageScraper.GetChaptersFromCheckPointAsync("//ul[@class='list-chapter']//a/@href", lastTableOfContentsUrl, novel.CurrentChapter);
                IEnumerable<ChapterData> chapterData = await novelPageScraper.GetChaptersDataAsync(latestChapterData.LatestChapterUrls, "//span[@class='chapter-text']", "//div[@id='chapter']", novel.Title);

                List<Chapter> newChapters = chapterData.Select(data => new Chapter
                {
                    Url = data.Url ?? "",
                    Content = data.Content ?? "",
                    Title = data.Title ?? "",
                    DateCreated = DateTime.UtcNow,
                    DateLastModified = DateTime.UtcNow,
                    Number = data.Number,

                }).ToList();
                novel.LastTableOfContentsUrl = latestChapterData.LastTableOfContentsUrl;
                novel.Status = latestChapterData.Status;

                novel.Chapters.AddRange(newChapters);
                await novelService.UpdateAndAddChapters(novel, newChapters);
            }
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