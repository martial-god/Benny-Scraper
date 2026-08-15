// ReSharper disable LocalizableElement
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Autofac;
using BennyScraper.BusinessLogic;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Extensions;
using BennyScraper.BusinessLogic.Factory;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.FileGenerators;
using BennyScraper.BusinessLogic.FileGenerators.Interfaces;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Interfaces;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using BennyScraper.BusinessLogic.Services;
using BennyScraper.BusinessLogic.Services.Interfaces;
using BennyScraper.BusinessLogic.Utilities;
using BennyScraper.DataAccess.Data;
using BennyScraper.DataAccess.DbInitializer;
using BennyScraper.DataAccess.Repository;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;
using CommandLine;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

namespace BennyScraper;

internal static class Program
{
    private const int _defaultConfigId = 1;
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    private static IContainer? Container { get; set; }

    private static IConfiguration? Configuration { get; set; }

    private static NovelScraperSettings? NovelScraperSettings { get; set; }

    // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"
    private static async Task Main(string[] args)
    {
        SetupLogger(LogLevel.Info);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        SQLitePCL.Batteries.Init();
        var configuration = BuildConfiguration();
        Configuration = configuration;
        NovelScraperSettings = BuildNovelScraperSettings(configuration);

        var builder = new ContainerBuilder();
        ConfigureServices(builder, configuration, NovelScraperSettings);
        Container = builder.Build();

        // Ensure Selenium drivers (and other unmanaged resources) are disposed even on crash/exit.
        ShutdownHooks.Register(Container!.Resolve<IDriverFactory>());

        await using var scope = Container!.BeginLifetimeScope();
        var dbInitializer = scope.Resolve<DbInitializer>();
        var dbChangesMade = dbInitializer.Initialize();

        if (dbChangesMade)
        {
            _logger.Info("Database Initialized");
        }

        if (args.Length > 0)
        {
            await RunAsync(args);
        }
        else
        {
            _logger.Info("Application Started");
            await RunAsync(Container);
        }
    }

    private static async Task RunAsync(IContainer container)
    {
        await using var scope = container.BeginLifetimeScope();
        var logger = NLog.LogManager.GetCurrentClassLogger();

        // Display supported sites with ASCII art
        DisplaySupportedSites();

        // Display instructions
        var howToMessages = new[] { "HOW TO USE" };
        CommonHelper.DrawBox(howToMessages, ConsoleColor.Cyan);
        Console.WriteLine();
        Console.WriteLine("  1. Visit a supported website above");
        Console.WriteLine("  2. Choose a novel and navigate to its table of contents page");
        Console.WriteLine("  3. Copy the URL from your browser's address bar");
        Console.WriteLine("  4. Paste the URL below when prompted");
        Console.WriteLine();
        var specialCommandsMessages = new[] { "SPECIAL COMMANDS" };
        CommonHelper.DrawBox(specialCommandsMessages, ConsoleColor.Yellow);
        Console.WriteLine();
        Console.WriteLine("  • test <url>     - Test if a site is reachable before scraping");
        Console.WriteLine("  • test-all       - Test connectivity to all supported sites");
        Console.WriteLine("  • exit           - Quit the application");
        Console.WriteLine();
        Console.WriteLine(new string('─', 78));
        Console.WriteLine();

        // Test all sites on startup to give users immediate feedback
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Testing connectivity to all supported sites...");
        Console.ResetColor();
        Console.WriteLine();
        await TestAllSitesAsync();

        var novelProcessor = scope.Resolve<INovelProcessor>();

        var isApplicationRunning = true;
        while (isApplicationRunning)
        {
            // Uri help https://www.dotnetperls.com/uri#:~:text=URI%20stands%20for%20Universal%20Resource,strings%20starting%20with%20%22http.%22
            Console.WriteLine("\nEnter the site url (or 'exit' to quit): ");
            var siteUrl = Console.ReadLine()?.Trim() ?? string.Empty;
            var input = siteUrl.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (string.IsNullOrWhiteSpace(siteUrl))
            {
                Console.WriteLine("Invalid input. Please enter a valid URL.");
                continue;
            }

            if (string.Equals(siteUrl, "exit", StringComparison.OrdinalIgnoreCase))
            {
                isApplicationRunning = false;
                continue;
            }

            // Check if user wants to test site connectivity
            if (input.Length >= 2 && string.Equals(input[0], "test", StringComparison.OrdinalIgnoreCase))
            {
                var testUrl = input[1];
                if (Uri.TryCreate(testUrl, UriKind.Absolute, out var testUri))
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
            if (string.Equals(siteUrl, "test-all", StringComparison.OrdinalIgnoreCase))
            {
                await TestAllSitesAsync();
                continue;
            }

            if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out Uri? novelTableOfContentUri))
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
            catch (InvalidOperationException ex) when (ex.Message.Contains("Chrome browser version mismatch", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Error($"Chrome version mismatch: {ex.Message}");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(new string('═', 78));
                Console.WriteLine("  CHROME VERSION MISMATCH DETECTED");
                Console.WriteLine(new string('═', 78));
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Please update Google Chrome to the latest version:");
                Console.WriteLine();
                Console.WriteLine("    1. Open Chrome");
                Console.WriteLine("    2. Click the menu (three dots) → Help → About Google Chrome");
                Console.WriteLine("    3. Chrome will automatically update");
                Console.WriteLine("    4. Restart Chrome, then run this application again");
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(new string('═', 78));
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                _logger.Error($"Exception when trying to process novel. {ex}");
            }

            stopwatch.Stop();
            var elapsedTime = stopwatch.Elapsed;
            _logger.Info($"Elapsed time: {elapsedTime}");
        }
    }

