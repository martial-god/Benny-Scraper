using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using Autofac;
using Benny_Scraper.BusinessLogic;
using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.FileGenerators;
using Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;
using Benny_Scraper.BusinessLogic.Helper;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Services;
using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.DbInitializer;
using Benny_Scraper.DataAccess.Repository;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;
using CommandLine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

namespace Benny_Scraper
{
    internal class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static IContainer Container { get; set; }
        private const string AreYouSure = "Are you sure you want to {0}? (y/n)";

        public static IConfiguration Configuration { get; set; }

        // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"
        static async Task Main(string[] args)
        {
            DeleteOldLogs();
            SetupLogger(LogLevel.Info);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            SQLitePCL.Batteries.Init();
            Configuration = BuildConfiguration();

            var builder = new ContainerBuilder();
            ConfigureServices(builder);
            Container = builder.Build();

            using var scope = Container.BeginLifetimeScope();
            DbInitializer dbInitializer = scope.Resolve<DbInitializer>();
            bool dbChangesMade = dbInitializer.Initialize();

            if (dbChangesMade)
                Logger.Info("Database Initialized");


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
            using (var scope = Container.BeginLifetimeScope())
            {
                var logger = NLog.LogManager.GetCurrentClassLogger();

                string instructions = GetInstructions();

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(instructions);
                Console.ResetColor();

                INovelProcessor novelProcessor = scope.Resolve<INovelProcessor>();

                bool isApplicationRunning = true;
                while (isApplicationRunning)
                {
                    // Uri help https://www.dotnetperls.com/uri#:~:text=URI%20stands%20for%20Universal%20Resource,strings%20starting%20with%20%22http.%22
                    Console.WriteLine("\nEnter the site url (or 'exit' to quit): ");
                    string siteUrl = Console.ReadLine().Trim();
                    string[] input = siteUrl.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    if (string.IsNullOrWhiteSpace(siteUrl))
                    {
                        Console.WriteLine("Invalid input. Please enter a valid URL.");
                        continue;
                    }

                    if (siteUrl.ToLowerInvariant() == "exit")
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
            HttpNovelScraper httpNovelScraper = new(); //used specifically for getting all supported urls.
            var supportedSites = httpNovelScraper.GetSupportedSites();

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

        #region CommandLine Methods
        private static async Task RunAsync(string[] args)
        {
            var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
            await result.MapResult(
                async options => await HandleOptionsAsync(options),
                errors => HandleParseErrors(errors)
            );
        }

        private static async Task HandleOptionsAsync(CommandLineOptions options)
        {
            if (options.List)
                await ListNovelsAsync(options.Page, options.ItemsPerPage, options.SearchKeyword);
            else if (options.ExtensionType)
            {
                await GetDefaultMangaExtensionAsync();
            }
            else if (options.ClearDatabase)
            {
                var userQuery = string.Format(AreYouSure, "clear the database");
                Console.WriteLine(userQuery);
                var confirmation = Console.ReadLine();
                if (confirmation.ToLowerInvariant() == "y")
                    await ClearDatabaseAsync();
            }
            else if (options.UpdateAll)
            {
                await UpdateAllNovelsAsync(CancellationToken.None);
            }
            else if (options.DeleteNovelById != Guid.Empty)
            {
                await DeleteNovelByIdAsync(options.DeleteNovelById);
            }
            else if (options.RecreateEpubById != Guid.Empty)
            {
                await RecreateEpubByIdAsync(options.RecreateEpubById);
            }
            else if (options.UpdateNovelSavedLocationById != Guid.Empty)
            {
                await UpdateNovelSavedLocationByIdAsync(options.UpdateNovelSavedLocationById);
            }
            else if (options.NovelInformation != Guid.Empty)
            {
                await DisplayNovelInformationAsync(options.NovelInformation);
            }
            else if (options.NovelExtensionById != Guid.Empty)
            {
                await UpdateNovelFileType(options.NovelExtensionById);
            }
            else if (options.ConcurrentRequests > 0)
            {
                await SetConcurrentRequestsAsync(options.ConcurrentRequests);
            }
            else if (!string.IsNullOrEmpty(options.SaveLocation))
            {
                await SetSaveLocationAsync(options.SaveLocation);
            }
            else if (!string.IsNullOrEmpty(options.MangaSaveLocation))
            {
                await SetMangaSaveLocationAsync(options.MangaSaveLocation);
            }
            else if (!string.IsNullOrEmpty(options.NovelSaveLocation))
            {
                await SetNovelSaveLocationAsync(options.NovelSaveLocation);
            }
            else if (options.MangaExtension >= 0 && options.MangaExtension < Enum.GetNames(typeof(FileExtension)).Length)
            {
                int extension = (int)options.MangaExtension;
                await SetDefaultMangaExtensionAsync(extension);
            }
            else if (string.Equals(options.SingleFile.ToLowerInvariant(), "y", StringComparison.OrdinalIgnoreCase) || string.Equals(options.SingleFile.ToLowerInvariant(), "n", StringComparison.OrdinalIgnoreCase))
            {
                bool singleFile = options.SingleFile.ToLowerInvariant() == "y";
                await SetSingleFileAsync(singleFile);
            }
            else if (options.ExtensionType)
            {
                await GetDefaultMangaExtensionAsync();
            }
            else
                Console.WriteLine("Invalid command. Please try again.");
        }

        private static async Task UpdateAllNovelsAsync(CancellationToken cancellation)
        {
            if (cancellation.IsCancellationRequested)
                return;
            var scope = Container.BeginLifetimeScope();
            var novelService = scope.Resolve<INovelService>();
            var novelProcessor = scope.Resolve<INovelProcessor>();
            var updatedNovels = new List<(int, string)>();
            var novels = await novelService.GetAllAsync();
            var nonCompletedNovels = novels.Where(novel => !novel.LastChapter)
                .Where(novel => novel.SiteName != "mangareader.to" && novel.DateLastModified.Date != DateTime.Now.Date).ToList(); // issue with mangareader.to
            // change default log level to error
            SetupLogger(LogLevel.Info);
            int count = 0;
            foreach (var novel in nonCompletedNovels)
            {
                if (cancellation.IsCancellationRequested)
                    break;
                try
                {
                    await novelProcessor.ProcessNovelAsync(new Uri(novel.Url));
                    ++count;
                    updatedNovels.Add((count, novel.Title));
                }
                catch (Exception ex)
                {

                    Logger.Error($"Exception when trying to update novel. {ex.Message}");
                    Console.WriteLine("Completed novels: " + count);
                    foreach (var updateNovel in updatedNovels)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"{updateNovel.Item1}) {updateNovel.Item2}");
                        Console.ResetColor();
                    }
                }
            }
            Console.WriteLine("Completed novels: " + count);
            foreach (var updateNovel in updatedNovels)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{updateNovel.Item1}) {updateNovel.Item2}");
            }
            Console.ResetColor();
        }

        private static Task HandleParseErrors(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Console.WriteLine($"Error: {error}");
            }

            // Depending on your requirements, you can return a faulted task to signal an error.
            return Task.FromResult(1);
        }

        private static async Task ListNovelsAsync(int page, int itemsPerPage, string searchKeyWord)
        {
            if (page <= 0)
            {
                Console.WriteLine("Page number must be greater than 0. Please enter a valid page number");
                return;
            }
            if (itemsPerPage <= 0)
            {
                Console.WriteLine("Items per page must be greater than 0. Please enter a valid number of items per page");
                return;
            }
            await using var scope = Container.BeginLifetimeScope();
            var novelService = scope.Resolve<INovelService>();

            var novels = await novelService.GetAllAsync();
            if (novels == null || !novels.Any())
            {
                Console.WriteLine("No novels found.");
                return;
            }
            if (!string.IsNullOrEmpty(searchKeyWord))
                novels = novels.Where(novel => novel.Title.Contains(searchKeyWord, StringComparison.InvariantCultureIgnoreCase));

            var paginatedNovels = novels.Skip((page - 1) * itemsPerPage).Take(itemsPerPage);
            int totalPages = (int)Math.Ceiling((double)novels.Count() / itemsPerPage);

            int maxNoLength = novels.Count().ToString().Length + 3;  // "3" accounts for ")."
            int maxIdLength = novels.Max(novel => novel.Id.ToString().Length);
            int maxChapterLength = novels.Max(novel => novel.CurrentChapter?.Length ?? 0);  // New line for max chapter length
            int maxFileTypeLength = novels.Max(novel => novel.FileType.ToString().Length + 3); // +3 for " []"
            int maxTitleLength = novels.Max(novel => Math.Min(novel.Title.Length, 60 - maxFileTypeLength)); // Adjusted for maxFileTypeLength

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"No.".PadRight(maxNoLength) +
                              "ID".PadRight(maxIdLength + 2) +
                              "Title [FileType]".PadRight(maxTitleLength + 2) +
                              "Current Chapter".PadRight(maxChapterLength + 2));
            Console.ResetColor();

            int count = 0;
            foreach (var novel in paginatedNovels)
            {
                var countStr = $"{++count}).".PadRight(maxNoLength);
                var idStr = novel.Id.ToString().PadRight(maxIdLength);

                var truncatedTitle = TruncateTitle(novel.Title, maxTitleLength - novel.FileType.ToString().Length - 3);  // -3 for the space, brackets, and the fileType itself
                var titleStr = $"{truncatedTitle} [{novel.FileType}]";
                titleStr = titleStr.PadRight(maxTitleLength + maxFileTypeLength);

                var chapterStr = (novel.CurrentChapter ?? "N/A").PadRight(maxChapterLength);  // New line for chapter string

                if (novel.LastChapter)
                    Console.ForegroundColor = ConsoleColor.Green;
                else
                    Console.ResetColor();
                Console.WriteLine($"{countStr}{idStr}  {titleStr}  {chapterStr}");
                if (novel.LastChapter)
                    Console.ResetColor();
            }


            Console.WriteLine();
            Console.WriteLine($"Total: {novels.Count()}   Novels Completed: {novels.Count(novel => novel.LastChapter == true)}");

            if (totalPages == 1)
                return;
            Console.WriteLine($"Showing page {page} of {totalPages}");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("[N]ext page, [P]revious page, [J]ump to page, [Q]uit");
            Console.ResetColor();
            var userInput = Console.ReadKey();
            Console.WriteLine();

            switch (userInput.KeyChar)
            {
                case 'N':
                case 'n':
                    page++;
                    if (page > totalPages)
                    {
                        Console.WriteLine("You are on the last page.");
                        page--;  // Reset to last page
                    }
                    break;

                case 'P':
                case 'p':
                    page--;
                    if (page < 1)
                    {
                        Console.WriteLine("You are on the first page.");
                        page++;  // Reset to first page
                    }
                    break;

                case 'J':
                case 'j':
                    Console.Write("Enter the page number: ");
                    if (int.TryParse(Console.ReadLine(), out int selectedPage) && selectedPage > 0 && selectedPage <= totalPages)
                    {
                        page = selectedPage;
                    }
                    else
                    {
                        Console.WriteLine("Invalid page number.");
                    }
                    break;

                case 'Q':
                case 'q':
                    return;  // Exit the method

                default:
                    Console.WriteLine("Invalid choice.");
                    break;
            }

            // Recursive call to load the selected page
            await ListNovelsAsync(page, itemsPerPage, searchKeyWord);
        }

        private static string TruncateTitle(string title, int maxLength)
        {
            if (string.IsNullOrEmpty(title) || title.Length <= maxLength)
                return title;

            return title.Substring(0, maxLength - 3) + "..."; // -3 to account for "..."
        }


        private static async Task ClearDatabaseAsync()
        {
            await using var scope = Container.BeginLifetimeScope();
            var Logger = NLog.LogManager.GetCurrentClassLogger();
            var novelService = scope.Resolve<INovelService>();

            Logger.Info("Clearing all novels and chapters from database");
            await novelService.RemoveAllAsync();
            Logger.Info("Database cleared");
        }

        private static async Task DeleteNovelByIdAsync(Guid id)
        {
            await using var scope = Container.BeginLifetimeScope();
            var novelService = scope.Resolve<INovelService>();
            var novel = await novelService.GetByIdAsync(id);
            if (novel == null)
                Console.WriteLine($"Novel with id: {id} not found.");
            else
            {
                Logger.Info($"Deleting novel {novel.Title} with id {id}");
                await novelService.RemoveByIdAsync(id);
                Logger.Info($"Novel with id: {id} deleted.");
            }
        }

        private static async Task SetDefaultMangaExtensionAsync(int extension)
        {
            var totalExtensions = Enum.GetNames(typeof(FileExtension)).Length;
            if (extension > totalExtensions)
                Console.WriteLine("Invalid extension. Please enter a value between 1 and " + totalExtensions);
            await using var scope = Container.BeginLifetimeScope();
            var configurationRepository = scope.Resolve<IConfigurationRepository>();
            var configuration = await configurationRepository.GetByIdAsync(1);
            configuration.DefaultMangaFileExtension = (FileExtension)extension;
            configurationRepository.Update(configuration);
            Console.WriteLine($"Default manga extension updated: {configuration.DefaultMangaFileExtension}");
        }

        private static async Task GetDefaultMangaExtensionAsync()
        {
            await using var scope = Container.BeginLifetimeScope();
            var configurationRepository = scope.Resolve<IConfigurationRepository>();
            var configuration = await configurationRepository.GetByIdAsync(1);
            var extensions = Enum.GetValues(typeof(FileExtension)).Cast<FileExtension>().ToList();
            Console.WriteLine($"Default manga extension: {configuration.DefaultMangaFileExtension}");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Available extensions: {string.Join(", ", extensions)}");
            Console.ResetColor();
        }

        private static async Task RecreateEpubByIdAsync(Guid id)
        {
            try
            {
                await using var scope = Container.BeginLifetimeScope();
                var configurationRepository = scope.Resolve<IConfigurationRepository>();
                var novelService = scope.Resolve<INovelService>();
                var configuration = await configurationRepository.GetByIdAsync(1);
                IEpubGenerator epubGenerator = scope.Resolve<IEpubGenerator>();
                var novel = await novelService.GetByIdAsync(id);
                if (novel != null)
                {
                    Logger.Info($"Recreating novel {novel.Title}. Id: {novel.Id}, Total Chapters: {novel.Chapters.Count}");
                    var chapters = novel.Chapters.Where(c => c.Number != 0).OrderBy(c => c.Number).ToList();
                    string safeTitle = CommonHelper.SanitizeFileName(novel.Title, true);
                    var documentsFolder = CommonHelper.GetOutputDirectoryForTitle(safeTitle, configuration.DetermineSaveLocation());
                    Directory.CreateDirectory(documentsFolder);
                    string epubFile = Path.Combine(documentsFolder, $"{safeTitle}.epub");
                    epubGenerator.CreateEpub(novel, chapters, epubFile, null);
                }
                else
                    Logger.Error($"Novel with id {id} not found.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to recreate novel. {ex.Message}");
            }
        }

        private static async Task SetConcurrentRequestsAsync(int concurrentRequests)
        {
            try
            {
                await using var scope = Container.BeginLifetimeScope();
                var configurationRepository = scope.Resolve<IConfigurationRepository>();
                var configuration = await configurationRepository.GetByIdAsync(1);
                configuration.ConcurrencyLimit = concurrentRequests;
                configurationRepository.Update(configuration);
                Console.WriteLine($"Concurrent requests updated: {configuration.ConcurrencyLimit}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to set concurrent requests. {ex.Message}");
            }

        }

        private static async Task SetSaveLocationAsync(string saveLocation)
        {
            try
            {
                if (Directory.Exists(saveLocation))
                {
                    await using var scope = Container.BeginLifetimeScope();
                    var configurationRepository = scope.Resolve<IConfigurationRepository>();
                    var configuration = configurationRepository.GetByIdAsync(1).Result;
                    configuration.SaveLocation = saveLocation;
                    configurationRepository.Update(configuration);
                    Console.WriteLine($"Save location updated: {configuration.SaveLocation}");
                }
                else
                    Console.WriteLine($"Directory {saveLocation} does not exist.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to set save location. {ex.Message}");
            }
        }

        private static async Task SetMangaSaveLocationAsync(string saveLocation)
        {
            try
            {
                if (Directory.Exists(saveLocation))
                {
                    await using var scope = Container.BeginLifetimeScope();
                    var configurationRepository = scope.Resolve<IConfigurationRepository>();
                    var configuration = configurationRepository.GetByIdAsync(1).Result;
                    configuration.MangaSaveLocation = saveLocation;
                    configurationRepository.Update(configuration);
                    Console.WriteLine($"Manga save location updated: {configuration.MangaSaveLocation}");
                }
                else
                    Console.WriteLine($"Directory {saveLocation} does not exist.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to set manga save location. {ex.Message}");
            }
        }

        private static async Task SetNovelSaveLocationAsync(string saveLocatoin)
        {
            try
            {
                if (Directory.Exists(saveLocatoin))
                {
                    await using var scope = Container.BeginLifetimeScope();
                    var configurationRepository = scope.Resolve<IConfigurationRepository>();
                    var configuration = configurationRepository.GetByIdAsync(1).Result;
                    configuration.NovelSaveLocation = saveLocatoin;
                    configurationRepository.Update(configuration);
                    Console.WriteLine($"Novel save location updated: {configuration.NovelSaveLocation}");
                }
                else
                    Console.WriteLine($"Directory {saveLocatoin} does not exist.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to set novel save location. {ex.Message}");
            }
        }

        public static async Task SetSingleFileAsync(bool singleFile)
        {
            try
            {
                await using var scope = Container.BeginLifetimeScope();
                var configurationRepository = scope.Resolve<IConfigurationRepository>();
                var configuration = await configurationRepository.GetByIdAsync(1);
                configuration.SaveAsSingleFile = singleFile;
                configurationRepository.Update(configuration);
                if (configuration.SaveAsSingleFile)
                    Console.WriteLine("Single file mode enabled.");
                else
                    Console.WriteLine("Single file mode disabled.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to set single file. {ex.Message}");
            }
        }

        public static async Task UpdateNovelSavedLocationByIdAsync(Guid id)
        {
            try
            {
                await using var scope = Container.BeginLifetimeScope();
                var novelService = scope.Resolve<INovelService>();
                var novel = await novelService.GetByIdAsync(id);
                if (novel != null)
                {
                    Console.WriteLine($"Novel: {novel.Title}");
                    Console.WriteLine($"Current place where we think the novel is stored: {novel.SaveLocation}\n");
                    Console.WriteLine(@"Please enter the full path to the novel, this includes the file name. i.e. C:\user\documents\mynovel.epub");
                    string newSaveLocation = Console.ReadLine();
                    if (File.Exists(newSaveLocation))
                    {
                        novel.SaveLocation = newSaveLocation;
                        await novelService.UpdateAsync(novel);
                        Console.WriteLine($"\nSave location updated: {novel.SaveLocation}");
                    }
                    else
                        Console.WriteLine($"\nDirectory {newSaveLocation} does not exist.");
                }
                else
                    Console.WriteLine($"\nNovel with id {id} not found.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to update novel save location. {ex.Message}");
            }
        }

        public static async Task RenameDatabaseFileAsync(string newDbName)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string directoryPath = Path.Combine(appDataPath, "BennyScraper", "Database");
                string oldDbPath = GetConnectionString(); // Assuming this returns the full path
                string newDbPath = Path.Combine(directoryPath, newDbName);

                // Rename the physical file
                File.Move(oldDbPath, newDbPath);

                // Update the configuration table
                await using var scope = Container.BeginLifetimeScope();
                var configurationRepository = scope.Resolve<IConfigurationRepository>();
                var configuration = await configurationRepository.GetByIdAsync(1);
                configuration.DatabaseFileName = newDbName;
                configurationRepository.Update(configuration);

                Console.WriteLine($"Database file renamed to: {newDbName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to rename database file. {ex.Message}");
            }
        }

        public static async Task DisplayNovelInformationAsync(Guid novelId)
        {
            using var scope = Container.BeginLifetimeScope();
            var novelService = scope.Resolve<INovelService>();

            Novel novel = await novelService.GetByIdAsync(novelId);
            if (novel == null)
            {
                Console.WriteLine($"No novel found for ID: {novelId}");
                return;
            }

            var details = new List<KeyValuePair<string, string>>
            {
                new("ID", novel.Id.ToString()),
                new("Title", novel.Title),
                new("Author", novel.Author ?? "N/A"),
                new("Site Name", novel.SiteName),
                new("URL", novel.Url),
                new("Genre(s)", novel.Genre ?? "N/A"),
                new("Current Chapter", novel.CurrentChapter ?? "N/A"),
                new("Current Chapter Url", novel.CurrentChapterUrl ?? "N/A"),
                new("Total Chapters", novel.TotalChapters.ToString()),
                new("Date Created", novel.DateCreated.ToShortDateString()),
                new("Last Modified", novel.DateLastModified.ToShortDateString()),
                new("NovelStatus", !string.IsNullOrEmpty(novel.Status) ? novel.Status : "N/A"),
                new("Save Location", novel.SaveLocation ?? "N/A"),
                new("File Type", Enum.GetName(typeof(NovelFileType), novel.FileType) ?? "EPUB"),
                new("Saved As Single File", novel.SavedFileIsSplit ? "No" : "Yes")
            };

            Console.WriteLine("NOVEL INFORMATION:");
            Console.WriteLine("-------------------");
            foreach (var detail in details)
            {
                Console.WriteLine($"{detail.Key.PadRight(20)} {detail.Value}");
            }
            Console.WriteLine("-------------------");
        }

        private static async Task UpdateNovelFileType(Guid id)
        {
            try
            {
                await using var scope = Container.BeginLifetimeScope();
                var novelService = scope.Resolve<INovelService>();
                var novel = await novelService.GetByIdAsync(id);

                if (novel != null)
                {
                    Console.WriteLine($"Current file type for novel: {novel.FileType}");
                    var extensions = Enum.GetValues(typeof(NovelFileType)).Cast<NovelFileType>().ToList();

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"Available extensions: {string.Join(", ", extensions.Select((ext, index) => $"({index}) {ext}"))}");
                    Console.ResetColor();

                    Console.WriteLine("Please enter the file type as a number you want to change the novel to.");
                    string fileType = Console.ReadLine();

                    if (int.TryParse(fileType, out int fileTypeInt) && Enum.IsDefined(typeof(NovelFileType), fileTypeInt))
                    {
                        novel.FileType = (NovelFileType)fileTypeInt;
                        await novelService.UpdateAsync(novel);
                        Console.WriteLine($"Novel file type updated to: {novel.FileType}");
                    }
                    else
                        Console.WriteLine("Invalid file type entered.");
                }
                else
                    Console.WriteLine($"No novel found for ID: {id}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to update novel file type. {ex.Message}");
            }
        }
        #endregion

        #region Setup
        private static void SetupLogger(LogLevel logLevel)
        {
            var config = new NLog.Config.LoggingConfiguration();

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string directoryPath = Path.Combine(appDataPath, "BennyScraper", "logs");

            string logPath = Path.Combine(directoryPath, $"log-book {DateTime.Now.ToString("MM-dd-yyyy")}.log");
            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = logPath };

            var logconsole = new NLog.Targets.ColoredConsoleTarget("logconsole")
            {
                Layout = @"${date:format=HH\:mm\:ss} ${level} ${message} ${exception}"
            };

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

            config.AddRule(logLevel, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);

            NLog.LogManager.Configuration = config;
        }

        private static void DeleteOldLogs()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string directoryPath = Path.Combine(appDataPath, "BennyScraper", "logs");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var directory = new DirectoryInfo(directoryPath);
            var files = directory.GetFiles("*.log")
                .OrderByDescending(file => file.LastWriteTime)
                .Skip(5);

            foreach (var file in files)
            {
                file.Delete();
            }
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
            builder.RegisterType<ConfigurationRepository>().As<IConfigurationRepository>();
            builder.RegisterType<EpubGenerator>().As<IEpubGenerator>().InstancePerDependency();
            builder.RegisterType<PdfGenerator>().As<PdfGenerator>().InstancePerDependency();
            builder.RegisterType<ComicBookArchiveGenerator>().As<IComicBookArchiveGenerator>().InstancePerDependency();

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
        #endregion
    }
}