// ReSharper disable LocalizableElement
using Autofac;
using Benny_Scraper.BusinessLogic;
using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.FileGenerators;
using Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;
using Benny_Scraper.BusinessLogic.Helper;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using Benny_Scraper.BusinessLogic.Services;
using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.BusinessLogic.Utilities;
using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.DbInitializer;
using Benny_Scraper.DataAccess.Repository;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;
using CommandLine;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NLog.Targets;
using System.Diagnostics;
using System.Text;
using LogLevel = NLog.LogLevel;

namespace Benny_Scraper
{
    internal class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private static IContainer? Container { get; set; }
        private const string AreYouSure = "Are you sure you want to {0}? (y/n)";
        private static IConfiguration? Configuration { get; set; }
        private const int DefaultConfigId = 1;

        // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"
        private static async Task Main(string[] args)
        {
            DeleteOldLogs();
            SetupLogger(LogLevel.Info);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            SQLitePCL.Batteries.Init();
            Configuration = BuildConfiguration();

            var builder = new ContainerBuilder();
            ConfigureServices(builder);
            Container = builder.Build();

            // Ensure Selenium drivers (and other unmanaged resources) are disposed even on crash/exit.
            ShutdownHooks.Register(Container.Resolve<IDriverFactory>());

            await using var scope = Container.BeginLifetimeScope();
            var dbInitializer = scope.Resolve<DbInitializer>();
            var dbChangesMade = dbInitializer.Initialize();

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
            await using var scope = Container.BeginLifetimeScope();
            var logger = NLog.LogManager.GetCurrentClassLogger();

            var instructions = GetInstructions();

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(instructions);
            Console.ResetColor();

            // Test all sites on startup to give users immediate feedback
            Console.WriteLine("\nTesting connectivity to all supported sites...\n");
            await TestAllSitesAsync();

            var novelProcessor = scope.Resolve<INovelProcessor>();

            var isApplicationRunning = true;
            while (isApplicationRunning)
            {
                // Uri help https://www.dotnetperls.com/uri#:~:text=URI%20stands%20for%20Universal%20Resource,strings%20starting%20with%20%22http.%22
                Console.WriteLine("\nEnter the site url (or 'exit' to quit): ");
                var siteUrl = Console.ReadLine().Trim();
                var input = siteUrl.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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

                // Check if user wants to test site connectivity
                if (input.Length >= 2 && input[0].ToLowerInvariant() == "test")
                {
                    var testUrl = input[1];
                    if (Uri.TryCreate(testUrl, UriKind.Absolute, out Uri testUri))
                    {
                        await TestSiteConnectivityAsync(testUri);
                    }
                    else
                    {
                        Console.WriteLine("Invalid test URL. Please provide a valid URL after 'test'.");
                    }
                    continue;
                }

                // Check if user wants to test all sites
                if (siteUrl.ToLowerInvariant() == "test-all")
                {
                    await TestAllSitesAsync();
                    continue;
                }

                if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out Uri novelTableOfContentUri))
                {
                    Console.WriteLine("Invalid URL. Please enter a valid URL.");
                    continue;
                }

