using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.FileGenerators;
using BennyScraper.BusinessLogic.FileGenerators.Interfaces;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Interfaces;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using BennyScraper.BusinessLogic.Services.Interface;
using BennyScraper.BusinessLogic.Utilities;
using BennyScraper.BusinessLogic.Validators;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using NLog;
using System.Reflection;
using Configuration = BennyScraper.Models.Configuration;

namespace BennyScraper.BusinessLogic;

public class NovelProcessor(
    INovelService novelService,
    IChapterService chapterService,
    INovelScraperFactory novelScraper,
    IOptions<NovelScraperSettings> novelScraperSettings,
    IEpubGenerator epubGenerator,
    PdfGenerator pdfGenerator,
    IComicBookArchiveGenerator comicBookArchiveGenerator,
    IConfigurationRepository configurationRepository)
    : INovelProcessor
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private readonly IChapterService _chapterService = chapterService;
    private readonly NovelScraperSettings _novelScraperSettings = novelScraperSettings.Value;
    private const string ProjectName = "Benny-Scraper";
    private const string DllProjectName = "Benny-Scraper.dll";
    private const int DefaultConfigId = 1;

    public async Task ProcessNovelAsync(Uri novelTableOfContentsUri, int? beginChapter = null, int? endChapter = null, bool withLogin = false)
    {
        if (!IsThereConfigurationForSite(novelTableOfContentsUri))
        {
            throw new Exception($"There is no configuration for site {novelTableOfContentsUri.Host}. Please check appsettings.json. Skipping this novel..");
        }

        var novel = await novelService.GetByUrlAsync(novelTableOfContentsUri);

        var siteConfig = GetSiteConfiguration(novelTableOfContentsUri); // nullability check is done in IsThereConfigurationForSite.
        var scraper = novelScraper.CreateScraper(novelTableOfContentsUri, siteConfig);
        var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
        var scraperStrategy = scraper.GetScraperStrategy(novelTableOfContentsUri, siteConfig);

        if (scraperStrategy == null)
        {
            Logger.Error($"No scraper strategy found for {novelTableOfContentsUri}. Skipping this novel..");
            return;
        }

        scraperStrategy.SetVariables(siteConfig, novelTableOfContentsUri, configuration);

        // Enable FlareSolverr if configured (for Cloudflare bypass)
        if (_novelScraperSettings.FlareSolverrSettings?.Enabled == true)
        {
            var flareSolverrUrl = _novelScraperSettings.FlareSolverrSettings.Url ?? "http://localhost:8191";
            await scraperStrategy.EnableFlareSolverrAsync(flareSolverrUrl);
        }

        if (siteConfig.HasPremiumChapters)
        {
            scraperStrategy.SetLoginPreference(withLogin);
        }
        else
        {
            Console.WriteLine("This site does not have premium chapters, login option will be ignored.");
        }

        if (novel == null)
        {
            Logger.Debug($"Novel with url {novelTableOfContentsUri} is not in database, adding it now.");
            await AddNewNovelAsync(novelTableOfContentsUri, scraperStrategy, configuration, beginChapter, endChapter); // consider creating something to decide whi
            Logger.Debug($"Added novel with url {novelTableOfContentsUri} to database.");
        }
        else if (novel.IsPartialDownload)
        {
            Logger.Info($"Novel {novel.Title} (ID: {novel.Id}) is a partial download, expanding.");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Current saved novel chapter: {novel.CurrentChapter}");
            Console.WriteLine($"Date Created: {novel.DateCreated}");
            Console.WriteLine($"Date Last Updated: {novel.DateLastModified}\n");
            Console.ResetColor();
            await ExpandPartialDownloadAsync(novel, novelTableOfContentsUri, scraperStrategy, configuration, beginChapter, endChapter);
        }
        else
        {
            var validator = new ValidateObject();
            var errors = validator.Validate(novel);
            Logger.Info($"Novel {novel.Title} found with url {novelTableOfContentsUri} is in database, updating it now. Novel Id: {novel.Id}");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Current saved novel chapter: {novel.CurrentChapter}");
            Console.WriteLine($"Date Created: {novel.DateCreated}");
            Console.WriteLine($"Date Last Updated: {novel.DateLastModified}\n");
            Console.ResetColor();
            await UpdateExistingNovelAsync(novel, novelTableOfContentsUri, scraperStrategy, configuration, beginChapter, endChapter);
        }
    }

    public async Task<RetryResult> RetryFailedChaptersAsync(Guid novelId, bool withLogin = false)
    {
        var novel = await novelService.GetByIdAsync(novelId);
        if (novel == null)
        {
            Logger.Error($"Novel with ID {novelId} not found.");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Novel with ID {novelId} not found in database.");
            Console.ResetColor();
            return new RetryResult(0, 0, 0);
        }

        var failedChapters = novel.Chapters
            .Where(ch => (string.IsNullOrEmpty(ch.Content) || ch.Content == "No content found")
                         && (ch.Pages == null || ch.Pages.Count == 0))
            .OrderBy(ch => ch.Number)
            .ToList();

        if (failedChapters.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"No failed chapters found for \"{novel.Title}\". All chapters have content.");
            Console.ResetColor();
            return new RetryResult(0, 0, 0);
        }

        // Display failed chapter info
        Console.WriteLine();
        var headerMessages = new[] { $"RETRY FAILED CHAPTERS - {novel.Title}" };
        CommonHelper.DrawBox(headerMessages, ConsoleColor.Yellow);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  Novel ID:        {novel.Id}");
        Console.WriteLine($"  Total Chapters:  {novel.Chapters.Count}");
        Console.WriteLine($"  Failed Chapters: {failedChapters.Count}");
        Console.ResetColor();
        Console.WriteLine();

        // Initialize scraper
        var novelUri = new Uri(novel.Url);
        if (!IsThereConfigurationForSite(novelUri))
        {
            Logger.Error($"No configuration found for site {novelUri.Host}.");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"No site configuration found for {novelUri.Host}. Cannot retry.");
            Console.ResetColor();
            return new RetryResult(failedChapters.Count, 0, failedChapters.Count);
        }

        var siteConfig = GetSiteConfiguration(novelUri);
        var scraper = novelScraper.CreateScraper(novelUri, siteConfig);
        var configuration = await configurationRepository.GetByIdAsync(DefaultConfigId);
        var scraperStrategy = scraper.GetScraperStrategy(novelUri, siteConfig);

        if (scraperStrategy == null)
        {
            Logger.Error($"No scraper strategy found for {novelUri}.");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"No scraper strategy found for {novelUri}. Cannot retry.");
            Console.ResetColor();
            return new RetryResult(failedChapters.Count, 0, failedChapters.Count);
        }

        scraperStrategy.SetVariables(siteConfig, novelUri, configuration);

        if (_novelScraperSettings.FlareSolverrSettings?.Enabled == true)
        {
            var flareSolverrUrl = _novelScraperSettings.FlareSolverrSettings.Url ?? "http://localhost:8191";
            await scraperStrategy.EnableFlareSolverrAsync(flareSolverrUrl);
        }

        // Handle login for premium sites
        if (siteConfig.HasPremiumChapters && withLogin)
        {
            scraperStrategy.SetLoginPreference(true);
            using var novelDataBuffer = await scraperStrategy.ScrapeAsync();
            scraperStrategy.SetSessionAuthenticated(novelDataBuffer?.IsLoggedIn ?? false);
        }
        else
        {
            scraperStrategy.SetSessionAuthenticated(false);
        }

        // Build ChapterLink list from failed chapters
        var chapterLinks = failedChapters.Select(ch => new ChapterLink
        {
            Url = ch.Url,
            Title = ch.Title,
            ChapterNumber = (int)ch.Number
        }).ToList();

        // Re-scrape
        var chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(chapterLinks);

        // Update existing chapters in-place
        var succeeded = 0;
        var stillFailed = 0;
        var recoveredChapters = new List<Chapter>();
        var stillFailedChapters = new List<Chapter>();

        foreach (var buffer in chapterDataBuffers)
        {
            var chapter = failedChapters.FirstOrDefault(ch => ch.Url == buffer.Url);
            if (chapter == null)
            {
                continue;
            }

            var newContent = HtmlEntity.DeEntitize(buffer.Content);
            if (!string.IsNullOrEmpty(newContent) && newContent != "No content found")
            {
                chapter.Content = newContent;
                if (!string.IsNullOrEmpty(buffer.Title))
                {
                    chapter.Title = HtmlEntity.DeEntitize(buffer.Title);
                }

                if (buffer.Pages != null)
                {
                    chapter.SetPages(buffer.Pages.Select(p => new Page { Url = p.Url }));
                }

                chapter.DateLastModified = DateTime.Now;
                succeeded++;
                recoveredChapters.Add(chapter);
            }
            else
            {
                stillFailed++;
                stillFailedChapters.Add(chapter);
            }
        }

        // Any failed chapters not in buffer results are still failed
        var processedUrls = chapterDataBuffers.Select(b => b.Url).ToHashSet();
        foreach (var ch in failedChapters.Where(ch => !processedUrls.Contains(ch.Url)))
        {
            stillFailed++;
            stillFailedChapters.Add(ch);
        }

        // Persist changes
        await novelService.UpdateAsync(novel);

        // Report per-chapter results
        Console.WriteLine();
        if (recoveredChapters.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"RECOVERED CHAPTERS ({recoveredChapters.Count}):");
            foreach (var ch in recoveredChapters)
            {
                Console.WriteLine($"  \u2713 #{ch.Number} - {ch.Title} ({ch.Url})");
            }

            Console.ResetColor();
        }

        if (stillFailedChapters.Count > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"STILL FAILED ({stillFailedChapters.Count}):");
            foreach (var ch in stillFailedChapters)
            {
                Console.WriteLine($"  \u2717 #{ch.Number} - {ch.Title} ({ch.Url})");
            }

            Console.ResetColor();
        }

        // Auto-regenerate output file
        if (succeeded > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Regenerating output file...");
            Console.ResetColor();

            var userOutputDirectory = configuration.DetermineSaveLocation((bool)(siteConfig?.HasImagesForChapterContent));
            var outputDirectory = CommonHelper.GetOutputDirectoryForTitle(novel.Title, userOutputDirectory);

            // Build filename suffix from chapter ranges if partial download
            var filenameSuffix = string.Empty;
            if (novel.IsPartialDownload)
            {
                var orderedRanges = novel.ChapterRanges.OrderBy(r => r.Begin).ToList();
                filenameSuffix = orderedRanges.Count == 1
                    ? $"Ch {orderedRanges[0].Begin}-{orderedRanges[0].End}"
                    : "Ch " + string.Join(" & ", orderedRanges.Select(r => $"{r.Begin}-{r.End}"));
            }

            // Determine file type from actual content, not stored FileType (which may be stale)
            var hasMangaPages = novel.Chapters.Any(ch => ch.Pages?.Count > 0);

            if (!hasMangaPages)
            {
                var sortedChapters = CommonHelper.SortNovelChaptersByDateCreated(novel.Chapters);
                novel.SaveLocation = CreateEpub(novel, sortedChapters, null, outputDirectory, filenameSuffix);
                novel.FileType = NovelFileType.Epub;
            }
            else if (novel.FileType == NovelFileType.Pdf)
            {
                var (saveLocation, isFileSplit) = pdfGenerator.CreatePdf(novel, chapterDataBuffers, outputDirectory, configuration, filenameSuffix);
                novel.SaveLocation = saveLocation;
                novel.SavedFileIsSplit = isFileSplit;
            }
            else
            {
                novel.SaveLocation = comicBookArchiveGenerator.CreateComicBookArchive(novel, chapterDataBuffers, outputDirectory, configuration, filenameSuffix);
            }

            await novelService.UpdateAsync(novel);
        }

        // Dispose buffers
        foreach (var buffer in chapterDataBuffers)
        {
            buffer.Dispose();
        }

        // Final summary box
        Console.WriteLine();
        var summaryMessages = new[]
        {
            "RETRY SUMMARY",
            $"Total Failed: {failedChapters.Count}  |  Recovered: {succeeded}  |  Still Failed: {stillFailed}"
        };
        CommonHelper.DrawBox(summaryMessages, succeeded > 0 ? ConsoleColor.Green : ConsoleColor.Red);

        return new RetryResult(failedChapters.Count, succeeded, stillFailed);
    }

    private async Task AddNewNovelAsync(Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy, Configuration configuration, int? beginChapter = null, int? endChapter = null)
    {
        using var novelDataBuffer = await scraperStrategy.ScrapeAsync();

        SelectedChapterRange? selectedRange = null;
        string? detectedVolumeName = null;
        var chapterRangeSelector = new ChapterRangeSelector();

        if (novelDataBuffer.ChapterLinks.Any())
        {
            Logger.Info("Using cached chapter titles for range selection");
            var chapterTitles = novelDataBuffer.ChapterLinks.Select(l => l.Title).ToList();

            if (beginChapter.HasValue || endChapter.HasValue)
            {
                Logger.Info("Using chapter range from command line options");
                selectedRange = chapterRangeSelector.GetRangeFromOptions(novelDataBuffer.ChapterLinks.Count, beginChapter, endChapter);
                ChapterRangeSelector.DisplayRangeInfo(selectedRange, chapterTitles);
                ChapterRangeSelector.ConfirmPremiumChapters(selectedRange, novelDataBuffer.ChapterLinks, novelDataBuffer?.UserPremiumCurrencies, novelDataBuffer?.IsLoggedIn ?? false);
            }
            else
            {
                selectedRange = chapterRangeSelector.PromptUserForRange(novelDataBuffer.ChapterLinks, novelDataBuffer?.UserPremiumCurrencies, novelDataBuffer?.IsLoggedIn ?? false);

                if (selectedRange == null)
                {
                    var allChaptersRange = new SelectedChapterRange(1, novelDataBuffer!.ChapterLinks.Count);
                    ChapterRangeSelector.ConfirmPremiumChapters(allChaptersRange, novelDataBuffer.ChapterLinks, novelDataBuffer?.UserPremiumCurrencies, novelDataBuffer?.IsLoggedIn ?? false);
                }
            }

            if (selectedRange != null)
            {
                Logger.Info($"User selected chapter range: {selectedRange}");

                // Detect if this is a volume download
                var volumeRanges = DetectVolumes(chapterTitles);
                var matchingVolume = volumeRanges.FirstOrDefault(v =>
                    v.Begin == selectedRange.Begin && v.End == selectedRange.End);

                if (matchingVolume != null)
                {
                    detectedVolumeName = matchingVolume.Name;
                    Logger.Info($"Detected volume download: {detectedVolumeName}");
                }

                scraperStrategy.SetChapterRange(selectedRange, detectedVolumeName);
            }
        }

        var newNovel = CreateNovel(novelDataBuffer, novelTableOfContentsUri, selectedRange);
        Logger.Info("Finished populating Novel data for {0}", newNovel.Title);

        // Filter chapter URLs based on range if selected
        var chaptersToDownload = selectedRange != null
            ? novelDataBuffer.ChapterLinks.Skip(selectedRange.Begin - 1).Take(selectedRange.Count).ToList()
            : novelDataBuffer.ChapterLinks;

        scraperStrategy.SetSessionAuthenticated(novelDataBuffer?.IsLoggedIn ?? false);
        IEnumerable<ChapterDataBuffer> chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(chaptersToDownload);
        newNovel.Chapters.ReplaceWith(CreateChapters(chapterDataBuffers, newNovel.Id));

        var userOutputDirectory = configuration.DetermineSaveLocation((bool)(scraperStrategy.GetSiteConfiguration()?.HasImagesForChapterContent));
        string outputDirectory = CommonHelper.GetOutputDirectoryForTitle(newNovel.Title, outputDirectory = userOutputDirectory);

        var novelId = await novelService.CreateAsync(newNovel);
        Logger.Info("Finished adding novel {0} to database", newNovel.Title);
        var novel = await GetNovelFromDataBase(novelId);

        if (novel == null)
        {
            // This should never happen, somehow some idiot deleted the novel after it was just added within milliseconds, proud of you idiot.
            Logger.Error($"Novel with url {novelTableOfContentsUri} could not be found in the database, even though it should. Novel will not be stored in database.");
            novel = newNovel;
        }
        else
        {
            Logger.Info($"Novel {novel.Title} found with url {novelTableOfContentsUri} is in database, updating it now. Novel Id: {novel.Id}");
        }

        var filenameSuffix = GenerateFilenameSuffix(selectedRange, detectedVolumeName);

        if (novel.Chapters.Any(chapter => chapter?.Pages?.Count > 0))
        {
            if (configuration.DefaultMangaFileExtension == FileExtension.Pdf)
            {
                var (saveLocation, isFileSplit) = pdfGenerator.CreatePdf(novel, chapterDataBuffers, outputDirectory, configuration, filenameSuffix);
                novel.SaveLocation = saveLocation;
                novel.SavedFileIsSplit = isFileSplit;
                novel.FileType = NovelFileType.Pdf;
            }
            else
            {
                novel.SaveLocation = comicBookArchiveGenerator.CreateComicBookArchive(novel, chapterDataBuffers, outputDirectory, configuration, filenameSuffix);
                novel.FileType = Enum.TryParse(configuration.DefaultMangaFileExtension.ToString(), out NovelFileType convertedType)
                    ? convertedType : NovelFileType.Cbz; // check to see if converting by name works, if not default to cbz
            }

            foreach (var chapterDataBuffer in chapterDataBuffers)
            {
                chapterDataBuffer.Dispose();
            }
        }
        else
        {
            novel.SaveLocation = CreateEpub(novel, novel.Chapters, novelDataBuffer.ThumbnailImage?.ToArray(), outputDirectory, filenameSuffix);
            novel.FileType = NovelFileType.Epub;
        }

        await novelService.UpdateAsync(novel);
    }

    private async Task ExpandPartialDownloadAsync(Novel novel, Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy, Configuration configuration, int? beginChapter = null, int? endChapter = null)
    {
        using var novelDataBuffer = await scraperStrategy.ScrapeAsync();

        if (!novelDataBuffer.ChapterLinks.Any())
        {
            Logger.Warn($"No chapter links found for {novel.Title}.");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No chapter links found on the table of contents page.");
            Console.ResetColor();
            return;
        }

        var chapterRangeSelector = new ChapterRangeSelector();
        var chapterTitles = novelDataBuffer.ChapterLinks.Select(l => l.Title).ToList();
        SelectedChapterRange? selectedRange = null;

        if (beginChapter.HasValue || endChapter.HasValue)
        {
            selectedRange = chapterRangeSelector.GetRangeFromOptions(novelDataBuffer.ChapterLinks.Count, beginChapter, endChapter);
            ChapterRangeSelector.DisplayRangeInfo(selectedRange, chapterTitles);
            ChapterRangeSelector.ConfirmPremiumChapters(selectedRange, novelDataBuffer.ChapterLinks, novelDataBuffer?.UserPremiumCurrencies, novelDataBuffer?.IsLoggedIn ?? false);
        }
        else
        {
            var chapterLinksList = novelDataBuffer.ChapterLinks;
            var existingRanges = novel.ChapterRanges.OrderBy(r => r.Begin).Select(r =>
            {
                var beginTitle = r.Begin >= 1 && r.Begin <= chapterLinksList.Count ? chapterLinksList[r.Begin - 1].Title : "?";
                var endTitle = r.End >= 1 && r.End <= chapterLinksList.Count ? chapterLinksList[r.End - 1].Title : "?";
                return $"{r.Begin}-{r.End} [{beginTitle} ... {endTitle}]";
            }).ToList();
            Console.WriteLine();
            var headerMessages = new[] { $"PARTIAL DOWNLOAD - {novel.Title}" };
            CommonHelper.DrawBox(headerMessages, ConsoleColor.Cyan);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  Existing Ranges:    {string.Join(", ", existingRanges)}");
            Console.WriteLine($"  Chapters in DB:     {novel.Chapters.Count}");
            Console.WriteLine($"  Chapters on Site:   {novelDataBuffer.ChapterLinks.Count}");
            Console.ResetColor();
            Console.WriteLine();

            selectedRange = chapterRangeSelector.PromptUserForRange(novelDataBuffer.ChapterLinks, novelDataBuffer?.UserPremiumCurrencies, novelDataBuffer?.IsLoggedIn ?? false);

            if (selectedRange == null)
            {
                selectedRange = new SelectedChapterRange(1, novelDataBuffer!.ChapterLinks.Count);
                ChapterRangeSelector.ConfirmPremiumChapters(selectedRange, novelDataBuffer.ChapterLinks, novelDataBuffer?.UserPremiumCurrencies, novelDataBuffer?.IsLoggedIn ?? false);
            }
        }

        var rangeChapterLinks = novelDataBuffer.ChapterLinks
            .Skip(selectedRange.Begin - 1)
            .Take(selectedRange.Count)
            .ToList();

        var existingUrls = novel.Chapters.Select(ch => ch.Url).ToHashSet();
        var newChapterLinks = rangeChapterLinks.Where(link => !existingUrls.Contains(link.Url)).ToList();

        if (newChapterLinks.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("All chapters in the selected range are already downloaded.");
            Console.ResetColor();
            return;
        }

        var skippedCount = rangeChapterLinks.Count - newChapterLinks.Count;
        if (skippedCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Skipping {skippedCount} already-downloaded chapter(s). Downloading {newChapterLinks.Count} new chapter(s).");
            Console.ResetColor();
        }

        string? detectedVolumeName = null;
        var volumeRanges = DetectVolumes(chapterTitles);
        var matchingVolume = volumeRanges.FirstOrDefault(v => v.Begin == selectedRange.Begin && v.End == selectedRange.End);
        if (matchingVolume != null)
        {
            detectedVolumeName = matchingVolume.Name;
            Logger.Info($"Detected volume download: {detectedVolumeName}");
        }

        scraperStrategy.SetSessionAuthenticated(novelDataBuffer?.IsLoggedIn ?? false);
        var chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(newChapterLinks);
        var newChapters = CreateChapters(chapterDataBuffers, novel.Id);

        novel.Chapters.AddRange(newChapters);
        novel.TotalChapters = novel.Chapters.Count;
        novel.DateLastModified = DateTime.Now;

        AddOrUpdateChapterRange(novel, selectedRange);

        var userOutputDirectory = configuration.DetermineSaveLocation((bool)(scraperStrategy.GetSiteConfiguration()?.HasImagesForChapterContent));
        var orderedRanges = novel.ChapterRanges.OrderBy(r => r.Begin).ToList();
        var filenameSuffix = orderedRanges.Count == 1
            ? GenerateFilenameSuffix(new SelectedChapterRange(orderedRanges[0].Begin, orderedRanges[0].End), detectedVolumeName)
            : "Ch " + string.Join(" & ", orderedRanges.Select(r => $"{r.Begin}-{r.End}"));

        await HandleFileTypeUpdatesAsync(novel, novelDataBuffer, chapterDataBuffers, newChapters, configuration, userOutputDirectory, filenameSuffix);
    }

    private async Task UpdateExistingNovelAsync(Novel novel, Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy, Configuration configuration, int? beginChapter = null, int? endChapter = null)
    {
        if (novel == null)
        {
            throw new ArgumentNullException(nameof(novel));
        }

        if (ShouldSkipPartialDownloadUpdate(novel, novelTableOfContentsUri, beginChapter, endChapter))
        {
            return;
        }

        using var novelDataBuffer = await scraperStrategy.ScrapeAsync();

        // Only skip when no explicit range is requested
        if (!beginChapter.HasValue && !endChapter.HasValue && IsNovelUpToDate(novel, novelDataBuffer, novelTableOfContentsUri))
        {
            novel.DateLastModified = DateTime.Now;
            await novelService.UpdateAsync(novel);
            return;
        }

        var sortedSavedChapters = CommonHelper.SortNovelChaptersByDateCreated(novel.Chapters);
        var newChapterLinks = DetermineNewChaptersToScrape(novel.CurrentChapterUrl, sortedSavedChapters, novel.Id, novelDataBuffer.ChapterLinks);

        SelectedChapterRange? selectedRange = null;
        var chapterRangeSelector = new ChapterRangeSelector();

        if (beginChapter.HasValue || endChapter.HasValue)
        {
            selectedRange = chapterRangeSelector.GetRangeFromOptions(novelDataBuffer.ChapterLinks.Count, beginChapter, endChapter);
            ChapterRangeSelector.DisplayRangeInfo(selectedRange, novelDataBuffer.ChapterLinks.Select(c => c.Title).ToList()!);
            ChapterRangeSelector.ConfirmPremiumChapters(selectedRange, novelDataBuffer.ChapterLinks, novelDataBuffer?.UserPremiumCurrencies, novelDataBuffer?.IsLoggedIn ?? false);

            newChapterLinks = newChapterLinks.Where(link =>
            {
                var chapterNumber = novelDataBuffer!.ChapterLinks.FindIndex(c => c.Url == link.Url) + 1;
                return chapterNumber >= selectedRange.Begin && chapterNumber <= selectedRange.End;
            }).ToList();
        }
        else if (newChapterLinks.Any())
        {
            // When downloading all new chapters, still check for premium chapters
            var firstNewChapterIndex = novelDataBuffer.ChapterLinks.FindIndex(c => c.Url == newChapterLinks.First().Url) + 1;
            var lastNewChapterIndex = novelDataBuffer.ChapterLinks.FindIndex(c => c.Url == newChapterLinks.Last().Url) + 1;
            var newChaptersRange = new SelectedChapterRange(firstNewChapterIndex, lastNewChapterIndex);
            ChapterRangeSelector.ConfirmPremiumChapters(newChaptersRange, novelDataBuffer.ChapterLinks, novelDataBuffer?.UserPremiumCurrencies, novelDataBuffer?.IsLoggedIn ?? false);
        }

        scraperStrategy.SetSessionAuthenticated(novelDataBuffer?.IsLoggedIn ?? false);
        var chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(newChapterLinks);
        var newChapters = CreateChapters(chapterDataBuffers, novel.Id);
        var userOutputDirectory = configuration.DetermineSaveLocation((bool)(scraperStrategy.GetSiteConfiguration()?.HasImagesForChapterContent));
        UpdateNovel(novel, novelDataBuffer!, newChapters);

        // Update chapter ranges if this is a continuation of a partial download
        if (selectedRange != null && novel.IsPartialDownload)
        {
            AddOrUpdateChapterRange(novel, selectedRange);
        }
        else if (selectedRange != null && !novel.IsPartialDownload)
        {
            // First partial download for this novel
            var exists = novel.ChapterRanges.Any(r => r.Begin == selectedRange.Begin && r.End == selectedRange.End);
            if (!exists)
            {
                novel.ChapterRanges.Add(new ChapterRange
                {
                    NovelId = novel.Id,
                    Begin = selectedRange.Begin,
                    End = selectedRange.End,
                    DateCreated = DateTime.Now
                });
                Logger.Info($"Added first chapter range: {selectedRange.Begin}-{selectedRange.End}");
            }
        }

        var filenameSuffix = GenerateFilenameSuffix(selectedRange, null);

        await HandleFileTypeUpdatesAsync(novel, novelDataBuffer, chapterDataBuffers, newChapters, configuration, userOutputDirectory, filenameSuffix);
    }

    private async Task<Novel?> GetNovelFromDataBase(Guid id)
    {
        var novel = await novelService.GetByIdAsync(id);
        if (novel != null)
        {
            novel.Chapters.ReplaceWith(novel.Chapters.OrderBy(chapter => chapter.Number).ToList());
        }

        return novel;
    }

    private string CreateEpub(Novel novel, ICollection<Chapter> chapters, byte[]? thumbnailImage, string outputDirectory, string filenameSuffix = "")
    {
        Directory.CreateDirectory(outputDirectory);
        var baseFilename = CommonHelper.SanitizeFileName(novel.Title, true);
        var filename = string.IsNullOrEmpty(filenameSuffix) ? baseFilename : $"{baseFilename} - {filenameSuffix}";
        var epubFile = Path.Combine(outputDirectory, $"{filename}.epub");
        epubGenerator.CreateEpub(novel, chapters, epubFile, thumbnailImage);
        return epubFile;
    }

    /// <summary>
    /// Generates filename suffix based on volume detection or chapter range
    /// </summary>
    private static string GenerateFilenameSuffix(SelectedChapterRange? chapterRange, string? detectedVolumeName)
    {
        if (chapterRange == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrEmpty(detectedVolumeName)
            ? detectedVolumeName
            : $"Ch {chapterRange.Begin}-{chapterRange.End}";
    }

    private static void UpdateNovel(Novel novel, NovelDataBuffer novelDataBuffer, List<Models.Chapter> newChapters)
    {
        novel.Chapters.AddRange(newChapters);
        novel.LastTableOfContentsUrl = (!string.IsNullOrEmpty(novelDataBuffer.LastTableOfContentsPageUrl)) ? novelDataBuffer.LastTableOfContentsPageUrl : novel.LastTableOfContentsUrl;
        novel.Status = (!string.IsNullOrEmpty(novelDataBuffer.NovelStatus)) ? novelDataBuffer.NovelStatus : novel.Status;
        novel.LastChapter = novelDataBuffer.IsNovelCompleted;
        novel.DateLastModified = DateTime.Now;
        novel.TotalChapters = novel.Chapters.Count;
        novel.CurrentChapter = novel.Chapters.LastOrDefault()?.Title ?? string.Empty;
        novel.CurrentChapterUrl = novel.Chapters.LastOrDefault()?.Url ?? string.Empty;
        if (!string.IsNullOrEmpty(novelDataBuffer.NovelUrl))
        {
            novel.Url = novelDataBuffer.NovelUrl;
        }

        if (novelDataBuffer.Genres.Count != 0)
        {
            novel.Genre = string.Join(", ", novelDataBuffer.Genres);
        }
    }

    private async Task HandleFileTypeUpdatesAsync(Novel novel, NovelDataBuffer novelDataBuffer, List<ChapterDataBuffer> chapterDataBuffers, List<Chapter> newChapters, Configuration configuration, string userOutputDirectory, string filenameSuffix = "")
    {
        var outputDirectory = CommonHelper.GetOutputDirectoryForTitle(novel.Title, userOutputDirectory);

        if (newChapters.All(chapter => chapter?.Pages == null || chapter.Pages.Count == 0) && novel.FileType == NovelFileType.Epub)
        {
            var sortedChapters = CommonHelper.SortNovelChaptersByDateCreated(novel.Chapters);
            novel.SaveLocation = CreateEpub(novel, sortedChapters, novelDataBuffer.ThumbnailImage?.ToArray(), outputDirectory, filenameSuffix);
            await novelService.UpdateAndAddChaptersAsync(novel, newChapters);
            return;
        }

        if (string.IsNullOrEmpty(novel.SaveLocation)) // assume that if the save location is null, then the novel is a pdf and was added before the cbz feature was added
        {
            novel.SaveLocation = Path.Combine(outputDirectory, CommonHelper.SanitizeFileName(novel.Title) + PdfGenerator.PdfFileExtension);
        }

        if (novel.FileType == NovelFileType.Pdf)
        {
            if (novel.SavedFileIsSplit)
            {
                pdfGenerator.CreatePdfByChapter(novel, chapterDataBuffers, novel.SaveLocation);
            }
            else
            {
                pdfGenerator.UpdatePdf(novel, chapterDataBuffers, configuration);
            }
        }
        else
        {
            comicBookArchiveGenerator.UpdateComicBookArchive(novel, chapterDataBuffers, outputDirectory, configuration);
        }

        foreach (var chapterDataBuffer in chapterDataBuffers)
        {
            chapterDataBuffer.Dispose();
        }

        await novelService.UpdateAndAddChaptersAsync(novel, newChapters);
    }

    private static bool IsNovelUpToDate(Novel novel, NovelDataBuffer novelDataBuffer, Uri novelTableOfContentsUri)
    {
        if ((novel.CurrentChapterUrl == novelDataBuffer.CurrentChapterUrl) || novel.CurrentChapter == novelDataBuffer.MostRecentChapterTitle)
        {
            Logger.Warn($"Novel {novel.Title} with url {novelTableOfContentsUri} is up to date.\n\t\tCurrent chapter: {novelDataBuffer.MostRecentChapterTitle} Novel Id: {novel.Id}");
            return true;
        }

        var lastChapter = novel.Chapters.OrderBy(chapter => chapter.Number).LastOrDefault();
        if (lastChapter == null || lastChapter.Url != novelDataBuffer.CurrentChapterUrl ||
            lastChapter.Title != novelDataBuffer.MostRecentChapterTitle)
        {
            return false;
        }

        Logger.Warn($"Novel {novel.Title} with url {novelTableOfContentsUri} is up to date.\n\t\tCurrent chapter: {novelDataBuffer.MostRecentChapterTitle} Novel Id: {novel.Id}");
        return true;
    }

    private static List<ChapterLink> DetermineNewChaptersToScrape(string currentChapterUrl, ICollection<Chapter> savedChapters, Guid novelId, IList<ChapterLink> bufferChapterLinks)
    {
        var indexOfLastChapter = bufferChapterLinks.FindIndex(cl => cl.Url == currentChapterUrl);
        if (indexOfLastChapter == -1 && savedChapters.Any())
        {
            indexOfLastChapter = bufferChapterLinks.FindIndex(cl => cl.Url == savedChapters.Last().Url);
        }

        if (indexOfLastChapter != -1)
        {
            return bufferChapterLinks.Skip(indexOfLastChapter + 1).ToList();
        }

        Logger.Error($"A case where the last chapter is not in the database and the current chapter is not in the database has been found. Novel Id: {novelId}");
        var getDllLocation = Assembly.GetExecutingAssembly().Location;
        var getDllDir = Path.GetDirectoryName(getDllLocation);
        var mainDll = Path.Combine(getDllDir, DllProjectName);
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write(File.Exists(mainDll)
            ? $"Please delete the novel from the database using\n\t\t{mainDll} delete_novel_by_id {novelId} and try again."
            : $"Please delete the novel from the database using\n\t\t{ProjectName} delete_novel_by_id {novelId} and try again.");
        Console.ResetColor();
        return bufferChapterLinks.Skip(indexOfLastChapter + 1).ToList();
    }

    /// <summary>
    /// Checks if we should skip updating a partial download that has no chapter range specified.
    /// Returns true if update should be skipped, false otherwise.
    /// </summary>
    private static bool ShouldSkipPartialDownloadUpdate(Novel novel, Uri novelTableOfContentsUri, int? beginChapter, int? endChapter)
    {
        // Only block partial downloads if NO chapter range is specified
        // If a range is specified, the overlap check above already validated it's a continuation
        if (!novel.IsPartialDownload || beginChapter.HasValue || endChapter.HasValue)
        {
            return false;
        }

        Logger.Info($"Skipping update for novel {novel.Title} (ID: {novel.Id}) - This is a partial download (volume/chapter range)");
        Console.WriteLine();
        var partialMessages = new[] { "PARTIAL DOWNLOAD DETECTED" };
        CommonHelper.DrawBox(partialMessages, ConsoleColor.Yellow);
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  Novel:           {novel.Title}");
        Console.WriteLine($"  Novel ID:        {novel.Id}");
        if (novel.ChapterRanges.Any())
        {
            var ranges = novel.ChapterRanges.OrderBy(r => r.Begin).Select(r => $"{r.Begin}-{r.End}").ToList();
            Console.WriteLine($"  Chapter Ranges:  {string.Join(", ", ranges)}");
        }

        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Partial downloads (with chapter/volume ranges) cannot be automatically");
        Console.WriteLine("updated. To download the full novel or a different range:");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  1. Delete the existing partial download:");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"     benny-scraper -d {novel.Id}");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  2. Then re-download the novel:");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"     benny-scraper \"{novelTableOfContentsUri}\"");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine(new string('─', 78));
        Console.WriteLine();
        return true;
    }

    /// <summary>
    /// Adds a new chapter range or extends an existing one if continuous
    /// </summary>
    private static void AddOrUpdateChapterRange(Novel novel, SelectedChapterRange selectedRange)
    {
        var orderedRanges = novel.ChapterRanges.OrderBy(r => r.Begin).ToList();

        // Check if the new range is continuous with any existing range (allow 2 chapter buffer)
        var continuousRange = orderedRanges.FirstOrDefault(r =>
            (selectedRange.Begin >= r.Begin - 2 && selectedRange.Begin <= r.End + 2) ||
            (selectedRange.End >= r.Begin - 2 && selectedRange.End <= r.End + 2));

        if (continuousRange != null)
        {
            // Extend the existing range
            var oldBegin = continuousRange.Begin;
            var oldEnd = continuousRange.End;
            continuousRange.Begin = Math.Min(continuousRange.Begin, selectedRange.Begin);
            continuousRange.End = Math.Max(continuousRange.End, selectedRange.End);

            Logger.Info($"Extended chapter range from {oldBegin}-{oldEnd} to {continuousRange.Begin}-{continuousRange.End}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Extended range: {oldBegin}-{oldEnd} → {continuousRange.Begin}-{continuousRange.End}");
            Console.ResetColor();
        }
        else
        {
            // Add new range (gap detected)
            novel.ChapterRanges.Add(new Models.ChapterRange
            {
                NovelId = novel.Id,
                Begin = selectedRange.Begin,
                End = selectedRange.End,
                DateCreated = DateTime.Now
            });

            var ranges = novel.ChapterRanges.OrderBy(r => r.Begin).Select(r => $"{r.Begin}-{r.End}").ToList();
            Logger.Info($"Added new chapter range: {selectedRange.Begin}-{selectedRange.End}. All ranges: {string.Join(", ", ranges)}");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Gap detected - Added new range: {selectedRange.Begin}-{selectedRange.End}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  All ranges: {string.Join(", ", ranges)}");
            Console.ResetColor();
        }
    }

    private static Novel CreateNovel(NovelDataBuffer novelDataBuffer, Uri novelTableOfContentsUri, SelectedChapterRange? chapterRange = null, string? volumeName = null)
    {
        var novel = new Novel
        {
            Title = novelDataBuffer.Title ?? string.Empty,
            Author = novelDataBuffer.Author,
            Url = novelTableOfContentsUri.ToString(), // to handle stale urls, make sure to handle the case when users are able to update from files
            Genre = string.Join(", ", novelDataBuffer.Genres),
            Description = novelDataBuffer.Description != null ? string.Join(" ", novelDataBuffer.Description) : null,
            DateCreated = DateTime.Now,
            DateLastModified = DateTime.Now,
            Status = novelDataBuffer.NovelStatus,
            LastTableOfContentsUrl = novelDataBuffer.LastTableOfContentsPageUrl,
            LastChapter = novelDataBuffer.IsNovelCompleted,
            CurrentChapter = novelDataBuffer.MostRecentChapterTitle ?? string.Empty,
            SiteName = novelTableOfContentsUri.Host ?? string.Empty,
            FirstChapter = novelDataBuffer.FirstChapter ?? string.Empty,
            CurrentChapterUrl = novelDataBuffer.CurrentChapterUrl ?? string.Empty
        };

        // Add to ChapterRanges collection if this is a partial download
        if (chapterRange != null)
        {
            novel.ChapterRanges.Add(new Models.ChapterRange
            {
                NovelId = novel.Id,
                Begin = chapterRange.Begin,
                End = chapterRange.End,
                DateCreated = DateTime.Now,
                VolumeName = volumeName
            });
        }

        return novel;
    }

    private static List<Chapter> CreateChapters(IEnumerable<ChapterDataBuffer> chapterDataBuffers, Guid novelId)
    {
        return chapterDataBuffers.Select(data =>
        {
            var chapter = new Chapter
            {
                NovelId = novelId,
                Url = data.Url ?? string.Empty,
                Content = HtmlEntity.DeEntitize(data.Content),
                Title = HtmlEntity.DeEntitize(data.Title) ?? string.Empty,
                Number = data.SequenceNumber,
                DateCreated = DateTime.Now,
                DateLastModified = data.DateLastModified
            };
            chapter.SetPages(data.Pages?.Select(p => new Page { Url = p.Url }));
            return chapter;
        }).ToList();
    }

    private bool IsThereConfigurationForSite(Uri novelTableOfContentsUri)
    {
        var siteConfigurations = _novelScraperSettings.SiteConfigurations;
        return siteConfigurations.Any(config => novelTableOfContentsUri.Host.Contains(config.UrlPattern));
    }

    private SiteConfiguration GetSiteConfiguration(Uri novelTableOfContentsUri)
    {
        var siteConfigurations = _novelScraperSettings.SiteConfigurations;
        return siteConfigurations.First(config => novelTableOfContentsUri.Host.Contains(config.UrlPattern));
    }

    /// <summary>
    /// Detects volume boundaries from chapter titles using common patterns
    /// </summary>
    private static List<VolumeRange> DetectVolumes(List<string> chapterTitles)
    {
        var volumes = new List<VolumeRange>();
        var volumePatterns = new[]
        {
            @"volume\s*(\d+)",
            @"vol\.?\s*(\d+)",
            @"book\s*(\d+)",
            @"part\s*(\d+)",
            @"v(\d+)",
            @"\(v(\d+)\)"
        };

        int? currentVolumeStart = null;
        int? currentVolumeNumber = null;

        for (var i = 0; i < chapterTitles.Count; i++)
        {
            var title = chapterTitles[i].ToLower();
            var detectedVolume = TryDetectVolumeNumber(title, volumePatterns);

            if (!detectedVolume.HasValue)
            {
                continue;
            }

            // First volume detected
            if (!currentVolumeNumber.HasValue)
            {
                currentVolumeStart = i + 1;
                currentVolumeNumber = detectedVolume.Value;
                continue;
            }

            // Same volume, continue
            if (detectedVolume.Value == currentVolumeNumber.Value)
            {
                continue;
            }

            // New volume detected, save previous volume
            volumes.Add(new VolumeRange
            {
                Begin = currentVolumeStart.Value,
                End = i,
                Name = $"Volume {currentVolumeNumber.Value}"
            });

            currentVolumeStart = i + 1;
            currentVolumeNumber = detectedVolume.Value;
        }

        // Add final volume if exists
        if (currentVolumeStart.HasValue && currentVolumeNumber.HasValue)
        {
            volumes.Add(new VolumeRange
            {
                Begin = currentVolumeStart.Value,
                End = chapterTitles.Count,
                Name = $"Volume {currentVolumeNumber.Value}"
            });
        }

        return volumes;
    }

    private static int? TryDetectVolumeNumber(string title, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(title, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success || match.Groups.Count <= 1)
            {
                continue;
            }

            if (!int.TryParse(match.Groups[1].Value, out var volNum))
            {
                continue;
            }

            return volNum;
        }

        return null;
    }

    private class VolumeRange
    {
        public int Begin { get; set; }

        public int End { get; set; }

        public string Name { get; set; }
    }
}