    private static async Task RunAsync(string[] args)
    {
        var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
        await result.MapResult(
            async options => await HandleOptionsAsync(options),
            HandleParseErrors);
    }

    private static async Task HandleOptionsAsync(CommandLineOptions options)
    {
        if (options.List)
        {
            await ListNovelsAsync(options.Page, options.ItemsPerPage, options.SearchKeyword);
        }
        else if (options.ExtensionType)
        {
            await GetDefaultMangaExtensionAsync();
        }
        else if (options.ClearDatabase)
        {
            Console.WriteLine("Are you sure you want to clear the database? (y/n)");
            var confirmation = Console.ReadLine();
            if (confirmation?.ToLowerInvariant() == "y")
            {
                await ClearDatabaseAsync();
            }
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
        else if (options.GetConcurrent)
        {
            await GetConcurrentRequestsAsync();
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
        else if (options.MangaExtension >= 0 && options.MangaExtension < Enum.GetNames<FileExtension>().Length)
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
            if (Uri.TryCreate(options.TestSite, UriKind.Absolute, out Uri? testUri))
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
        else if (options.ListFields)
        {
            DisplayTestableFields();
        }
        else if (options.SupportedSites)
        {
            DisplaySupportedSites();
        }
        else if (!string.IsNullOrEmpty(options.TestInteractive))
        {
            if (Uri.TryCreate(options.TestInteractive, UriKind.Absolute, out Uri? testUri))
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
            await RunSingleFieldTestAsync(options.TestField, options.Url, options.UseSelenium, !options.ShowBrowser);
        }
        else if (!string.IsNullOrEmpty(options.ValidateConfig))
        {
            await ValidateSiteConfigAsync(options.ValidateConfig);
        }
        else if (options.ValidateAllConfigs)
        {
            await ValidateAllSiteConfigsAsync();
        }
        else if (options.RetryFailedById != Guid.Empty)
        {
            await RetryFailedChaptersAsync(options.RetryFailedById, options.WithLogin);
        }
        else if (options.RetryAllFailed)
        {
            await RetryAllFailedChaptersAsync(options.WithLogin);
        }
        else if (!string.IsNullOrEmpty(options.Url))
        {
            // Handle URL with optional chapter range
            if (Uri.TryCreate(options.Url, UriKind.Absolute, out var novelUri))
            {
                await using var scope = Container!.BeginLifetimeScope();
                var novelProcessor = scope.Resolve<INovelProcessor>();
                try
                {
                    await novelProcessor.ProcessNovelAsync(novelUri, options.BeginChapter, options.EndChapter, options.WithLogin);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Chrome browser version mismatch", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Error($"Chrome version mismatch: {ex.Message}");
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(new string('═', 78));
                    Console.WriteLine("  CHROME VERSION MISMATCH DETECTED");
                    Console.WriteLine(new string('═', 78));
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  Please update Google Chrome to the latest version:");
                    Console.WriteLine();
                    Console.WriteLine("    1. Open Chrome");
                    Console.WriteLine("    2. Click the menu (three dots) → Help → About Google Chrome");
                    Console.WriteLine("    3. Chrome will automatically update");
                    Console.WriteLine("    4. Restart Chrome, then run this application again");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(new string('═', 78));
                    Console.ResetColor();
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    _logger.Error($"Exception when trying to process novel with chapter range. {ex}");
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
        {
            Console.WriteLine("Invalid command or a parameter is missing. Please try again.");
        }
    }

    private static async Task UpdateAllNovelsAsync(CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return;
        }

        var scope = Container!.BeginLifetimeScope();
        var novelService = scope.Resolve<INovelService>();
        var novelProcessor = scope.Resolve<INovelProcessor>();
        var updatedNovels = new List<(int, string NovelName)>();
        var failedToUpdate = new List<(int, string NovelName)>();
        var novels = await novelService.GetAllAsync();
        var nonCompletedNovels = novels.Where(novel => !novel.LastChapter).ToList(); // issue with mangareader.to

        // change default log level to error
        SetupLogger(LogLevel.Error);
        var count = 0;
        foreach (var novel in nonCompletedNovels)
        {
            if (cancellation.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await novelProcessor.ProcessNovelAsync(new Uri(novel.Url));
                ++count;
                updatedNovels.Add((count, novel.Title));
            }
            catch (Exception ex)
            {
                _logger.Error($"Exception when trying to update novel. {ex.Message}");
                failedToUpdate.Add((count, novel.Title));
            }
        }

        Console.WriteLine("\nCompleted novels: " + updatedNovels.Count + $"/{nonCompletedNovels.Count}");
        foreach (var updateNovel in updatedNovels)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{updateNovel.Item1}) {updateNovel.NovelName}");
        }

        Console.ResetColor();
        Console.WriteLine($"Failed novels: {failedToUpdate.Count}");
        if (failedToUpdate.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed to update novels:");
            foreach (var failedNovel in failedToUpdate)
            {
                Console.WriteLine($"{failedNovel.Item1}) {failedNovel.NovelName}");
            }

            Console.ResetColor();
        }
    }

    private static Task HandleParseErrors(IEnumerable<Error> errors)
    {
        var parseErrors = errors.Where(error =>
            error is not HelpRequestedError and
            not HelpVerbRequestedError and
            not VersionRequestedError);

        foreach (var error in parseErrors)
        {
            Console.WriteLine($"Error: {error}");
        }

        return Task.CompletedTask;
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

        await using var scope = Container!.BeginLifetimeScope();
        var novelService = scope.Resolve<INovelService>();

        var novels = await novelService.GetAllAsync();
        novels = novels.ToList();

        if (!novels.Any())
        {
            Console.WriteLine("No novels found.");
            return;
        }

        if (!string.IsNullOrEmpty(searchKeyWord))
        {
            novels = novels.Where(novel =>
                novel.Title.Contains(searchKeyWord, StringComparison.InvariantCultureIgnoreCase)).ToList();
        }

        if (!novels.Any())
        {
            Console.WriteLine($"No novel found with the search term '{searchKeyWord}'");
            return;
        }

        var paginatedNovels = novels.Skip((page - 1) * itemsPerPage).Take(itemsPerPage);
        var totalPages = (int)Math.Ceiling((double)novels.Count() / itemsPerPage);

        var maxNoLength = novels.Count().ToString(CultureInfo.InvariantCulture).Length + 3;  // "3" accounts for ")."
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
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            else
            {
                Console.ResetColor();
            }

            Console.WriteLine($"{countStr}{idStr}  {titleStr}  {chapterStr}");
            if (novel.LastChapter)
            {
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {novels.Count()}   Novels Completed: {novels.Count(novel => novel.LastChapter)}");

        if (totalPages == 1)
        {
            return;
        }

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
        {
            return title;
        }

        return string.Concat(title.AsSpan(0, maxLength - 3), "..."); // -3 to account for "..."
    }

    private static async Task ClearDatabaseAsync()
    {
        await using var scope = Container!.BeginLifetimeScope();
        var logger = NLog.LogManager.GetCurrentClassLogger();
        var novelService = scope.Resolve<INovelService>();

        logger.Info("Clearing all novels and chapters from database");
        await novelService.RemoveAllAsync();
        logger.Info("Database cleared");
    }

    private static async Task DeleteNovelByIdAsync(Guid id)
    {
        await using var scope = Container!.BeginLifetimeScope();
        var novelService = scope.Resolve<INovelService>();
        var novel = await novelService.GetByIdAsync(id);
        if (novel == null)
        {
            Console.WriteLine($"Novel with id: {id} not found.");
        }
        else
        {
            _logger.Info($"Deleting novel {novel.Title} with id {id}");
            await novelService.RemoveByIdAsync(id);
            _logger.Info($"Novel with id: {id} deleted.");
        }
    }

    private static async Task SetDefaultMangaExtensionAsync(int extension)
    {
        var totalExtensions = Enum.GetNames<FileExtension>().Length;
        if (extension > totalExtensions)
        {
            Console.WriteLine("Invalid extension. Please enter a value between 1 and " + totalExtensions);
        }

        await using var scope = Container!.BeginLifetimeScope();
        var configurationRepository = scope.Resolve<IConfigurationRepository>();
        var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId);
        configuration.DefaultMangaFileExtension = (FileExtension)extension;
        configurationRepository.Update(configuration);
        Console.WriteLine($"Default manga extension updated: {configuration.DefaultMangaFileExtension}");
    }

    private static async Task GetDefaultMangaExtensionAsync()
    {
        await using var scope = Container!.BeginLifetimeScope();
        var configurationRepository = scope.Resolve<IConfigurationRepository>();
        var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId);
        var extensions = Enum.GetValues<FileExtension>().ToList();
        Console.WriteLine($"Default manga extension: {configuration.DefaultMangaFileExtension}");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"Available extensions: {string.Join(", ", extensions)}");
        Console.ResetColor();
    }

