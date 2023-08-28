using Autofac;
using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Services;
using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.DbInitializer;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.DataAccess.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Targets;
using System.Diagnostics;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using Benny_Scraper.BusinessLogic.Helper;

namespace Benny_Scraper
{
    internal class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static IContainer Container { get; set; }
        public static IConfiguration Configuration { get; set; }
        // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"


        static async Task Main(string[] args)
        {
            SetupLogger();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            SQLitePCL.Batteries.Init();
            Configuration = BuildConfiguration();

            var builder = new ContainerBuilder();
            ConfigureServices(builder);
            Container = builder.Build();

            if (args.Length > 0)
            {
                await RunAsync(args);
            }
            else
            {
                Logger.Info("Application Started");
                await RunAsync();
            }
        }

        private static async Task RunAsync()
        {
            //create methods to check for updates for each novel in our database
            using (var scope = Container.BeginLifetimeScope())
            {                
                var logger = NLog.LogManager.GetCurrentClassLogger();
                Logger.Info("Initializing Database");
                DbInitializer dbInitializer = scope.Resolve<DbInitializer>();
                dbInitializer.Initialize();
                Logger.Info("Database Initialized");
                
                string instructions = GetInstructions();

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(instructions);
                Console.ResetColor();

                INovelProcessor novelProcessor = scope.Resolve<INovelProcessor>();

                bool isApplicationRunning = true;
                while (isApplicationRunning)
                {
                    // Uri help https://www.dotnetperls.com/uri#:~:text=URI%20stands%20for%20Universal%20Resource,strings%20starting%20with%20%22http.%22
                    Console.WriteLine("\nEnter the site url (or 'recreate' {site url} to create from saved webnovel)(or 'exit' to quit): ");
                    string siteUrl = Console.ReadLine();
                    string[] input = siteUrl.Split(' ');

                    if (string.IsNullOrWhiteSpace(siteUrl))
                    {
                        Console.WriteLine("Invalid input. Please enter a valid URL.");
                        continue;
                    }

                    if (siteUrl.ToLower() == "exit")
                    {
                        isApplicationRunning = false;
                        continue;
                    }                    

                    if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out Uri novelTableOfContentUri))
                    {
                        Console.WriteLine("Invalid URL. Please enter a valid URL.");
                        continue;
                    }

                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    try
                    {
                        await novelProcessor.ProcessNovelAsync(novelTableOfContentUri);
                        
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Exception when trying to process novel. {ex}");
                    }
                    stopwatch.Stop();
                    TimeSpan elapsedTime = stopwatch.Elapsed;
                    Logger.Info($"Elapsed time: {elapsedTime}");
                }
            }
        }

        private static string GetInstructions()
        {
            List<string> supportedSites = new List<string>
                {
                    "\nWebnovel Pub (https://www.lightnovelworld.com/)",
                    "Novel Full (https://www.novelfull.com/)",
                    "Mangakakalot (https://mangakakalot.to/)",
                    "Mangareader (https://www.mangareader.to/)",
                    "Mangakatana (https://mangakatana.com/)",
                };

            string instructions = "\n" + $@"Welcome to our novel scraper application!
                Currently, we support the following websites:
                {string.Join("\n", supportedSites)}

                To use our application, please follow these steps:
                1. Visit a supported website.
                2. Choose a novel and navigate to its table of contents page.
                3. Copy the URL of this page.
                4. Paste the URL into our application when prompted.

                Please ensure the URL is from the table of contents page of a novel.
                Our application will then download the novel and convert it into an EPUB file.
                Thank you for using our application! Enjoy your reading.";

            return instructions;
        }


        // create private RunAsync that accepts args and then call it from Main, also make it so that args are used, accepting multiple arguments 'clear_database' should be an argument that will clear the database using the removeall method. Case statement should be used to check for the argument and then call the removeall method.
        private static async Task RunAsync(string[] args)
        {
            using (var scope = Container.BeginLifetimeScope())
            {
                var logger = NLog.LogManager.GetCurrentClassLogger();
                INovelService novelService = scope.Resolve<INovelService>();

                switch (args[0])
                {
                    case "list":
                        {
                            var novels = await novelService.GetAllAsync();

                            // Calculate the maximum lengths of each column
                            int maxIdLength = novels.Max(novel => novel.Id.ToString().Length);
                            int maxTitleLength = novels.Max(novel => novel.Title.Length);

                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.WriteLine($"Id:".PadRight(maxIdLength) + "\tTitle:".PadRight(maxTitleLength));
                            Console.ResetColor();
                            foreach (var novel in novels)
                            {                                
                                Console.WriteLine($"{novel.Id.ToString().PadRight(maxIdLength)}\t{novel.Title.PadRight(maxTitleLength)}");
                            }
                            break;
                        }
                    case "clear_database":
                        {
                            logger.Info("Clearing all novels and chapter from database");
                            await novelService.RemoveAllAsync();
                        }
                        break;
                    case "delete_novel_by_id": // only way to resovle the same variable, in the case novelService is to surround the case statement in curly braces
                        {
                            logger.Info($"Deleting novel with id {args[1]}");
                            Guid.TryParse(args[1], out Guid novelId);
                            await novelService.RemoveByIdAsync(novelId);
                            logger.Info($"Novel with id: {args[1]} deleted.");
                        }
                        break;
                    case "recreate": // at this momement should only work for webnovels not mangas. Need to create something to distinguish between the two in the database
                        {
                            try
                            {
                                IEpubGenerator epubGenerator = scope.Resolve<IEpubGenerator>();
                                Uri.TryCreate(args[1], UriKind.Absolute, out Uri tableOfContentUri);
                                bool isNovelInDatabase = await novelService.IsNovelInDatabaseAsync(tableOfContentUri.ToString());
                                if (isNovelInDatabase)
                                {
                                    var novel = await novelService.GetByUrlAsync(tableOfContentUri);
                                    Logger.Info($"Recreating novel {novel.Title}. Id: {novel.Id}, Total Chapters: {novel.Chapters.Count()}");
                                    var chapters = novel.Chapters.Where(c => c.Number != 0).OrderBy(c => c.Number).ToList();
                                    string safeTitle = CommonHelper.GetFileSafeName(novel.Title);
                                    var documentsFolder = GetDocumentsFolder(safeTitle);
                                    Directory.CreateDirectory(documentsFolder);
                                    string epubFile = Path.Combine(documentsFolder, $"{safeTitle}.epub");
                                    epubGenerator.CreateEpub(novel, chapters, epubFile, null);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Exception when trying to recreate novel. {ex}");
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private static string GetDocumentsFolder(string title)
        {
            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.Equals(Environment.UserName, "emiya", StringComparison.OrdinalIgnoreCase))
                documentsFolder = DriveInfo.GetDrives().FirstOrDefault(drive => drive.Name == @"H:\")?.Name ?? documentsFolder;
            string fileRegex = @"[^a-zA-Z0-9-\s]";
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            var novelFileSafeTitle = textInfo.ToTitleCase(Regex.Replace(title, fileRegex, string.Empty).ToLower().ToLowerInvariant());
            return Path.Combine(documentsFolder, "BennyScrapedNovels", novelFileSafeTitle);
        }

        private static void SetupLogger()
        {
            var config = new NLog.Config.LoggingConfiguration();

            //write log file using date as day-month-year
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string directoryPath = Path.Combine(appDataPath, "BennyScraper", "logs");

            string logPath = Path.Combine(directoryPath, $"log-book {DateTime.Now.ToString("MM-dd-yyyy")}.log");
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = logPath };

            var logconsole = new NLog.Targets.ColoredConsoleTarget("logconsole")
            {
                Layout = @"${date:format=HH\:mm\:ss} ${level} ${message} ${exception}"
            };

            // Set up colors
            logconsole.RowHighlightingRules.Add(new NLog.Targets.ConsoleRowHighlightingRule(
                NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Info"),
                ConsoleOutputColor.Green, ConsoleOutputColor.Black));
            logconsole.RowHighlightingRules.Add(new NLog.Targets.ConsoleRowHighlightingRule(
                NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Warn"),
                ConsoleOutputColor.DarkYellow, ConsoleOutputColor.Black));
            logconsole.RowHighlightingRules.Add(new NLog.Targets.ConsoleRowHighlightingRule(
                NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Error"),
                ConsoleOutputColor.Red, ConsoleOutputColor.Black));
            logconsole.RowHighlightingRules.Add(new NLog.Targets.ConsoleRowHighlightingRule(
                NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Fatal"),
                ConsoleOutputColor.White, ConsoleOutputColor.Red));

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);

            NLog.LogManager.Configuration = config;
        }


        /// <summary>
        /// Loads the configuration for the application from appsettings.json. The configuration is used to configure the application's services, and will be 
        /// handed to the Autofac container builder in Startup.cs, which will register the appsettings as classes I have defined.
        /// </summary>
        /// <returns>The loaded configuration object.</returns>
        private static IConfigurationRoot BuildConfiguration()
        {
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true)
                .Build();

            return configuration;
        }

        /// <summary>
        /// Register all services and repositories, including the DbContext, appsettings.json as NovelScraperSettings and EpubTemplates based on the key in the file
        /// </summary>
        /// <param name="builder"></param>
        public static void ConfigureServices(ContainerBuilder builder)
        {
            // Register IConfiguration
            builder.RegisterInstance(Configuration).As<IConfiguration>();

            builder.Register(c => new Database(new DbContextOptionsBuilder<Database>()
                .UseSqlite(GetConnectionString(), options => options.MigrationsAssembly("Benny-Scraper.DataAccess")).Options)).InstancePerLifetimeScope();


            builder.RegisterType<DbInitializer>().As<DbInitializer>();
            builder.RegisterType<UnitOfWork>().As<IUnitOfWork>();
            builder.RegisterType<NovelProcessor>().As<INovelProcessor>();
            builder.RegisterType<ChapterRepository>().As<IChapterRepository>();
            builder.RegisterType<NovelService>().As<INovelService>().InstancePerLifetimeScope();
            builder.RegisterType<ChapterService>().As<IChapterService>().InstancePerLifetimeScope();
            builder.RegisterType<NovelRepository>().As<INovelRepository>();
            builder.RegisterType<EpubGenerator>().As<IEpubGenerator>().InstancePerDependency();

            builder.Register(c =>
            {
                var config = c.Resolve<IConfiguration>();
                var settings = new NovelScraperSettings();
                config.GetSection("NovelScraperSettings").Bind(settings);
                return settings;
            }).SingleInstance();
            //needed to register NovelScraperSettings implicitly, Autofac does not resolve 'IOptions<T>' by defualt. Optoins.Create avoids ArgumentException
            builder.Register(c => Options.Create(c.Resolve<NovelScraperSettings>())).As<IOptions<NovelScraperSettings>>().SingleInstance();

            // register EpuTemplates.cs as singleton from the appsettings.json file
            builder.Register(c =>
            {
                var config = c.Resolve<IConfiguration>();
                var settings = new EpubTemplates();
                config.GetSection("EpubTemplates").Bind(settings);
                return settings;
            }).SingleInstance();
            builder.Register(c => Options.Create(c.Resolve<EpubTemplates>())).As<IOptions<EpubTemplates>>().SingleInstance();

            // register the factory
            builder.Register<Func<string, INovelScraper>>(c =>
            {
                var context = c.Resolve<IComponentContext>();
                return key => context.ResolveNamed<INovelScraper>(key);
            });

            builder.RegisterType<NovelScraperFactory>().As<INovelScraperFactory>().InstancePerDependency();
            builder.RegisterType<SeleniumNovelScraper>().Named<INovelScraper>("Selenium").InstancePerDependency(); // InstancePerDependency() similar to transient
            builder.RegisterType<HttpNovelScraper>().Named<INovelScraper>("Http").InstancePerDependency();
        }

        /// <summary>
        /// Get the connection string for the database file, if the file does not exist, create it
        /// </summary>
        /// <returns>connection string</returns>
        private static string GetConnectionString()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string directoryPath = Path.Combine(appDataPath, "BennyScraper", "Database");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string dbPath = Path.Combine(directoryPath, "BennyTestDb.db");
            var connectionString = $"Data Source={dbPath};";
            return connectionString;
        }
    }
}