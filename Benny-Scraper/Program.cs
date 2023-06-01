using Autofac;
using Benny_Scraper.BusinessLogic;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.DataAccess.DbInitializer;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Targets;
using System.Diagnostics;

namespace Benny_Scraper
{
    internal class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static IContainer Container { get; set; }
        // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"
        static async Task Main(string[] args)
        {
            SetupLogger();
            Logger.Info("Application Started");
            SQLitePCL.Batteries.Init();
            var configuration = BuildConfiguration();
            // Pass the built configuration to the StartUp class
            var startUp = new StartUp(configuration);

            // Database Injections
            var builder = new ContainerBuilder();
            startUp.ConfigureServices(builder);

            Container = builder.Build();

            if (args.Length > 0)
            {
                await RunAsync(args);
            }
            else
            {
                await RunAsync();
            }
        }

        private static async Task RunAsync()
        {
            using (var scope = Container.BeginLifetimeScope())
            {
                
                var logger = NLog.LogManager.GetCurrentClassLogger();
                Logger.Info("Initializing Database");
                IDbInitializer dbInitializer = scope.Resolve<IDbInitializer>();
                dbInitializer.Initialize();
                Logger.Info("Database Initialized");
                
                string instructions = GetInstructions();

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(instructions);
                Console.ResetColor();

                IEpubGenerator epubGenerator = scope.Resolve<IEpubGenerator>();
                //epubGenerator.ValidateEpub(@"C:\Users\Emiya\Documents\BennyScrapedNovels\SUPREMACY GAMES\Read Supremacy Games\supremacy games.epub");

                INovelProcessor novelProcessor = scope.Resolve<INovelProcessor>();

                bool isApplicationRunning = true;
                while (isApplicationRunning)
                {
                    // Uri help https://www.dotnetperls.com/uri#:~:text=URI%20stands%20for%20Universal%20Resource,strings%20starting%20with%20%22http.%22
                    Console.WriteLine("\nEnter the site url (or 'exit' to quit): ");
                    string siteUrl = Console.ReadLine();

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
                    "\nWebnovel Pub (https://www.webnovelpub.com/)",
                    "Novel Full (https://www.novelfull.com/)"
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
                switch (args[0])
                {
                    case "clear_database":
                        {
                            logger.Info("Clearing all novels and chapter from database");
                            INovelService novelService = scope.Resolve<INovelService>();
                            await novelService.RemoveAllAsync();
                        }
                        break;
                    case "delete_novel_by_id": // only way to resovle the same variable, in the case novelService is to surround the case statement in curly braces
                        {
                            logger.Info($"Deleting novel with id {args[1]}");
                            INovelService novelService = scope.Resolve<INovelService>();
                            Guid.TryParse(args[1], out Guid novelId);
                            await novelService.RemoveByIdAsync(novelId);
                        }
                        break;
                    default:
                        break;
                }
            }
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
                ConsoleOutputColor.Yellow, ConsoleOutputColor.Black));
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