    private static async Task RecreateEpubByIdAsync(Guid id)
    {
        try
        {
            await using var scope = Container!.BeginLifetimeScope();
            var configurationRepository = scope.Resolve<IConfigurationRepository>();
            var novelService = scope.Resolve<INovelService>();
            var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId);
            var epubGenerator = scope.Resolve<IEpubGenerator>();
            var novel = await novelService.GetByIdAsync(id);
            if (novel != null)
            {
                _logger.Info($"Recreating novel {novel.Title}. Id: {novel.Id}, Total Chapters: {novel.Chapters.Count}");
                var chapters = CommonHelper.SortNovelChaptersByNumber(novel.Chapters).ToList();
                var safeTitle = CommonHelper.SanitizeFileName(novel.Title, true);
                var documentsFolder = CommonHelper.GetOutputDirectoryForTitle(safeTitle, configuration.DetermineSaveLocation());
                Directory.CreateDirectory(documentsFolder);
                var epubFile = Path.Combine(documentsFolder, $"{safeTitle}.epub");
                epubGenerator.CreateEpub(novel, chapters, epubFile, null);
            }
            else
            {
                _logger.Error($"Novel with id {id} not found.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when trying to recreate novel. {ex.Message}");
        }
    }

    private static async Task GetConcurrentRequestsAsync()
    {
        await using var scope = Container!.BeginLifetimeScope();
        var configurationRepository = scope.Resolve<IConfigurationRepository>();
        var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId);
        Console.WriteLine($"Concurrent request limit: {configuration.ConcurrencyLimit}");
    }

    private static async Task SetConcurrentRequestsAsync(int concurrentRequests)
    {
        try
        {
            await using var scope = Container!.BeginLifetimeScope();
            var configurationRepository = scope.Resolve<IConfigurationRepository>();
            var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId);
            configuration.ConcurrencyLimit = concurrentRequests;
            configurationRepository.Update(configuration);
            Console.WriteLine($"Concurrent requests updated: {configuration.ConcurrencyLimit}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when trying to set concurrent requests. {ex.Message}");
        }
    }

    private static async Task SetSaveLocationAsync(string saveLocation)
    {
        try
        {
            if (Directory.Exists(saveLocation))
            {
                await using var scope = Container!.BeginLifetimeScope();
                var configurationRepository = scope.Resolve<IConfigurationRepository>();
                var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId);
                configuration.SaveLocation = saveLocation;
                configurationRepository.Update(configuration);
                Console.WriteLine($"Save location updated: {configuration.SaveLocation}");
            }
            else
            {
                Console.WriteLine($"Directory {saveLocation} does not exist.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when trying to set save location. {ex.Message}");
        }
    }

    private static async Task SetMangaSaveLocationAsync(string saveLocation)
    {
        try
        {
            if (Directory.Exists(saveLocation))
            {
                await using var scope = Container!.BeginLifetimeScope();
                var configurationRepository = scope.Resolve<IConfigurationRepository>();
                var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId);
                configuration.MangaSaveLocation = saveLocation;
                configurationRepository.Update(configuration);
                Console.WriteLine($"Manga save location updated: {configuration.MangaSaveLocation}");
            }
            else
            {
                Console.WriteLine($"Directory {saveLocation} does not exist.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when trying to set manga save location. {ex.Message}");
        }
    }

    private static async Task SetNovelSaveLocationAsync(string saveLocation)
    {
        try
        {
            if (Directory.Exists(saveLocation))
            {
                await using var scope = Container!.BeginLifetimeScope();
                var configurationRepository = scope.Resolve<IConfigurationRepository>();
                var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId);
                configuration.NovelSaveLocation = saveLocation;
                configurationRepository.Update(configuration);
                Console.WriteLine($"Novel save location updated: {configuration.NovelSaveLocation}");
            }
            else
            {
                Console.WriteLine($"Directory {saveLocation} does not exist.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when trying to set novel save location. {ex.Message}");
        }
    }

    private static async Task SetSingleFileAsync(bool singleFile)
    {
        try
        {
            await using var scope = Container!.BeginLifetimeScope();
            var configurationRepository = scope.Resolve<IConfigurationRepository>();
            var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId);
            configuration.SaveAsSingleFile = singleFile;
            configurationRepository.Update(configuration);
            Console.WriteLine(configuration.SaveAsSingleFile
                ? "Single file mode enabled."
                : "Single file mode disabled.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when trying to set single file. {ex.Message}");
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
            var siteConfig = NovelScraperSettings?.SiteConfigurations
                .FirstOrDefault(config => testUri.Host.Contains(config.UrlPattern, StringComparison.OrdinalIgnoreCase));

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

            using var httpClientFactory = new HttpClientFactory();
            using var testStrategy = new TestStrategy(httpClientFactory);
            await ConfigureFlareSolverrForTestingAsync(testStrategy);

            var (htmlDocument, updatedUri, statusCode, cloudflareDetected) = await testStrategy.TestLoadHtmlAsync(testUri);

            // Guard: If document loaded successfully, handle success path
            if (htmlDocument != null)
            {
                HandleSuccessfulTestConnection(
                    htmlDocument,
                    testUri,
                    updatedUri,
                    testStrategy.LastRequestUsedFlareSolverr);
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
            _logger.Error($"Test site connectivity error: {ex}");
        }

        Console.WriteLine($"\n{'='}{new string('=', 60)}\n");
    }

    private static async Task RunInteractiveTestAsync(Uri testUri)
    {
        try
        {
            using var httpClientFactory = new HttpClientFactory();
            using var testStrategy = new TestStrategy(httpClientFactory);
            await ConfigureFlareSolverrForTestingAsync(testStrategy);

            await testStrategy.RunInteractiveTestAsync(testUri);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error during interactive test: {ex.Message}");
            Console.ResetColor();
            _logger.Error($"Interactive test error: {ex}");
        }
    }

    private static async Task RunSingleFieldTestAsync(string testField, string url, bool useSelenium, bool headless)
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

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? testUri))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Invalid URL provided");
                Console.ResetColor();
                return;
            }

            using var httpClientFactory = new HttpClientFactory();
            var driverFactory = new DriverFactory();
            using var testStrategy = new TestStrategy(httpClientFactory, driverFactory);
            await testStrategy.TestSingleFieldAsync(testUri, fieldName, xpath, useSelenium, headless);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error during field test: {ex.Message}");
            Console.ResetColor();
            _logger.Error($"Field test error: {ex}");
        }
    }

    private static async Task ValidateSiteConfigAsync(string configName)
    {
        try
        {
            var novelScraperSettings = NovelScraperSettings;
            var siteConfig = novelScraperSettings?.SiteConfigurations.FirstOrDefault(config =>
                config.SiteName.Equals(configName, StringComparison.OrdinalIgnoreCase));

            if (siteConfig == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Configuration '{configName}' was not found in the sites directory");
                Console.ResetColor();
                Console.WriteLine("\nAvailable configurations:");
                foreach (var config in novelScraperSettings?.SiteConfigurations ?? Enumerable.Empty<SiteConfiguration>())
                {
                    Console.WriteLine($"  - {config.SiteName}");
                }

                return;
            }

            Console.Write("Enter test URL for this site: ");
            var urlInput = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(urlInput) || !Uri.TryCreate(urlInput, UriKind.Absolute, out Uri? testUri))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Invalid URL provided");
                Console.ResetColor();
                return;
            }

            using var httpClientFactory = new HttpClientFactory();
            using var testStrategy = new TestStrategy(httpClientFactory);
            await testStrategy.ValidateConfigAsync(siteConfig, testUri);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error validating configuration: {ex.Message}");
            Console.ResetColor();
            _logger.Error($"Config validation error: {ex}");
        }
    }

    private static async Task ValidateAllSiteConfigsAsync()
    {
        try
        {
            var novelScraperSettings = NovelScraperSettings;
            var activeConfigs = novelScraperSettings?.SiteConfigurations.Where(siteConfiguration => siteConfiguration.IsActive).ToList();

            if (activeConfigs == null || activeConfigs.Count == 0)
            {
                Console.WriteLine("No active site configurations found.");
                return;
            }

            Console.WriteLine($"\nFound {activeConfigs.Count} active site configurations");
            Console.WriteLine("This will test each site. You'll need to provide a test URL for each.\n");

            using var httpClientFactory = new HttpClientFactory();
            using var testStrategy = new TestStrategy(httpClientFactory);

            foreach (var siteConfig in activeConfigs)
            {
                Console.WriteLine($"\n{new string('=', 70)}");
                Console.WriteLine($"Site: {siteConfig.SiteName}");
                Console.WriteLine($"{new string('=', 70)}");
                Console.Write($"Enter test URL for {siteConfig.SiteName} (or press Enter to skip): ");
                var urlInput = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(urlInput))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("⊘ Skipped");
                    Console.ResetColor();
                    continue;
                }

                if (!Uri.TryCreate(urlInput, UriKind.Absolute, out Uri? testUri))
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
            _logger.Error($"Validate all configs error: {ex}");
        }
    }

    private static async Task ConfigureFlareSolverrForTestingAsync(TestStrategy testStrategy)
    {
        var flareSolverrSettings = NovelScraperSettings?.FlareSolverrSettings;
        if (flareSolverrSettings?.Enabled == true)
        {
            await testStrategy.EnableFlareSolverrAsync(flareSolverrSettings.Url);
        }
    }

    private static void HandleSuccessfulTestConnection(
        HtmlDocument htmlDocument,
        Uri testUri,
        Uri updatedUri,
        bool usedFlareSolverr)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Successfully reached the site!");
        Console.ResetColor();

        if (usedFlareSolverr)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("FlareSolverr was needed to bypass this site's Cloudflare protection.");
            Console.WriteLine("Users must have FlareSolverr running to access this site the same way.");
            Console.ResetColor();
        }

        var titleNode = htmlDocument.DocumentNode.SelectSingleNode("//title");
        Console.WriteLine(titleNode != null
            ? $"Page Title: {titleNode.InnerText.Trim()}"
            : "Page Title: Not found");

        var descNode = htmlDocument.DocumentNode.SelectSingleNode("//meta[@name='description']");
        if (descNode?.Attributes["content"] != null)
        {
            var description = descNode.Attributes["content"].Value;
            if (description.Length > 100)
            {
                description = $"{description[..100]}...";
            }

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

    private static void HandleFailedTestConnection(int statusCode, bool cloudflareDetected, SiteConfiguration? siteConfig)
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
            Console.WriteLine($"  Consider updating the site configuration for '{siteConfig.SiteName}':");
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

        switch (statusCode)
        {
            case 404:
                Console.WriteLine("Page not found.");
                break;
            case >= 500:
                Console.WriteLine("Server error.");
                break;
            default:
                Console.WriteLine("This site may require additional bypass techniques or Selenium.");
                break;
        }

        Console.ResetColor();
    }

    private static void HandleCloudflareDetected(SiteConfiguration? siteConfig)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nCloudflare Protection Detected:");
        Console.WriteLine("  This site is protected by Cloudflare and is blocking HttpClient requests.");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("\nRECOMMENDATION:");
        Console.WriteLine("  Start FlareSolverr and run this test again.");
        Console.WriteLine("  Enable it under FlareSolverrSettings in appsettings.json.");
        Console.WriteLine("  You can also use --test-interactive to try Selenium.");
        Console.ResetColor();

        if (siteConfig == null)
        {
            return;
        }

        switch (siteConfig.CloudflareProtection)
        {
            // Handle JsChallenge configuration
            case CloudflareProtectionLevel.JsChallenge:
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  Configuration matches - site is configured for JsChallenge protection.");
                    Console.ResetColor();
                    return;
                }

            // Handle Detected level (needs escalation)
            case CloudflareProtectionLevel.Detected:
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Configuration matches - Cloudflare protection was previously detected for this site.");
                Console.ResetColor();
                return;

            // Handle no Cloudflare protection configured
            case null:
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("\nACTION REQUIRED:");
                Console.WriteLine($"  Please update the site configuration for '{siteConfig.SiteName}':");
                Console.WriteLine("  Set \"cloudflareProtection\": \"detected\"");
                Console.ResetColor();
                break;
        }
    }

    private static void DisplayTestableFields()
    {
        var tocFields = new[] { "Title", "Author", "Description", "Genres", "Status", "AlternativeNames", "Thumbnail", "ChapterLinks", "NovelRating", "TotalRatings", "ChapterTitleInToc" };
        var chapterFields = new[] { "ChapterTitle", "ChapterContent", "NextChapterButton" };

        var messages = new List<string>
        {
            "TESTABLE FIELDS",
            string.Empty,
            "Table of Contents (use TOC URL)",
            string.Empty,
        };
        messages.AddRange(tocFields.Select(f => $"  {f}"));
        messages.Add(string.Empty);
        messages.Add("Chapter (use chapter URL)");
        messages.Add(string.Empty);
        messages.AddRange(chapterFields.Select(f => $"  {f}"));
        messages.Add(string.Empty);
        messages.Add("Note: ChapterTitleInToc uses a relative XPath evaluated per chapter link");
        messages.Add(string.Empty);
        messages.Add("Usage: --test-field \"FieldName:XPath\" <URL>");

        CommonHelper.DrawBox(messages.ToArray(), ConsoleColor.Cyan);
    }

    private static void DisplaySupportedSites()
    {
        var supportedSites = GetSupportedSites();

        // ASCII Art header
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine();
        Console.WriteLine(@"  ____  _____ _   _ _   ___   __     ____   ____ ____      _    ____  _____ ____  ");
        Console.WriteLine(@" | __ )| ____| \ | | \ | \ \ / /    / ___| / ___|  _ \    / \  |  _ \| ____|  _ \ ");
        Console.WriteLine(@" |  _ \|  _| |  \| |  \| |\ V /_____\___ \| |   | |_) |  / _ \ | |_) |  _| | |_) |");
        Console.WriteLine(@" | |_) | |___| |\  | |\  | | |_______|__) | |___|  _ <  / ___ \|  __/| |___|  _ < ");
        Console.WriteLine(@" |____/|_____|_| \_|_| \_| |_|      |____/ \____|_| \_\/_/   \_\_|   |_____|_| \_\");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════╗");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"║                        SUPPORTED WEBSITES ({supportedSites.Count})                         ║");
        Console.ResetColor();
        Console.WriteLine("╠══════════════════════════════════════════════════════════════════════════╣");

        foreach (var site in supportedSites)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("║  ✓  ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{site,-66}");
            Console.WriteLine("║");
        }

        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Tip: Use --test-all to verify connectivity to all sites");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static IReadOnlyList<string> GetSupportedSites()
    {
        using var lifetimeScope = Container!.BeginLifetimeScope();
        return lifetimeScope.Resolve<INovelScraper>().GetSupportedSites();
    }

    private static async Task TestAllSitesAsync()
    {
        var supportedSites = GetSupportedSites();
        var novelScraperSettings = NovelScraperSettings;

        Console.WriteLine($"\n{'='}{new string('=', 60)}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Testing connectivity to all {supportedSites.Count} supported sites");
        Console.ResetColor();
        Console.WriteLine($"{'='}{new string('=', 60)}\n");

        var results = new List<(string Site, bool Success, string Error, bool CloudflareDetected, bool NeedsConfigUpdate, bool IsInactive)>();

        foreach (var site in supportedSites)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Testing: {site}");
            Console.ResetColor();

            try
            {
                if (!Uri.TryCreate(site, UriKind.Absolute, out Uri? testUri))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ FAILED - Invalid URI");
                    Console.ResetColor();
                    results.Add((Site: site, Success: false, Error: "Invalid URI", CloudflareDetected: false, NeedsConfigUpdate: false, IsInactive: false));
                    Console.WriteLine();
                    await Task.Delay(500);
                    continue;
                }

                var siteConfig = novelScraperSettings?.SiteConfigurations?
                    .FirstOrDefault(config => testUri.Host.Contains(config.UrlPattern, StringComparison.OrdinalIgnoreCase));

                if (siteConfig is { IsActive: false })
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  ⊘ INACTIVE - Site has been shut down or is no longer supported");
                    Console.ResetColor();
                    results.Add((site, false, "Site inactive", false, false, true));
                    Console.WriteLine();
                    await Task.Delay(500);
                    continue;
                }

                using var httpClientFactory = new HttpClientFactory();
                using var testStrategy = new TestStrategy(httpClientFactory);

                var (htmlDocument, _, statusCode, cloudflareDetected) = await testStrategy.TestLoadHtmlAsync(testUri);

                var needsConfigUpdate = cloudflareDetected && siteConfig != null && !siteConfig.CloudflareProtection.HasValue;

                if (htmlDocument != null)
                {
                    var titleNode = htmlDocument.DocumentNode.SelectSingleNode("//title");
                    var title = titleNode != null ? titleNode.InnerText.Trim() : "No title";

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  ✓ SUCCESS ({statusCode}) - Title: {(title.Length > 50 ? string.Concat(title.AsSpan(0, 50), "...") : title)}");
                    Console.ResetColor();

                    results.Add((site, true, string.Empty, false, false, false));
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

                    if (siteConfig?.CloudflareProtection == CloudflareProtectionLevel.Detected)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"    → Escalate: Change \"cloudflareProtection\": \"detected\" to \"jschallenge\" for '{siteConfig.SiteName}'");
                    }
                    else if (needsConfigUpdate)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"    → Update the site configuration: Set \"cloudflareProtection\": \"jschallenge\" for '{siteConfig?.SiteName}'");
                    }
                }
                else
                {
                    Console.WriteLine($"  ✗ FAILED - {errorMsg}");
                }

                Console.ResetColor();

                results.Add((site, false, errorMsg, cloudflareDetected, needsConfigUpdate, false));
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ FAILED - {ex.Message}");
                Console.ResetColor();

                results.Add((site, false, ex.Message, false, false, false));
            }

            Console.WriteLine();

            await Task.Delay(500);
        }

        // Summary
        Console.WriteLine($"{'='}{new string('=', 60)}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("TEST SUMMARY");
        Console.ResetColor();
        Console.WriteLine($"{'='}{new string('=', 60)}\n");

        var successCount = results.Count(r => r.Success);
        var inactiveCount = results.Count(r => r.IsInactive);
        var failCount = results.Count(r => r is { Success: false, IsInactive: false });
        var cloudflareBlockedCount = results.Count(r => r is { Success: false, CloudflareDetected: true, IsInactive: false });
        var needsConfigUpdateCount = results.Count(r => r.NeedsConfigUpdate);

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
            foreach (var result in results.Where(r => r.IsInactive))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  - {result.Site}");
                Console.ResetColor();
            }
        }

        if (cloudflareBlockedCount > 0)
        {
            Console.WriteLine("\nCloudflare Protected Sites (require Selenium):");
            foreach (var result in results.Where(r => !r.Success && r.CloudflareDetected))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  - {result.Site}");
                Console.ResetColor();
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"    Status: {result.Error}");
                }

                if (result.NeedsConfigUpdate)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("    → Needs a site configuration update");
                    Console.ResetColor();
                }
            }
        }

        var otherFailCount = results.Count(r => !r.Success && !r.CloudflareDetected && !r.IsInactive);
        if (otherFailCount > 0)
        {
            Console.WriteLine("\nOther Failed Sites:");
            foreach (var result in results.Where(r => !r.Success && !r.CloudflareDetected && !r.IsInactive))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  - {result.Site}");
                Console.ResetColor();
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"    Error: {result.Error}");
                }
            }
        }

        if (successCount > 0)
        {
            Console.WriteLine("\nSuccessful Sites:");
            foreach (var result in results.Where(r => r.Success))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  - {result.Site}");
                Console.ResetColor();
            }
        }

        Console.WriteLine($"\n{'='}{new string('=', 60)}\n");
    }

    private static async Task RetryFailedChaptersAsync(Guid novelId, bool withLogin)
    {
        try
        {
            await using var scope = Container!.BeginLifetimeScope();
            var novelProcessor = scope.Resolve<INovelProcessor>();
            await novelProcessor.RetryFailedChaptersAsync(novelId, withLogin);
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when retrying failed chapters for novel {novelId}. {ex.Message}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error retrying failed chapters: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task RetryAllFailedChaptersAsync(bool withLogin)
    {
        try
        {
            await using var scope = Container!.BeginLifetimeScope();
            var novelService = scope.Resolve<INovelService>();
            var novelProcessor = scope.Resolve<INovelProcessor>();
            var novels = await novelService.GetAllAsync();
            var novelList = novels.ToList();

            Console.WriteLine();
            var headerMessages = new[] { $"RETRY ALL FAILED CHAPTERS ({novelList.Count} novels)" };
            CommonHelper.DrawBox(headerMessages, ConsoleColor.Cyan);

            var totalRecovered = 0;
            var totalStillFailed = 0;
            var novelsWithFailures = new List<(string Title, int Recovered, int StillFailed)>();

            foreach (var novel in novelList)
            {
                var result = await novelProcessor.RetryFailedChaptersAsync(novel.Id, withLogin);
                if (result.TotalFailed > 0)
                {
                    totalRecovered += result.Succeeded;
                    totalStillFailed += result.StillFailed;
                    novelsWithFailures.Add((novel.Title, result.Succeeded, result.StillFailed));
                }
            }

            Console.WriteLine();
            if (novelsWithFailures.Count > 0)
            {
                var summaryMessages = new[] { "OVERALL RETRY SUMMARY" };
                CommonHelper.DrawBox(summaryMessages, ConsoleColor.Cyan);
                Console.WriteLine($"  Novels with failed chapters: {novelsWithFailures.Count}");
                Console.WriteLine($"  Total recovered:             {totalRecovered}");
                Console.WriteLine($"  Total still failed:          {totalStillFailed}");
                Console.WriteLine();

                foreach (var (title, recovered, stillFailed) in novelsWithFailures)
                {
                    var color = stillFailed == 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
                    Console.ForegroundColor = color;
                    Console.WriteLine($"  {title}: {recovered} recovered, {stillFailed} still failed");
                }

                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("No failed chapters found across any novels.");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when retrying all failed chapters. {ex.Message}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error retrying all failed chapters: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task UpdateNovelSavedLocationByIdAsync(Guid id)
    {
        try
        {
            await using var scope = Container!.BeginLifetimeScope();
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
                {
                    Console.WriteLine($"\nDirectory {newSaveLocation} does not exist.");
                }
            }
            else
            {
                Console.WriteLine($"\nNovel with id {id} not found.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when trying to update novel save location. {ex.Message}");
        }
    }

    private static async Task RenameDatabaseFileAsync(string newDbName)
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
            await using var scope = Container!.BeginLifetimeScope();
            var configurationRepository = scope.Resolve<IConfigurationRepository>();
            var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId);
            configuration.DatabaseFileName = newDbName;
            configurationRepository.Update(configuration);

            Console.WriteLine($"Database file renamed to: {newDbName}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when trying to rename database file. {ex.Message}");
        }
    }

    private static async Task DisplayNovelInformationAsync(Guid novelId)
    {
        await using var scope = Container!.BeginLifetimeScope();
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
            new("Current Chapter", novel.CurrentChapter),
            new("Current Chapter Url", novel.CurrentChapterUrl),
            new("Total Chapters", novel.TotalChapters.ToString(CultureInfo.InvariantCulture)),
            new("Date Created", novel.DateCreated.ToShortDateString()),
            new("Last Modified", novel.DateLastModified.ToShortDateString()),
            new("NovelStatus", !string.IsNullOrEmpty(novel.Status) ? novel.Status : "N/A"),
            new("Save Location", novel.SaveLocation ?? "N/A"),
            new("File Type", Enum.GetName(novel.FileType) ?? "EPUB"),
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
            await using var scope = Container!.BeginLifetimeScope();
            var novelService = scope.Resolve<INovelService>();
            var novel = await novelService.GetByIdAsync(id);

            if (novel != null)
            {
                Console.WriteLine($"Current file type for novel: {novel.FileType}");
                var extensions = Enum.GetValues<NovelFileType>().ToList();

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
                {
                    Console.WriteLine("Invalid file type entered.");
                }
            }
            else
            {
                Console.WriteLine($"No novel found for ID: {id}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Exception when trying to update novel file type. {ex.Message}");
        }
    }

    private static void SetupLogger(LogLevel logLevel)
    {
        var loggingConfiguration = new NLog.Config.LoggingConfiguration();

        var applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDirectoryPath = Path.Combine(applicationDataPath, "BennyScraper", "logs");
        Directory.CreateDirectory(logDirectoryPath);

        var logFilePath = Path.Combine(logDirectoryPath, "log-book ${shortdate}.log");
#pragma warning disable CA2000 // NLog owns targets after the configuration is assigned.
        var logFileTarget = new FileTarget("logfile")
        {
            FileName = logFilePath,
            MaxArchiveDays = 14,
            MaxArchiveFiles = 14
        };

        var logConsoleTarget = new ColoredConsoleTarget("logconsole");
        logConsoleTarget.Layout = @"${date:format=HH\:mm\:ss} ${level} ${message} ${exception}";

        logConsoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
            NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Info"), ConsoleOutputColor.Green, ConsoleOutputColor.Black));
        logConsoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
            NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Warn"), ConsoleOutputColor.DarkYellow, ConsoleOutputColor.Black));
        logConsoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
            NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Error"), ConsoleOutputColor.Red, ConsoleOutputColor.Black));
        logConsoleTarget.RowHighlightingRules.Add(new ConsoleRowHighlightingRule(
            NLog.Conditions.ConditionParser.ParseExpression("level == LogLevel.Fatal"), ConsoleOutputColor.White, ConsoleOutputColor.Red));

        loggingConfiguration.AddRule(logLevel, LogLevel.Fatal, logConsoleTarget);
        loggingConfiguration.AddRule(LogLevel.Info, LogLevel.Fatal, logFileTarget);

        NLog.LogManager.Configuration = loggingConfiguration;
#pragma warning restore CA2000
    }

    /// <summary>
    /// Loads the application-wide configuration from appsettings.json. Site configurations are loaded separately from the sites directory.
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
    /// Binds the application-wide scraper settings and adds the per-site files from the sites directory.
    /// </summary>
    /// <param name="configuration">The application configuration containing the non-site scraper settings.</param>
    /// <returns>The complete scraper settings used by the application.</returns>
    private static NovelScraperSettings BuildNovelScraperSettings(IConfiguration configuration)
    {
        var novelScraperSettings = new NovelScraperSettings();
        configuration.GetSection("NovelScraperSettings").Bind(novelScraperSettings);

        novelScraperSettings.SiteConfigurations.Clear();
        var siteConfigurationsDirectory = Path.Combine(AppContext.BaseDirectory, "sites");
        foreach (var siteConfiguration in SiteConfigurationLoader.LoadFromDirectory(siteConfigurationsDirectory))
        {
            novelScraperSettings.SiteConfigurations.Add(siteConfiguration);
        }

        return novelScraperSettings;
    }

    /// <summary>
    /// Registers services, repositories, application settings, site configurations, and EPUB templates.
    /// </summary>
    /// <param name="builder">The container builder.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="novelScraperSettings">The scraper settings, including the configurations loaded from the sites directory.</param>
    private static void ConfigureServices(
        ContainerBuilder builder,
        IConfigurationRoot configuration,
        NovelScraperSettings novelScraperSettings)
    {
        // Register IConfiguration
        builder.RegisterInstance(configuration).As<IConfiguration>();

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
        builder.RegisterType<ComicBookArchiveGenerator>().As<IComicBookArchiveGenerator>().InstancePerDependency();
        builder.RegisterType<CommonStrategy>().Keyed<ScraperStrategy>("common").InstancePerDependency();
        builder.RegisterType<LightNovelWorldStrategy>().Keyed<ScraperStrategy>("lightnovelworld").InstancePerDependency();
        builder.RegisterType<MangaKakalotStrategy>().Keyed<ScraperStrategy>("mangakakalot").InstancePerDependency();
        builder.RegisterType<MangaKatanaStrategy>().Keyed<ScraperStrategy>("mangakatana").InstancePerDependency();
        builder.RegisterType<MangaReaderStrategy>().Keyed<ScraperStrategy>("mangareader").InstancePerDependency();
        builder.RegisterType<NovelBinStrategy>().Keyed<ScraperStrategy>("novelbin").InstancePerDependency();
        builder.RegisterType<NovelDramaStrategy>().Keyed<ScraperStrategy>("noveldrama").InstancePerDependency();
        builder.RegisterType<NovelFullStrategy>().Keyed<ScraperStrategy>("novelfull").InstancePerDependency();
        builder.RegisterType<RoyalRoadStrategy>().Keyed<ScraperStrategy>("royalroad").InstancePerDependency();
        builder.RegisterType<WanderingInnStrategy>().Keyed<ScraperStrategy>("wanderinginn").InstancePerDependency();
        builder.RegisterType<WuxiaWorldStrategy>().Keyed<ScraperStrategy>("wuxiaworld").InstancePerDependency();

        builder.AddHttpResilience();

        // Centralized Selenium driver factory (so all drivers can be disposed on shutdown)
        builder.RegisterType<DriverFactory>().As<IDriverFactory>().SingleInstance();

        builder.RegisterInstance(novelScraperSettings).SingleInstance();

        // needed to register NovelScraperSettings implicitly, Autofac does not resolve 'IOptions<T>' by defualt. Optoins.Create avoids ArgumentException
        builder.Register(c => Options.Create(c.Resolve<NovelScraperSettings>())).As<IOptions<NovelScraperSettings>>().SingleInstance();

        // Register EpubTemplates.cs as a singleton from appsettings.json.
        builder.Register(c =>
        {
            var config = c.Resolve<IConfiguration>();
            var settings = new EpubTemplates();
            config.GetSection("EpubTemplates").Bind(settings);
            return settings;
        }).SingleInstance();
        builder.Register(c => Options.Create(c.Resolve<EpubTemplates>())).As<IOptions<EpubTemplates>>().SingleInstance();

        builder.RegisterType<NovelScraperFactory>().As<INovelScraperFactory>().InstancePerDependency();
        builder.RegisterType<NovelScraper>().As<INovelScraper>().InstancePerDependency();
    }

    /// <summary>
    /// Get the connection string for the database file, if the file does not exist, create it.
    /// </summary>
    /// <returns>connection string.</returns>
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
}