                var stopwatch = new Stopwatch();
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
                var elapsedTime = stopwatch.Elapsed;
                Logger.Info($"Elapsed time: {elapsedTime}");
            }
        }

        private static string GetInstructions()
        {
            HttpNovelScraper httpNovelScraper = new(); //used specifically for getting all supported urls.
            var supportedSites = httpNovelScraper.GetSupportedSites();

            var instructions = "\n" + $@"Welcome to our novel scraper application!
                Currently, we support the following websites:
                {string.Join("\n", supportedSites)}

                To use our application, please follow these steps:
                1. Visit a supported website.
                2. Choose a novel and navigate to its table of contents page.
                3. Copy the URL of this page.
                4. Paste the URL into our application when prompted.

                Special Commands:
                - Type 'test <url>' to test if you can reach a site before implementing it
                - Type 'test-all' to test connectivity to all supported sites
                - Type 'exit' to quit

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
                HandleParseErrors
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
                if (confirmation?.ToLowerInvariant() == "y")
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
                var extension = (int)options.MangaExtension;
                await SetDefaultMangaExtensionAsync(extension);
            }
            else if (string.Equals(options.SingleFile?.ToLowerInvariant(), "y", StringComparison.OrdinalIgnoreCase) || string.Equals(options.SingleFile?.ToLowerInvariant(), "n", StringComparison.OrdinalIgnoreCase))
            {
                var singleFile = options.SingleFile?.ToLowerInvariant() == "y";
                await SetSingleFileAsync(singleFile);
            }
            else if (!string.IsNullOrEmpty(options.TestSite))
            {
                if (Uri.TryCreate(options.TestSite, UriKind.Absolute, out Uri testUri))
                {
                    await TestSiteConnectivityAsync(testUri);
                }
                else
                {
                    Console.WriteLine("Invalid URL for test site.");
                }
            }
            else if (options.TestAll)
            {
                await TestAllSitesAsync();
            }
            else if (!string.IsNullOrEmpty(options.TestInteractive))
            {
                if (Uri.TryCreate(options.TestInteractive, UriKind.Absolute, out Uri testUri))
                {
                    await RunInteractiveTestAsync(testUri);
                }
                else
                {
                    Console.WriteLine("Invalid URL for interactive test.");
                }
            }
            else if (!string.IsNullOrEmpty(options.TestField) && !string.IsNullOrEmpty(options.Url))
            {
                await RunSingleFieldTestAsync(options.TestField, options.Url);
            }
            else if (!string.IsNullOrEmpty(options.ValidateConfig))
            {
                await ValidateSiteConfigAsync(options.ValidateConfig);
            }
            else if (options.ValidateAllConfigs)
            {
                await ValidateAllSiteConfigsAsync();
            }
            else if (!string.IsNullOrEmpty(options.Url))
            {
                // Handle URL with optional chapter range
                if (Uri.TryCreate(options.Url, UriKind.Absolute, out var novelUri))
                {
                    await using var scope = Container.BeginLifetimeScope();
                    var novelProcessor = scope.Resolve<INovelProcessor>();
                    try
                    {
                        await novelProcessor.ProcessNovelAsync(novelUri, options.BeginChapter, options.EndChapter);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Exception when trying to process novel with chapter range. {ex}");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error processing novel: {ex.Message}");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.WriteLine("Invalid URL provided. Please provide a valid table of contents URL.");
                }
            }
            else
                Console.WriteLine("Invalid command or a parameter is missing. Please try again.");
        }

        private static async Task UpdateAllNovelsAsync(CancellationToken cancellation)
        {
            if (cancellation.IsCancellationRequested)
                return;
            var scope = Container.BeginLifetimeScope();
            var novelService = scope.Resolve<INovelService>();
            var novelProcessor = scope.Resolve<INovelProcessor>();
            var updatedNovels = new List<(int, string novelName)>();
            var failedToUpdate = new List<(int, string novelName)>();
            var novels = await novelService.GetAllAsync();
            var nonCompletedNovels = novels.Where(novel => !novel.LastChapter &&
                    novel.SiteName != "mangareader.to").ToList(); // issue with mangareader.to
            // change default log level to error
            SetupLogger(LogLevel.Error);
            var count = 0;
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
                    failedToUpdate.Add((count, novel.Title));
                }
            }

            Console.WriteLine("\nCompleted novels: " + updatedNovels.Count + $"/{nonCompletedNovels.Count}");
            foreach (var updateNovel in updatedNovels)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{updateNovel.Item1}) {updateNovel.novelName}");
            }
            Console.ResetColor();
            Console.WriteLine($"Failed novels: {failedToUpdate.Count}");
            if (failedToUpdate.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to update novels:");
                foreach (var failedNovel in failedToUpdate)
                {
                    Console.WriteLine($"{failedNovel.Item1}) {failedNovel.novelName}");
                }
                Console.ResetColor();
            }
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
            novels = novels.ToList();

            if (!novels.Any())
            {
                Console.WriteLine("No novels found.");
                return;
            }

            if (!string.IsNullOrEmpty(searchKeyWord))
                novels = novels.Where(novel =>
                    novel.Title.Contains(searchKeyWord, StringComparison.InvariantCultureIgnoreCase));
            if (!novels.Any())
            {
                Console.WriteLine($"No novel found with the search term '{searchKeyWord}'");
                return;
            }

            var paginatedNovels = novels.Skip((page - 1) * itemsPerPage).Take(itemsPerPage);
            var totalPages = (int)Math.Ceiling((double)novels.Count() / itemsPerPage);

            var maxNoLength = novels.Count().ToString().Length + 3;  // "3" accounts for ")."
            var maxIdLength = novels.Max(novel => novel.Id.ToString().Length);
            var maxChapterLength = novels.Max(novel => novel.CurrentChapter?.Length ?? 0);  // New line for max chapter length
            var maxFileTypeLength = novels.Max(novel => novel.FileType.ToString().Length + 3); // +3 for " []"
            var maxTitleLength = novels.Max(novel => Math.Min(novel.Title.Length, 60 - maxFileTypeLength)); // Adjusted for maxFileTypeLength

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"No.".PadRight(maxNoLength) +
                              "ID".PadRight(maxIdLength + 2) +
                              "Title [FileType]".PadRight(maxTitleLength + 2) +
                              "Current Chapter".PadRight(maxChapterLength + 2));
            Console.ResetColor();

            var count = 0;
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
            var logger = NLog.LogManager.GetCurrentClassLogger();
            var novelService = scope.Resolve<INovelService>();

            logger.Info("Clearing all novels and chapters from database");
            await novelService.RemoveAllAsync();
            logger.Info("Database cleared");
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
            var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
            configuration.DefaultMangaFileExtension = (FileExtension)extension;
            configurationRepository.Update(configuration);
            Console.WriteLine($"Default manga extension updated: {configuration.DefaultMangaFileExtension}");
        }

        private static async Task GetDefaultMangaExtensionAsync()
        {
            await using var scope = Container.BeginLifetimeScope();
            var configurationRepository = scope.Resolve<IConfigurationRepository>();
            var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
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
                var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
                var epubGenerator = scope.Resolve<IEpubGenerator>();
                var novel = await novelService.GetByIdAsync(id);
                if (novel != null)
                {
                    Logger.Info($"Recreating novel {novel.Title}. Id: {novel.Id}, Total Chapters: {novel.Chapters.Count}");
                    var chapters = CommonHelper.SortNovelChaptersByDateCreated(novel.Chapters);
                    var safeTitle = CommonHelper.SanitizeFileName(novel.Title, true);
                    var documentsFolder = CommonHelper.GetOutputDirectoryForTitle(safeTitle, configuration.DetermineSaveLocation());
                    Directory.CreateDirectory(documentsFolder);
                    var epubFile = Path.Combine(documentsFolder, $"{safeTitle}.epub");
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
                var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
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
                    var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
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
                    var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
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

        private static async Task SetNovelSaveLocationAsync(string saveLocation)
        {
            try
            {
                if (Directory.Exists(saveLocation))
                {
                    await using var scope = Container.BeginLifetimeScope();
                    var configurationRepository = scope.Resolve<IConfigurationRepository>();
                    var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
                    configuration.NovelSaveLocation = saveLocation;
                    configurationRepository.Update(configuration);
                    Console.WriteLine($"Novel save location updated: {configuration.NovelSaveLocation}");
                }
                else
                    Console.WriteLine($"Directory {saveLocation} does not exist.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to set novel save location. {ex.Message}");
            }
        }

        private static async Task SetSingleFileAsync(bool singleFile)
        {
            try
            {
                await using var scope = Container.BeginLifetimeScope();
                var configurationRepository = scope.Resolve<IConfigurationRepository>();
                var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
                configuration.SaveAsSingleFile = singleFile;
                configurationRepository.Update(configuration);
                Console.WriteLine(configuration.SaveAsSingleFile
                    ? "Single file mode enabled."
                    : "Single file mode disabled.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception when trying to set single file. {ex.Message}");
            }
        }

        /// <summary>
        /// Tests connectivity to a site by attempting to fetch the page and extract basic information.
        /// Useful for verifying Cloudflare bypass is working before implementing a full scraper strategy.
        /// </summary>
        private static async Task TestSiteConnectivityAsync(Uri testUri)
        {
            Console.WriteLine($"\n{'='}{new string('=', 60)}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Testing connectivity to: {testUri}");
            Console.ResetColor();
            Console.WriteLine($"{'='}{new string('=', 60)}\n");

            try
            {
                var novelScraperSettings = Configuration.GetSection("NovelScraperSettings").Get<NovelScraperSettings>();
                var siteConfig = novelScraperSettings?.SiteConfigurations?.FirstOrDefault(config => testUri.Host.Contains(config.UrlPattern));

                // Guard: Check for inactive site first
                if (siteConfig is { IsActive: false })
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("⊘ INACTIVE SITE");
                    Console.WriteLine("This site has been shut down or is no longer supported.");
                    Console.WriteLine("The configuration is kept for reference but the site cannot be used.");
                    Console.ResetColor();
                    Console.WriteLine($"\n{'='}{new string('=', 60)}\n");
                    return;
                }

                var httpClientFactory = new HttpClientFactory();
                var testStrategy = new TestStrategy(httpClientFactory);

                var (htmlDocument, updatedUri, statusCode, cloudflareDetected) = await testStrategy.TestLoadHtmlAsync(testUri);

                // Guard: If document loaded successfully, handle success path
                if (htmlDocument != null)
                {
                    HandleSuccessfulTestConnection(htmlDocument, testUri, updatedUri);
                    return;
                }

                // Failed to fetch - handle error cases
                HandleFailedTestConnection(statusCode, cloudflareDetected, siteConfig);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error during test: {ex.Message}");
                Console.WriteLine("Check the logs for detailed error information.");
                Console.ResetColor();
                Logger.Error($"Test site connectivity error: {ex}");
            }

            Console.WriteLine($"\n{'='}{new string('=', 60)}\n");
        }

        private static async Task RunInteractiveTestAsync(Uri testUri)
        {
            try
            {
                var httpClientFactory = new HttpClientFactory();
                var testStrategy = new TestStrategy(httpClientFactory);
                await testStrategy.RunInteractiveTestAsync(testUri);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error during interactive test: {ex.Message}");
                Console.ResetColor();
                Logger.Error($"Interactive test error: {ex}");
            }
        }

        private static async Task RunSingleFieldTestAsync(string testField, string url)
        {
            try
            {
                var parts = testField.Split(':', 2);
                if (parts.Length != 2)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Invalid format. Use: --test-field FieldName:\"XPath\" <URL>");
                    Console.ResetColor();
                    Console.WriteLine("\nExample:");
                    Console.WriteLine("  dotnet run --test-field ChapterLinks:\"//ul[@class='chapters']//a/@href\" https://example.com");
                    return;
                }

                var fieldName = parts[0].Trim();
                var xpath = parts[1].Trim();

                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri testUri))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Invalid URL provided");
                    Console.ResetColor();
                    return;
                }

                var httpClientFactory = new HttpClientFactory();
                var testStrategy = new TestStrategy(httpClientFactory);
                await testStrategy.TestSingleFieldAsync(testUri, fieldName, xpath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error during field test: {ex.Message}");
                Console.ResetColor();
                Logger.Error($"Field test error: {ex}");
            }
        }

        private static async Task ValidateSiteConfigAsync(string configName)
        {
            try
            {
                var novelScraperSettings = Configuration.GetSection("NovelScraperSettings").Get<NovelScraperSettings>();
                var siteConfig = novelScraperSettings?.SiteConfigurations?.FirstOrDefault(config =>
                    config.Name.Equals(configName, StringComparison.OrdinalIgnoreCase));

                if (siteConfig == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Configuration '{configName}' not found in appsettings.json");
                    Console.ResetColor();
                    Console.WriteLine("\nAvailable configurations:");
                    foreach (var config in novelScraperSettings?.SiteConfigurations ?? Enumerable.Empty<SiteConfiguration>())
                    {
                        Console.WriteLine($"  - {config.Name}");
                    }
                    return;
                }

                Console.Write("Enter test URL for this site: ");
                var urlInput = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(urlInput) || !Uri.TryCreate(urlInput, UriKind.Absolute, out Uri testUri))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Invalid URL provided");
                    Console.ResetColor();
                    return;
                }

                var httpClientFactory = new HttpClientFactory();
                var testStrategy = new TestStrategy(httpClientFactory);
                await testStrategy.ValidateConfigAsync(siteConfig, testUri);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error validating configuration: {ex.Message}");
                Console.ResetColor();
                Logger.Error($"Config validation error: {ex}");
            }
        }

        private static async Task ValidateAllSiteConfigsAsync()
        {
            try
            {
                var novelScraperSettings = Configuration.GetSection("NovelScraperSettings").Get<NovelScraperSettings>();
                var activeConfigs = novelScraperSettings?.SiteConfigurations?.Where(c => c.IsActive).ToList();

                if (activeConfigs == null || !activeConfigs.Any())
                {
                    Console.WriteLine("No active site configurations found.");
                    return;
                }

                Console.WriteLine($"\nFound {activeConfigs.Count} active site configurations");
                Console.WriteLine("This will test each site. You'll need to provide a test URL for each.\n");

                var httpClientFactory = new HttpClientFactory();
                var testStrategy = new TestStrategy(httpClientFactory);

                foreach (var siteConfig in activeConfigs)
                {
                    Console.WriteLine($"\n{new string('=', 70)}");
                    Console.WriteLine($"Site: {siteConfig.Name}");
                    Console.WriteLine($"{new string('=', 70)}");
                    Console.Write($"Enter test URL for {siteConfig.Name} (or press Enter to skip): ");
                    var urlInput = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(urlInput))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("⊘ Skipped");
                        Console.ResetColor();
                        continue;
                    }

                    if (!Uri.TryCreate(urlInput, UriKind.Absolute, out Uri testUri))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("✗ Invalid URL, skipping this site");
                        Console.ResetColor();
                        continue;
                    }

                    await testStrategy.ValidateConfigAsync(siteConfig, testUri);
                }

                Console.WriteLine($"\n{new string('=', 70)}");
                Console.WriteLine("Validation complete for all sites");
                Console.WriteLine($"{new string('=', 70)}\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error validating configurations: {ex.Message}");
                Console.ResetColor();
                Logger.Error($"Validate all configs error: {ex}");
            }
        }

        private static void HandleSuccessfulTestConnection(HtmlDocument htmlDocument, Uri testUri, Uri updatedUri)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Successfully reached the site!");
            Console.ResetColor();

            var titleNode = htmlDocument.DocumentNode.SelectSingleNode("//title");
            Console.WriteLine(titleNode != null
                ? $"Page Title: {titleNode.InnerText.Trim()}"
                : "Page Title: Not found");

            var descNode = htmlDocument.DocumentNode.SelectSingleNode("//meta[@name='description']");
            if (descNode?.Attributes["content"] != null)
            {
                var description = descNode.Attributes["content"].Value;
                if (description.Length > 100)
                    description = description.Substring(0, 100) + "...";
                Console.WriteLine($"Description: {description}");
            }

            if (updatedUri.ToString() != testUri.ToString())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Note: URL was redirected to: {updatedUri}");
                Console.ResetColor();
            }

            Console.WriteLine($"\nTotal HTML length: {htmlDocument.DocumentNode.InnerHtml.Length} characters");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✓ Test completed successfully! You should be able to implement a scraper for this site.");
            Console.ResetColor();
        }

        private static void HandleFailedTestConnection(int statusCode, bool cloudflareDetected, SiteConfiguration siteConfig)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("✗ Failed to fetch the page.");
            if (statusCode > 0)
            {
                Console.WriteLine($"HTTP Status Code: {statusCode}");
            }

            if (cloudflareDetected)
            {
                HandleCloudflareDetected(siteConfig);
                return;
            }

            // Handle site configured for JsChallenge but test succeeded
            if (siteConfig?.CloudflareProtection == CloudflareProtectionLevel.JsChallenge)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nNote: Site is configured for JsChallenge but test succeeded with HttpClient.");
                Console.WriteLine("Cloudflare protection may have been reduced or removed.");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("\nRECOMMENDATION:");
                Console.WriteLine($"  Consider updating appsettings.json for '{siteConfig.Name}':");
                Console.WriteLine("  Change \"cloudflareProtection\": \"jschallenge\" to \"detected\" or null");
                Console.WriteLine("  Set \"entireSiteRequiresSelenium\": false (if not needed)");
                Console.ResetColor();
                return;
            }

            if (siteConfig?.CloudflareProtection == CloudflareProtectionLevel.Detected)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nNote: Site has Cloudflare 'Detected' - HttpClient working as expected.");
                Console.ResetColor();
                return;
            }

            if (statusCode == 404)
                Console.WriteLine("Page not found.");
            else if (statusCode >= 500)
                Console.WriteLine("Server error.");
            else
                Console.WriteLine("This site may require additional bypass techniques or Selenium.");

            Console.ResetColor();
        }

        private static void HandleCloudflareDetected(SiteConfiguration siteConfig)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nCloudflare Protection Detected:");
            Console.WriteLine("  This site is protected by Cloudflare and is blocking HttpClient requests.");
            Console.ResetColor();

            if (siteConfig == null)
                return;

            switch (siteConfig.CloudflareProtection)
            {
                // Handle JsChallenge configuration
                case CloudflareProtectionLevel.JsChallenge:
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("  Configuration matches - site is configured for JsChallenge protection.");
                        Console.ResetColor();

                        if (siteConfig.EntireSiteRequiresSelenium) return;
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("\nRECOMMENDATION:");
                        Console.WriteLine($"  Set \"entireSiteRequiresSelenium\": true for '{siteConfig.Name}' in appsettings.json");
                        Console.WriteLine("  (Selenium will be used automatically based on CloudflareProtection, but explicit setting is clearer)");
                        Console.ResetColor();
                        return;
                    }
                // Handle Detected level (needs escalation)
                case CloudflareProtectionLevel.Detected:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  Site is configured as 'Detected' but is actually blocking requests.");
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("\nACTION REQUIRED:");
                    Console.WriteLine($"  Please update appsettings.json for '{siteConfig.Name}':");
                    Console.WriteLine("  Change \"cloudflareProtection\": \"detected\" to \"jschallenge\"");
                    Console.WriteLine("  Set \"entireSiteRequiresSelenium\": true");
                    Console.ResetColor();
                    return;
                // Handle no Cloudflare protection configured
                case null:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("\nACTION REQUIRED:");
                    Console.WriteLine($"  Please update appsettings.json for '{siteConfig.Name}':");
                    Console.WriteLine("  Set \"cloudflareProtection\": \"jschallenge\"");
                    Console.WriteLine("  Set \"entireSiteRequiresSelenium\": true");
                    Console.ResetColor();
                    break;
            }
        }

        /// <summary>
        /// Tests connectivity to all supported sites configured in the application.
        /// </summary>
        private static async Task TestAllSitesAsync()
        {
            HttpNovelScraper httpNovelScraper = new();
            var supportedSites = httpNovelScraper.GetSupportedSites();

            var novelScraperSettings = Configuration.GetSection("NovelScraperSettings").Get<NovelScraperSettings>();

            Console.WriteLine($"\n{'='}{new string('=', 60)}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Testing connectivity to all {supportedSites.Count} supported sites");
            Console.ResetColor();
            Console.WriteLine($"{'='}{new string('=', 60)}\n");

            var results = new List<(string site, bool success, string error, bool cloudflareDetected, bool needsConfigUpdate, bool isInactive)>();

            foreach (var site in supportedSites)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Testing: {site}");
                Console.ResetColor();

                try
                {
                    // Guard: Validate URI
                    if (!Uri.TryCreate(site, UriKind.Absolute, out Uri testUri))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  ✗ FAILED - Invalid URI");
                        Console.ResetColor();
                        results.Add((site, false, "Invalid URI", false, false, false));
                        Console.WriteLine();
                        await Task.Delay(500);
                        continue;
                    }

                    var siteConfig = novelScraperSettings?.SiteConfigurations?.FirstOrDefault(config => testUri.Host.Contains(config.UrlPattern));

                    // Guard: Check for inactive site
                    if (siteConfig is { IsActive: false })
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"  ⊘ INACTIVE - Site has been shut down or is no longer supported");
                        Console.ResetColor();
                        results.Add((site, false, "Site inactive", false, false, true));
                        Console.WriteLine();
                        await Task.Delay(500);
                        continue;
                    }

                    var httpClientFactory = new HttpClientFactory();
                    var testStrategy = new TestStrategy(httpClientFactory);

                    var (htmlDocument, _, statusCode, cloudflareDetected) = await testStrategy.TestLoadHtmlAsync(testUri);

                    var needsConfigUpdate = cloudflareDetected && siteConfig != null && !siteConfig.CloudflareProtection.HasValue;

                    // Guard: Handle successful connection
                    if (htmlDocument != null)
                    {
                        var titleNode = htmlDocument.DocumentNode.SelectSingleNode("//title");
                        var title = titleNode != null ? titleNode.InnerText.Trim() : "No title";

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  ✓ SUCCESS ({statusCode}) - Title: {(title.Length > 50 ? title.Substring(0, 50) + "..." : title)}");
                        Console.ResetColor();

                        results.Add((site, true, "", false, false, false));
                        Console.WriteLine();
                        await Task.Delay(500);
                        continue;
                    }

                    // Handle failed connection
                    Console.ForegroundColor = ConsoleColor.Red;
                    var errorMsg = statusCode > 0 ? $"HTTP {statusCode}" : "Connection failed";

                    if (cloudflareDetected)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  ✗ FAILED - {errorMsg} (Cloudflare detected)");

                        if (siteConfig.CloudflareProtection == CloudflareProtectionLevel.Detected)
                        {
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"    → Escalate: Change \"cloudflareProtection\": \"detected\" to \"jschallenge\" for '{siteConfig.Name}'");
                        }
                        else if (needsConfigUpdate)
                        {
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"    → Update appsettings.json: Set \"cloudflareProtection\": \"jschallenge\" for '{siteConfig.Name}'");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ FAILED - {errorMsg}");
                    }
                    Console.ResetColor();

                    results.Add((site, false, errorMsg, cloudflareDetected: cloudflareDetected, needsConfigUpdate, false));
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ FAILED - {ex.Message}");
                    Console.ResetColor();

                    results.Add((site, false, ex.Message, false, false, false));
                }

                Console.WriteLine();

                // Small delay between requests to avoid rate limiting
                await Task.Delay(500);
            }

            // Summary
            Console.WriteLine($"{'='}{new string('=', 60)}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("TEST SUMMARY");
            Console.ResetColor();
            Console.WriteLine($"{'='}{new string('=', 60)}\n");

            var successCount = results.Count(r => r.success);
            var inactiveCount = results.Count(r => r.isInactive);
            var failCount = results.Count(r => r is { success: false, isInactive: false });
            var cloudflareBlockedCount = results.Count(r => r is { success: false, cloudflareDetected: true, isInactive: false });
            var needsConfigUpdateCount = results.Count(r => r.needsConfigUpdate);

            Console.WriteLine($"Total Sites: {results.Count}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Successful: {successCount}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed: {failCount}");
            Console.ResetColor();

            if (inactiveCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Inactive: {inactiveCount}");
                Console.ResetColor();
            }

            if (cloudflareBlockedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Cloudflare Blocked: {cloudflareBlockedCount}");
                Console.ResetColor();
            }

            if (needsConfigUpdateCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"Needs Config Update: {needsConfigUpdateCount}");
                Console.ResetColor();
            }

            if (inactiveCount > 0)
            {
                Console.WriteLine("\nInactive Sites (shut down or no longer supported):");
                foreach (var result in results.Where(r => r.isInactive))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  - {result.site}");
                    Console.ResetColor();
                }
            }

            if (cloudflareBlockedCount > 0)
            {
                Console.WriteLine("\nCloudflare Protected Sites (require Selenium):");
                foreach (var result in results.Where(r => !r.success && r.cloudflareDetected))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  - {result.site}");
                    Console.ResetColor();
                    if (!string.IsNullOrEmpty(result.error))
                        Console.WriteLine($"    Status: {result.error}");
                    if (result.needsConfigUpdate)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"    → Needs appsettings.json update");
                        Console.ResetColor();
                    }
                }
            }

            var otherFailCount = results.Count(r => !r.success && !r.cloudflareDetected && !r.isInactive);
            if (otherFailCount > 0)
            {
                Console.WriteLine("\nOther Failed Sites:");
                foreach (var result in results.Where(r => !r.success && !r.cloudflareDetected && !r.isInactive))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  - {result.site}");
                    Console.ResetColor();
                    if (!string.IsNullOrEmpty(result.error))
                        Console.WriteLine($"    Error: {result.error}");
                }
            }

            if (successCount > 0)
            {
                Console.WriteLine("\nSuccessful Sites:");
                foreach (var result in results.Where(r => r.success))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  - {result.site}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine($"\n{'='}{new string('=', 60)}\n");
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
                    var newSaveLocation = Console.ReadLine();
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
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var directoryPath = Path.Combine(appDataPath, "BennyScraper", "Database");
                var oldDbPath = GetConnectionString(); // Assuming this returns the full path
                var newDbPath = Path.Combine(directoryPath, newDbName);

                // Rename the physical file
                File.Move(oldDbPath, newDbPath);

                // Update the configuration table
                await using var scope = Container.BeginLifetimeScope();
                var configurationRepository = scope.Resolve<IConfigurationRepository>();
                var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
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
            await using var scope = Container.BeginLifetimeScope();
            var novelService = scope.Resolve<INovelService>();

            var novel = await novelService.GetByIdAsync(novelId);
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
                Console.WriteLine($"{detail.Key,-20} {detail.Value}"); // Align keys to the left with a width of 20, used -20 instead of .PadRight(20)
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
                    var fileType = Console.ReadLine();

                    if (int.TryParse(fileType, out var fileTypeInt) && Enum.IsDefined(typeof(NovelFileType), fileTypeInt))
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

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var directoryPath = Path.Combine(appDataPath, "BennyScraper", "logs");

            var logPath = Path.Combine(directoryPath, $"log-book {DateTime.Now:MM-dd-yyyy}.log");
            var logfile = new FileTarget("logfile") { FileName = logPath };

            var logConsole = new ColoredConsoleTarget("logconsole")
            {
                Layout = @"${date:format=HH\:mm\:ss} ${level} ${message} ${exception}"
            };

            logConsole.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Info"),
                ConsoleOutputColor.Green, ConsoleOutputColor.Black));
            logConsole.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Warn"),
                ConsoleOutputColor.DarkYellow, ConsoleOutputColor.Black));
            logConsole.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Error"),
                ConsoleOutputColor.Red, ConsoleOutputColor.Black));
            logConsole.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
                NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Fatal"),
                ConsoleOutputColor.White, ConsoleOutputColor.Red));

            config.AddRule(logLevel, LogLevel.Fatal, logConsole);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);

            NLog.LogManager.Configuration = config;
        }

        private static void DeleteOldLogs()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var directoryPath = Path.Combine(appDataPath, "BennyScraper", "logs");
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
        private static void ConfigureServices(ContainerBuilder builder)
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

            // Centralized HttpClient creation (shared handler, per-call HttpClient instances)
            builder.RegisterType<HttpClientFactory>().As<IHttpClientFactory>().SingleInstance();

            // Centralized Selenium driver factory (so all drivers can be disposed on shutdown)
            builder.RegisterType<DriverFactory>().As<IDriverFactory>().SingleInstance();

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
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var directoryPath = Path.Combine(appDataPath, "BennyScraper", "Database");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var dbPath = Path.Combine(directoryPath, "BennyTestDb.db");
            var connectionString = $"Data Source={dbPath};";
            return connectionString;
        }
        #endregion
    }
}

