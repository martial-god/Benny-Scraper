using System.Globalization;
using System.Reflection;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Extensions;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.FileGenerators;
using BennyScraper.BusinessLogic.FileGenerators.Interfaces;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Interfaces;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using BennyScraper.BusinessLogic.Services.Interfaces;
using BennyScraper.BusinessLogic.Utilities;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using NLog;
using Configuration = BennyScraper.Models.Configuration;

namespace BennyScraper.BusinessLogic;

internal sealed class NovelProcessor(
    INovelService novelService,
    INovelScraperFactory novelScraper,
    IOptions<NovelScraperSettings> novelScraperSettings,
    IEpubGenerator epubGenerator,
    IComicBookArchiveGenerator comicBookArchiveGenerator,
    IConfigurationRepository configurationRepository)
    : INovelProcessor
{
    private const string _projectName = "Benny-Scraper";
    private const int _defaultConfigId = 1;
    private const string _dllProjectName = "Benny-Scraper.dll";
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly NovelScraperSettings _novelScraperSettings = novelScraperSettings.Value;

    public async Task ProcessNovelAsync(Uri novelTableOfContentsUri, int? beginChapter = null, int? endChapter = null, bool withLogin = false, bool confirmPremiumChapters = true)
    {
        if (!IsThereConfigurationForSite(novelTableOfContentsUri))
        {
            throw new InvalidOperationException($"There is no configuration for site {novelTableOfContentsUri.Host}. Please check the sites directory. Skipping this novel..");
        }

        var novel = await novelService.GetByUrlAsync(novelTableOfContentsUri).ConfigureAwait(false);

        var siteConfig = GetSiteConfiguration(novelTableOfContentsUri); // nullability check is done in IsThereConfigurationForSite.
        var scraper = novelScraper.CreateScraper(novelTableOfContentsUri, siteConfig);
        var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId).ConfigureAwait(false);
        using var scraperStrategy = scraper.GetScraperStrategy(novelTableOfContentsUri, siteConfig);

        if (scraperStrategy == null)
        {
            _logger.Error($"No scraper strategy found for {novelTableOfContentsUri}. Skipping this novel..");
            return;
        }

        scraperStrategy.SetVariables(siteConfig, novelTableOfContentsUri, configuration);

        await ConfigureFlareSolverrAsync(scraperStrategy, siteConfig).ConfigureAwait(false);

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
            _logger.Debug($"Novel with url {novelTableOfContentsUri} is not in database, adding it now.");
            await AddNewNovelAsync(novelTableOfContentsUri, scraperStrategy, configuration, beginChapter, endChapter).ConfigureAwait(false); // consider creating something to decide whi
            _logger.Debug($"Added novel with url {novelTableOfContentsUri} to database.");
        }
        else if (novel.IsPartialDownload)
        {
            _logger.Info($"Novel {novel.Title} (ID: {novel.Id}) is a partial download, expanding.");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Current saved novel chapter: {novel.CurrentChapter}");
            Console.WriteLine($"Date Created: {novel.DateCreated}");
            Console.WriteLine($"Date Last Updated: {novel.DateLastModified}\n");
            Console.ResetColor();
            await ExpandPartialDownloadAsync(novel, novelTableOfContentsUri, scraperStrategy, configuration, beginChapter, endChapter).ConfigureAwait(false);
        }
        else
        {
            _logger.Info($"Novel {novel.Title} found with url {novelTableOfContentsUri} is in database, updating it now. Novel Id: {novel.Id}");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Current saved novel chapter: {novel.CurrentChapter}");
            Console.WriteLine($"Date Created: {novel.DateCreated}");
            Console.WriteLine($"Date Last Updated: {novel.DateLastModified}\n");
            Console.ResetColor();
            await UpdateExistingNovelAsync(novel, novelTableOfContentsUri, scraperStrategy, configuration, beginChapter, endChapter, confirmPremiumChapters).ConfigureAwait(false);
        }
    }

    public async Task<RetryResult> RetryFailedChaptersAsync(Guid novelId, bool withLogin = false)
    {
        var novel = await novelService.GetByIdAsync(novelId).ConfigureAwait(false);
        if (novel == null)
        {
            _logger.Error($"Novel with ID {novelId} not found.");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Novel with ID {novelId} not found in database.");
            Console.ResetColor();
            return new RetryResult(0, 0, 0);
        }

        var failedChapters = novel.Chapters
            .Where(NovelChapterStateUpdater.IsIncompleteChapter)
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
            _logger.Error($"No configuration found for site {novelUri.Host}.");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"No site configuration found for {novelUri.Host}. Cannot retry.");
            Console.ResetColor();
            return new RetryResult(failedChapters.Count, 0, failedChapters.Count);
        }

        var siteConfig = GetSiteConfiguration(novelUri);
        if (siteConfig == null)
        {
            _logger.Error($"There is no {nameof(SiteConfiguration)} found for {novelUri.Host}. Check the sites directory and verify one is present.");
            throw new InvalidOperationException($"There is no {nameof(SiteConfiguration)} found for {novelUri.Host}. Check the sites directory and verify one is present.");
        }

        var scraper = novelScraper.CreateScraper(novelUri, siteConfig);
        var configuration = await configurationRepository.GetByIdAsync(_defaultConfigId).ConfigureAwait(false);
        using var scraperStrategy = scraper.GetScraperStrategy(novelUri, siteConfig);

        if (scraperStrategy == null)
        {
            _logger.Error($"No scraper strategy found for {novelUri}.");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"No scraper strategy found for {novelUri}. Cannot retry.");
            Console.ResetColor();
            return new RetryResult(failedChapters.Count, 0, failedChapters.Count);
        }

        scraperStrategy.SetVariables(siteConfig, novelUri, configuration);

        await ConfigureFlareSolverrAsync(scraperStrategy, siteConfig).ConfigureAwait(false);

        // Handle login for premium sites
        if (siteConfig.HasPremiumChapters && withLogin)
        {
            scraperStrategy.SetLoginPreference(true);
            using var novelDataBuffer = await scraperStrategy.ScrapeAsync().ConfigureAwait(false);
            scraperStrategy.SetSessionAuthenticated(novelDataBuffer.IsLoggedIn);
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
        var chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(chapterLinks).ConfigureAwait(false);

        var originalPageCountsByChapterNumber = novel.Chapters
            .ToDictionary(chapter => chapter.Number, chapter => chapter.Pages?.Count ?? 0);

        // Update existing chapters in-place
        var succeeded = 0;
        var stillFailed = 0;
        var recoveredChapters = new List<Chapter>();
        var recoveredChapterDataBuffers = new List<ChapterDataBuffer>();
        var stillFailedChapters = new List<Chapter>();

        foreach (var buffer in chapterDataBuffers)
        {
            var chapter = failedChapters.FirstOrDefault(ch => ch.Url == buffer.Url);
            if (chapter == null)
            {
                continue;
            }

            if (NovelChapterStateUpdater.ApplyChapterRetry(
                    chapter,
                    buffer,
                    siteConfig.HasImagesForChapterContent))
            {
                succeeded++;
                recoveredChapters.Add(chapter);
                recoveredChapterDataBuffers.Add(buffer);
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

        NovelChapterStateUpdater.UpdateDownloadedChapterBoundaries(novel);

        // Persist changes
        await novelService.UpdateAsync(novel).ConfigureAwait(false);

        // Report per-chapter results
        Console.WriteLine();
        if (recoveredChapters.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"RECOVERED CHAPTERS ({recoveredChapters.Count}):");
            foreach (var ch in recoveredChapters)
            {
                Console.WriteLine($"  ✓ #{ch.Number} - {ch.Title} ({ch.Url})");
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
                Console.WriteLine($"  ✗ #{ch.Number} - {ch.Title} ({ch.Url})");
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

            var userOutputDirectory = configuration.DetermineSaveLocation(siteConfig.HasImagesForChapterContent);
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

            if (!siteConfig.HasImagesForChapterContent)
            {
                var sortedChapters = CommonHelper.SortNovelChaptersByNumber(novel.Chapters).ToList();
                novel.SaveLocation = CreateEpub(novel, sortedChapters, null, outputDirectory, filenameSuffix);
                novel.FileType = NovelFileType.Epub;
            }
            else if (novel.FileType == NovelFileType.Pdf)
            {
                if (novel.SavedFileIsSplit)
                {
                    var pdfDirectoryPath = string.IsNullOrWhiteSpace(novel.SaveLocation)
                        ? outputDirectory
                        : novel.SaveLocation;
                    PdfGenerator.CreatePdfByChapter(
                        novel,
                        recoveredChapterDataBuffers,
                        pdfDirectoryPath,
                        filenameSuffix);
                }
                else
                {
                    PdfGenerator.UpdatePdf(
                        novel,
                        recoveredChapterDataBuffers,
                        configuration,
                        originalPageCountsByChapterNumber);
                }
            }
            else
            {
                novel.SaveLocation = comicBookArchiveGenerator.UpdateComicBookArchive(
                    novel,
                    recoveredChapterDataBuffers,
                    outputDirectory,
                    configuration);
            }

            await novelService.UpdateAsync(novel).ConfigureAwait(false);
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

    /// <summary>
    /// Generates a filename suffix based on volume detection or the selected chapter range.
    /// </summary>
    /// <param name="chapterRange">The chapter range selected for this download, or null if the entire novel was downloaded.</param>
    /// <param name="detectedVolumeName">The detected volume name to use instead of a chapter range, if any.</param>
    /// <returns>A filename suffix describing the volume or chapter range, or an empty string if no range was selected.</returns>
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

    private static bool IsNovelUpToDate(Novel novel, NovelDataBuffer novelDataBuffer, Uri novelTableOfContentsUri)
    {
        if (novel.Chapters.Count == 0)
        {
            return false;
        }

        if ((novel.CurrentChapterUrl == novelDataBuffer.CurrentChapterUrl) || novel.CurrentChapter == novelDataBuffer.MostRecentChapterTitle)
        {
            _logger.Warn($"Novel {novel.Title} with url {novelTableOfContentsUri} is up to date.\n\t\tCurrent chapter: {novelDataBuffer.MostRecentChapterTitle} Novel Id: {novel.Id}");
            return true;
        }

        var lastChapter = novel.Chapters.OrderBy(chapter => chapter.Number).LastOrDefault();
        if (lastChapter == null || lastChapter.Url != novelDataBuffer.CurrentChapterUrl ||
            lastChapter.Title != novelDataBuffer.MostRecentChapterTitle)
        {
            return false;
        }

        _logger.Warn($"Novel {novel.Title} with url {novelTableOfContentsUri} is up to date.\n\t\tCurrent chapter: {novelDataBuffer.MostRecentChapterTitle} Novel Id: {novel.Id}");
        return true;
    }

    private static List<ChapterLink> DetermineNewChaptersToScrape(string currentChapterUrl, ICollection<Chapter> savedChapters, Guid novelId, IList<ChapterLink> bufferChapterLinks)
    {
        if (savedChapters.Count == 0)
        {
            return bufferChapterLinks.ToList();
        }

        var indexOfLastChapter = bufferChapterLinks.FindIndex(cl => cl.Url == currentChapterUrl);
        if (indexOfLastChapter == -1 && savedChapters.Count != 0)
        {
            indexOfLastChapter = bufferChapterLinks.FindIndex(cl => cl.Url == savedChapters.Last().Url);
        }

        if (indexOfLastChapter != -1)
        {
            return bufferChapterLinks.Skip(indexOfLastChapter + 1).ToList();
        }

        _logger.Error($"A case where the last chapter is not in the database and the current chapter is not in the database has been found. Novel Id: {novelId}");
        var getDllLocation = Assembly.GetExecutingAssembly().Location;
        var getDllDir = Path.GetDirectoryName(getDllLocation);
        var mainDll = Path.Combine(getDllDir ?? string.Empty, _dllProjectName);
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write(File.Exists(mainDll)
            ? $"Please delete the novel from the database using\n\t\t{mainDll} delete_novel_by_id {novelId} and try again."
            : $"Please delete the novel from the database using\n\t\t{_projectName} delete_novel_by_id {novelId} and try again.");
        Console.ResetColor();
        return bufferChapterLinks.Skip(indexOfLastChapter + 1).ToList();
    }

    private static List<IncompleteChapterDownload> DetermineIncompleteChaptersToScrape(
        IEnumerable<Chapter> incompleteChapters,
        IList<ChapterLink> availableChapterLinks)
    {
        var incompleteChapterDownloads = new List<IncompleteChapterDownload>();

        foreach (var incompleteChapter in incompleteChapters)
        {
            var availableChapterLink = availableChapterLinks.FirstOrDefault(chapterLink =>
                string.Equals(chapterLink.Url, incompleteChapter.Url, StringComparison.Ordinal));
            var savedChapterNumber = (int)incompleteChapter.Number;
            if (availableChapterLink == null &&
                savedChapterNumber >= 1 &&
                savedChapterNumber <= availableChapterLinks.Count)
            {
                availableChapterLink = availableChapterLinks[savedChapterNumber - 1];
            }

            var chapterLink = availableChapterLink == null
                ? new ChapterLink
                {
                    Url = incompleteChapter.Url,
                    Title = incompleteChapter.Title,
                    ChapterNumber = savedChapterNumber
                }
                : new ChapterLink
                {
                    Url = availableChapterLink.Url,
                    Title = availableChapterLink.Title,
                    PremiumInfo = availableChapterLink.PremiumInfo,
                    ChapterNumber = availableChapterLink.ChapterNumber > 0
                        ? availableChapterLink.ChapterNumber
                        : savedChapterNumber
                };

            incompleteChapterDownloads.Add(new IncompleteChapterDownload(incompleteChapter, chapterLink));
        }

        return incompleteChapterDownloads;
    }

    /// <summary>
    /// Checks if we should skip updating a partial download that has no chapter range specified.
    /// Returns true if update should be skipped, false otherwise.
    /// </summary>
    /// <param name="novel">The novel being considered for update.</param>
    /// <param name="novelTableOfContentsUri">The table of contents url for the novel, used in the printed re-download instructions.</param>
    /// <param name="beginChapter">The requested starting chapter number, if any.</param>
    /// <param name="endChapter">The requested ending chapter number, if any.</param>
    /// <returns>True if the update should be skipped because the novel is a partial download with no explicit range requested; otherwise, false.</returns>
    private static bool ShouldSkipPartialDownloadUpdate(Novel novel, Uri novelTableOfContentsUri, int? beginChapter, int? endChapter)
    {
        // Only block partial downloads if NO chapter range is specified
        // If a range is specified, the overlap check above already validated it's a continuation
        if (!novel.IsPartialDownload || beginChapter.HasValue || endChapter.HasValue)
        {
            return false;
        }

        _logger.Info($"Skipping update for novel {novel.Title} (ID: {novel.Id}) - This is a partial download (volume/chapter range)");
        Console.WriteLine();
        var partialMessages = new[] { "PARTIAL DOWNLOAD DETECTED" };
        CommonHelper.DrawBox(partialMessages, ConsoleColor.Yellow);
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  Novel:           {novel.Title}");
        Console.WriteLine($"  Novel ID:        {novel.Id}");
        if (novel.ChapterRanges.Count != 0)
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
            SiteName = novelTableOfContentsUri.Host ?? string.Empty,
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
                Content = HtmlEntity.DeEntitize(data.Content ?? string.Empty),
                Title = HtmlEntity.DeEntitize(data.Title) ?? string.Empty,
                Number = data.SequenceNumber,
                DateCreated = DateTime.Now,
                DateLastModified = data.DateLastModified,
                IsPartial = data.IsPartial
            };
            chapter.SetPages(data.Pages?.Select(p => new Page { Url = p.Url }));
            return chapter;
        }).ToList();
    }

    /// <summary>
    /// Detects volume boundaries from chapter titles using common patterns.
    /// </summary>
    /// <param name="chapterTitles">The ordered list of chapter titles to scan for volume markers.</param>
    /// <returns>The list of detected volume ranges, each spanning the chapters belonging to that volume.</returns>
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
            var title = chapterTitles[i].ToLowerCase();
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
            if (currentVolumeStart != null)
            {
                volumes.Add(new VolumeRange
                {
                    Begin = currentVolumeStart.Value,
                    End = i,
                    Name = $"Volume {currentVolumeNumber.Value}"
                });
            }

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

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var volNum))
            {
                continue;
            }

            return volNum;
        }

        return null;
    }

    private async Task AddNewNovelAsync(Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy, Configuration configuration, int? beginChapter = null, int? endChapter = null)
    {
        using var novelDataBuffer = await scraperStrategy.ScrapeAsync().ConfigureAwait(false);
        if (novelDataBuffer.ChapterLinks.Count == 0)
        {
            _logger.Error("No chapters found for this novel");
            throw new InvalidOperationException($"No chapters found for novel at {novelTableOfContentsUri}. Cannot add to database.");
        }

        SelectedChapterRange? selectedRange = null;
        string? detectedVolumeName = null;

        if (novelDataBuffer.ChapterLinks.Count != 0)
        {
            _logger.Info("Using cached chapter titles for range selection");
            var chapterTitles = novelDataBuffer.ChapterLinks.Select(l => l.Title).OfType<string>().ToList();

            if (beginChapter.HasValue || endChapter.HasValue)
            {
                _logger.Info("Using chapter range from command line options");
                selectedRange = ChapterRangeSelector.GetRangeFromOptions(novelDataBuffer.ChapterLinks.Count, beginChapter, endChapter);
                ChapterRangeSelector.DisplayRangeInfo(selectedRange, chapterTitles);
                ChapterRangeSelector.ConfirmPremiumChapters(selectedRange, novelDataBuffer.ChapterLinks, novelDataBuffer.UserPremiumCurrencies, novelDataBuffer.IsLoggedIn);
            }
            else
            {
                selectedRange = ChapterRangeSelector.PromptUserForRange(novelDataBuffer.ChapterLinks, novelDataBuffer.UserPremiumCurrencies, novelDataBuffer.IsLoggedIn);

                if (selectedRange == null)
                {
                    var allChaptersRange = new SelectedChapterRange(1, novelDataBuffer!.ChapterLinks.Count);
                    ChapterRangeSelector.ConfirmPremiumChapters(allChaptersRange, novelDataBuffer.ChapterLinks, novelDataBuffer.UserPremiumCurrencies, novelDataBuffer.IsLoggedIn);
                }
            }

            if (selectedRange != null)
            {
                _logger.Info($"User selected chapter range: {selectedRange}");

                // Detect if this is a volume download
                var volumeRanges = DetectVolumes(chapterTitles);
                var matchingVolume = volumeRanges.FirstOrDefault(v =>
                    v.Begin == selectedRange.Begin && v.End == selectedRange.End);

                if (matchingVolume != null)
                {
                    detectedVolumeName = matchingVolume.Name;
                    _logger.Info($"Detected volume download: {detectedVolumeName}");
                }

                scraperStrategy.SetChapterRange(selectedRange, detectedVolumeName);
            }
        }

        var selectedRangeCoversAllAvailableChapters = selectedRange is { Begin: 1 } &&
                                                      selectedRange.End == novelDataBuffer.ChapterLinks.Count;
        var partialDownloadRange = selectedRangeCoversAllAvailableChapters ? null : selectedRange;
        var newNovel = CreateNovel(
            novelDataBuffer,
            novelTableOfContentsUri,
            partialDownloadRange,
            detectedVolumeName);
        _logger.Info("Finished populating Novel data for {0}", newNovel.Title);

        // Filter chapter URLs based on range if selected
        var chaptersToDownload = selectedRange != null
            ? novelDataBuffer.ChapterLinks.Skip(selectedRange.Begin - 1).Take(selectedRange.Count).ToList()
            : novelDataBuffer.ChapterLinks;

        scraperStrategy.SetSessionAuthenticated(novelDataBuffer.IsLoggedIn);
        var chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(chaptersToDownload).ConfigureAwait(false);
        newNovel.Chapters.ReplaceWith(CreateChapters(chapterDataBuffers, newNovel.Id));
        NovelChapterStateUpdater.UpdateDownloadedChapterBoundaries(newNovel);

        var userOutputDirectory = configuration.DetermineSaveLocation(scraperStrategy.GetSiteConfiguration().HasImagesForChapterContent);
        string outputDirectory = CommonHelper.GetOutputDirectoryForTitle(newNovel.Title, userOutputDirectory);

        _ = await novelService.CreateAsync(newNovel).ConfigureAwait(false);
        _logger.Info("Finished adding novel {0} to database", newNovel.Title);

        var filenameSuffix = GenerateFilenameSuffix(selectedRange, detectedVolumeName);

        if (newNovel.Chapters.Any(chapter => chapter?.Pages?.Count > 0))
        {
            if (configuration.DefaultMangaFileExtension == FileExtension.Pdf)
            {
                var (saveLocation, isFileSplit) = PdfGenerator.CreatePdf(newNovel, chapterDataBuffers, outputDirectory, configuration, filenameSuffix);
                newNovel.SaveLocation = saveLocation;
                newNovel.SavedFileIsSplit = isFileSplit;
                newNovel.FileType = NovelFileType.Pdf;
            }
            else
            {
                newNovel.SaveLocation = comicBookArchiveGenerator.CreateComicBookArchive(newNovel, chapterDataBuffers, outputDirectory, configuration, filenameSuffix);
                newNovel.FileType = Enum.TryParse(configuration.DefaultMangaFileExtension.ToString(), out NovelFileType convertedType)
                    ? convertedType : NovelFileType.Cbz; // check to see if converting by name works, if not default to cbz
            }

            foreach (var chapterDataBuffer in chapterDataBuffers)
            {
                chapterDataBuffer.Dispose();
            }
        }
        else
        {
            newNovel.SaveLocation = CreateEpub(newNovel, newNovel.Chapters, novelDataBuffer.ThumbnailImage?.ToArray(), outputDirectory, filenameSuffix);
            newNovel.FileType = NovelFileType.Epub;
        }

        await novelService.UpdateAsync(newNovel).ConfigureAwait(false);
    }

    private async Task ExpandPartialDownloadAsync(Novel novel, Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy, Configuration configuration, int? beginChapter = null, int? endChapter = null)
    {
        var incompleteChapters = novel.Chapters
            .Where(NovelChapterStateUpdater.IsIncompleteChapter)
            .OrderBy(chapter => chapter.Number)
            .ToList();
        using var novelDataBuffer = await scraperStrategy.ScrapeAsync().ConfigureAwait(false);

        if (novelDataBuffer.ChapterLinks.Count == 0)
        {
            _logger.Warn($"No chapter links found for {novel.Title}.");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No chapter links found on the table of contents page.");
            Console.ResetColor();
            return;
        }

        var chapterTitles = novelDataBuffer.ChapterLinks.Select(l => l.Title).OfType<string>().ToList();
        SelectedChapterRange? selectedRange = null;

        if (beginChapter.HasValue || endChapter.HasValue)
        {
            selectedRange = ChapterRangeSelector.GetRangeFromOptions(novelDataBuffer.ChapterLinks.Count, beginChapter, endChapter);
            ChapterRangeSelector.DisplayRangeInfo(selectedRange, chapterTitles);
            ChapterRangeSelector.ConfirmPremiumChapters(selectedRange, novelDataBuffer.ChapterLinks, novelDataBuffer.UserPremiumCurrencies, novelDataBuffer.IsLoggedIn);
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

            selectedRange = ChapterRangeSelector.PromptUserForRange(novelDataBuffer.ChapterLinks, novelDataBuffer.UserPremiumCurrencies, novelDataBuffer.IsLoggedIn);

            if (selectedRange == null)
            {
                selectedRange = new SelectedChapterRange(1, novelDataBuffer!.ChapterLinks.Count);
                ChapterRangeSelector.ConfirmPremiumChapters(selectedRange, novelDataBuffer.ChapterLinks, novelDataBuffer.UserPremiumCurrencies, novelDataBuffer.IsLoggedIn);
            }
        }

        var rangeChapterLinks = novelDataBuffer.ChapterLinks
            .Skip(selectedRange.Begin - 1)
            .Take(selectedRange.Count)
            .ToList();

        var existingUrls = novel.Chapters.Select(ch => ch.Url).ToHashSet();
        var newChapterLinks = rangeChapterLinks.Where(link => !existingUrls.Contains(link.Url)).ToList();
        var incompleteChapterDownloads = DetermineIncompleteChaptersToScrape(
                incompleteChapters,
                novelDataBuffer.ChapterLinks)
            .Where(download => download.ChapterLink.ChapterNumber >= selectedRange.Begin &&
                               download.ChapterLink.ChapterNumber <= selectedRange.End)
            .ToList();
        var incompleteChaptersByDownloadUrl = incompleteChapterDownloads
            .GroupBy(download => download.ChapterLink.Url, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().SavedChapter, StringComparer.Ordinal);
        var chapterLinksToDownload = newChapterLinks
            .Concat(incompleteChapterDownloads.Select(download => download.ChapterLink))
            .DistinctBy(chapterLink => chapterLink.Url, StringComparer.Ordinal)
            .OrderBy(chapterLink => chapterLink.ChapterNumber)
            .ToList();

        if (chapterLinksToDownload.Count == 0)
        {
            NovelChapterStateUpdater.UpdateDownloadedChapterBoundaries(novel);
            NovelChapterStateUpdater.ClearChapterRangesWhenAllAvailableChaptersAreDownloaded(
                novel,
                novelDataBuffer.ChapterLinks.Count);
            await novelService.UpdateAsync(novel).ConfigureAwait(false);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("All chapters in the selected range are already downloaded.");
            Console.ResetColor();
            return;
        }

        var skippedCount = rangeChapterLinks.Count - chapterLinksToDownload.Count;
        if (skippedCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                $"Skipping {skippedCount} complete chapter(s). Downloading {newChapterLinks.Count} new and " +
                $"retrying {incompleteChapterDownloads.Count} incomplete chapter(s).");
            Console.ResetColor();
        }

        string? detectedVolumeName = null;
        var volumeRanges = DetectVolumes(chapterTitles);
        var matchingVolume = volumeRanges.FirstOrDefault(v => v.Begin == selectedRange.Begin && v.End == selectedRange.End);
        if (matchingVolume != null)
        {
            detectedVolumeName = matchingVolume.Name;
            _logger.Info($"Detected volume download: {detectedVolumeName}");
        }

        scraperStrategy.SetSessionAuthenticated(novelDataBuffer.IsLoggedIn);
        var originalPageCountsByChapterNumber = novel.Chapters
            .ToDictionary(chapter => chapter.Number, chapter => chapter.Pages?.Count ?? 0);
        var chapterDataBuffers = await scraperStrategy
            .GetChaptersDataAsync(chapterLinksToDownload)
            .ConfigureAwait(false);
        var newChapterDataBuffers = new List<ChapterDataBuffer>();
        var recoveredChapterDataBuffers = new List<ChapterDataBuffer>();

        foreach (var chapterDataBuffer in chapterDataBuffers)
        {
            if (!incompleteChaptersByDownloadUrl.TryGetValue(chapterDataBuffer.Url, out var incompleteChapter))
            {
                newChapterDataBuffers.Add(chapterDataBuffer);
                continue;
            }

            if (NovelChapterStateUpdater.ApplyChapterRetry(
                    incompleteChapter,
                    chapterDataBuffer,
                    scraperStrategy.GetSiteConfiguration().HasImagesForChapterContent))
            {
                recoveredChapterDataBuffers.Add(chapterDataBuffer);
            }
        }

        var newChapters = CreateChapters(newChapterDataBuffers, novel.Id);

        NovelChapterStateUpdater.AddDownloadedChaptersAndUpdateBoundaries(novel, newChapters);
        novel.DateLastModified = DateTime.Now;

        NovelChapterStateUpdater.AddOrMergeDownloadedChapterRange(novel, selectedRange);
        var completedPartialDownload = NovelChapterStateUpdater.ClearChapterRangesWhenAllAvailableChaptersAreDownloaded(
            novel,
            novelDataBuffer.ChapterLinks.Count);
        if (completedPartialDownload)
        {
            _logger.Info($"All available chapters for {novel.Title} are downloaded. Removed partial-download ranges.");
        }

        var userOutputDirectory = configuration.DetermineSaveLocation(scraperStrategy.GetSiteConfiguration().HasImagesForChapterContent);
        var orderedRanges = novel.ChapterRanges.OrderBy(r => r.Begin).ToList();
        var filenameSuffix = orderedRanges.Count switch
        {
            0 => string.Empty,
            1 => GenerateFilenameSuffix(new SelectedChapterRange(orderedRanges[0].Begin, orderedRanges[0].End), detectedVolumeName),
            _ => "Ch " + string.Join(" & ", orderedRanges.Select(r => $"{r.Begin}-{r.End}"))
        };

        if (newChapters.Count != 0 || recoveredChapterDataBuffers.Count != 0)
        {
            await HandleFileTypeUpdatesAsync(
                novel,
                novelDataBuffer,
                chapterDataBuffers,
                newChapters,
                configuration,
                userOutputDirectory,
                filenameSuffix,
                originalPageCountsByChapterNumber).ConfigureAwait(false);
        }
        else
        {
            foreach (var chapterDataBuffer in chapterDataBuffers)
            {
                chapterDataBuffer.Dispose();
            }

            await novelService.UpdateAsync(novel).ConfigureAwait(false);
        }
    }

    private async Task UpdateExistingNovelAsync(Novel novel, Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy, Configuration configuration, int? beginChapter = null, int? endChapter = null, bool confirmPremiumChapters = true)
    {
        if (ShouldSkipPartialDownloadUpdate(novel, novelTableOfContentsUri, beginChapter, endChapter))
        {
            return;
        }

        var incompleteChapters = novel.Chapters
            .Where(NovelChapterStateUpdater.IsIncompleteChapter)
            .OrderBy(chapter => chapter.Number)
            .ToList();
        using var novelDataBuffer = await scraperStrategy.ScrapeAsync().ConfigureAwait(false);

        // Only skip when no explicit range is requested and every saved chapter is complete.
        if (!beginChapter.HasValue &&
            !endChapter.HasValue &&
            incompleteChapters.Count == 0 &&
            IsNovelUpToDate(novel, novelDataBuffer, novelTableOfContentsUri))
        {
            novel.DateLastModified = DateTime.Now;
            await novelService.UpdateAsync(novel).ConfigureAwait(false);
            return;
        }

        var novelHadDownloadedChapters = novel.Chapters.Count != 0;
        var previouslyDownloadedChapterIndex = novelHadDownloadedChapters
            ? novelDataBuffer.ChapterLinks.FindIndex(chapterLink => chapterLink.Url == novel.CurrentChapterUrl) + 1
            : 0;
        if (previouslyDownloadedChapterIndex == 0 && novel.Chapters.Count != 0)
        {
            previouslyDownloadedChapterIndex = (int)novel.Chapters.Max(chapter => chapter.Number);
        }

        var sortedSavedChapters = CommonHelper.SortNovelChaptersByNumber(novel.Chapters);
        var newChapterLinks = DetermineNewChaptersToScrape(novel.CurrentChapterUrl, sortedSavedChapters, novel.Id, novelDataBuffer.ChapterLinks);

        SelectedChapterRange? selectedRange = null;
        var selectedRangeCreatesGap = false;

        if (beginChapter.HasValue || endChapter.HasValue)
        {
            selectedRange = ChapterRangeSelector.GetRangeFromOptions(novelDataBuffer.ChapterLinks.Count, beginChapter, endChapter);
            ChapterRangeSelector.DisplayRangeInfo(selectedRange, novelDataBuffer.ChapterLinks.Select(c => c.Title).ToList()!);
            if (confirmPremiumChapters)
            {
                ChapterRangeSelector.ConfirmPremiumChapters(selectedRange, novelDataBuffer.ChapterLinks, novelDataBuffer.UserPremiumCurrencies, novelDataBuffer.IsLoggedIn);
            }

            newChapterLinks = newChapterLinks.Where(link =>
            {
                var chapterNumber = novelDataBuffer!.ChapterLinks.FindIndex(c => c.Url == link.Url) + 1;
                return chapterNumber >= selectedRange.Begin && chapterNumber <= selectedRange.End;
            }).ToList();
            selectedRangeCreatesGap = selectedRange.Begin > previouslyDownloadedChapterIndex + 1;
        }
        else if (confirmPremiumChapters && newChapterLinks.Count != 0)
        {
            // When downloading all new chapters, still check for premium chapters
            var firstNewChapterIndex = novelDataBuffer.ChapterLinks.FindIndex(c => c.Url == newChapterLinks.First().Url) + 1;
            var lastNewChapterIndex = novelDataBuffer.ChapterLinks.FindIndex(c => c.Url == newChapterLinks.Last().Url) + 1;
            var newChaptersRange = new SelectedChapterRange(firstNewChapterIndex, lastNewChapterIndex);
            ChapterRangeSelector.ConfirmPremiumChapters(newChaptersRange, novelDataBuffer.ChapterLinks, novelDataBuffer.UserPremiumCurrencies, novelDataBuffer.IsLoggedIn);
        }

        var incompleteChapterDownloads = DetermineIncompleteChaptersToScrape(
            incompleteChapters,
            novelDataBuffer.ChapterLinks);
        if (selectedRange is not null)
        {
            incompleteChapterDownloads = incompleteChapterDownloads
                .Where(download => download.ChapterLink.ChapterNumber >= selectedRange.Begin &&
                                   download.ChapterLink.ChapterNumber <= selectedRange.End)
                .ToList();
        }

        var incompleteChaptersByDownloadUrl = incompleteChapterDownloads
            .GroupBy(download => download.ChapterLink.Url, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().SavedChapter, StringComparer.Ordinal);
        var chapterLinksToDownload = newChapterLinks
            .Concat(incompleteChapterDownloads.Select(download => download.ChapterLink))
            .DistinctBy(chapterLink => chapterLink.Url, StringComparer.Ordinal)
            .OrderBy(chapterLink => chapterLink.ChapterNumber)
            .ToList();

        if (chapterLinksToDownload.Count == 0)
        {
            NovelChapterStateUpdater.UpdateDownloadedChapterBoundaries(novel);
            await novelService.UpdateAsync(novel).ConfigureAwait(false);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(selectedRange == null
                ? "No new chapters are available."
                : "All chapters in the selected range are already downloaded.");
            Console.ResetColor();
            return;
        }

        if (incompleteChapterDownloads.Count != 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                $"Retrying {incompleteChapterDownloads.Count} incomplete chapter(s) while checking for new chapters.");
            Console.ResetColor();
        }

        scraperStrategy.SetSessionAuthenticated(novelDataBuffer.IsLoggedIn);
        var originalPageCountsByChapterNumber = novel.Chapters
            .ToDictionary(chapter => chapter.Number, chapter => chapter.Pages?.Count ?? 0);
        var chapterDataBuffers = await scraperStrategy
            .GetChaptersDataAsync(chapterLinksToDownload)
            .ConfigureAwait(false);
        var newChapterDataBuffers = new List<ChapterDataBuffer>();
        var recoveredChapterDataBuffers = new List<ChapterDataBuffer>();

        foreach (var chapterDataBuffer in chapterDataBuffers)
        {
            if (!incompleteChaptersByDownloadUrl.TryGetValue(chapterDataBuffer.Url, out var incompleteChapter))
            {
                newChapterDataBuffers.Add(chapterDataBuffer);
                continue;
            }

            if (NovelChapterStateUpdater.ApplyChapterRetry(
                    incompleteChapter,
                    chapterDataBuffer,
                    scraperStrategy.GetSiteConfiguration().HasImagesForChapterContent))
            {
                recoveredChapterDataBuffers.Add(chapterDataBuffer);
            }
        }

        var newChapters = CreateChapters(newChapterDataBuffers, novel.Id);
        var userOutputDirectory = configuration.DetermineSaveLocation(scraperStrategy.GetSiteConfiguration().HasImagesForChapterContent);
        NovelChapterStateUpdater.UpdateNovelWithDownloadedChapters(
            novel,
            novelDataBuffer,
            newChapters);

        var selectedRangeCreatesPartialDownloadFromEmptyNovel = selectedRange is not null &&
                                                                !novelHadDownloadedChapters &&
                                                                (selectedRange.Begin != 1 ||
                                                                 selectedRange.End != novelDataBuffer.ChapterLinks.Count);
        if (newChapters.Count != 0 &&
            selectedRange is not null &&
            (selectedRangeCreatesGap || selectedRangeCreatesPartialDownloadFromEmptyNovel))
        {
            if (previouslyDownloadedChapterIndex > 0)
            {
                NovelChapterStateUpdater.AddOrMergeDownloadedChapterRange(
                    novel,
                    new SelectedChapterRange(1, previouslyDownloadedChapterIndex));
            }

            NovelChapterStateUpdater.AddOrMergeDownloadedChapterRange(novel, selectedRange);
        }

        var downloadedRanges = novel.ChapterRanges.OrderBy(range => range.Begin).ToList();
        var filenameSuffix = downloadedRanges.Count switch
        {
            0 => string.Empty,
            1 => GenerateFilenameSuffix(
                new SelectedChapterRange(downloadedRanges[0].Begin, downloadedRanges[0].End),
                null),
            _ => "Ch " + string.Join(" & ", downloadedRanges.Select(range => $"{range.Begin}-{range.End}"))
        };

        if (newChapters.Count != 0 || recoveredChapterDataBuffers.Count != 0)
        {
            await HandleFileTypeUpdatesAsync(
                novel,
                novelDataBuffer,
                chapterDataBuffers,
                newChapters,
                configuration,
                userOutputDirectory,
                filenameSuffix,
                originalPageCountsByChapterNumber).ConfigureAwait(false);
        }
        else
        {
            foreach (var chapterDataBuffer in chapterDataBuffers)
            {
                chapterDataBuffer.Dispose();
            }

            await novelService.UpdateAsync(novel).ConfigureAwait(false);
        }

        if (incompleteChapterDownloads.Count != 0)
        {
            var stillIncompleteCount = incompleteChapterDownloads.Count - recoveredChapterDataBuffers.Count;
            Console.ForegroundColor = stillIncompleteCount == 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine(
                $"Incomplete chapter retry: {recoveredChapterDataBuffers.Count} recovered, {stillIncompleteCount} still incomplete.");
            Console.ResetColor();
        }
    }

    private async Task<Novel?> GetNovelFromDataBase(Guid id)
    {
        var novel = await novelService.GetByIdAsync(id).ConfigureAwait(false);
        novel?.Chapters.ReplaceWith(novel.Chapters.OrderBy(chapter => chapter.Number));

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

    private async Task HandleFileTypeUpdatesAsync(
        Novel novel,
        NovelDataBuffer novelDataBuffer,
        List<ChapterDataBuffer> chapterDataBuffers,
        List<Chapter> newChapters,
        Configuration configuration,
        string userOutputDirectory,
        string filenameSuffix = "",
        IReadOnlyDictionary<float, int>? originalPageCountsByChapterNumber = null)
    {
        var outputDirectory = CommonHelper.GetOutputDirectoryForTitle(novel.Title, userOutputDirectory);

        if (newChapters.All(chapter => chapter?.Pages == null || chapter.Pages.Count == 0) && novel.FileType == NovelFileType.Epub)
        {
            var sortedChapters = CommonHelper.SortNovelChaptersByNumber(novel.Chapters).ToList();
            novel.SaveLocation = CreateEpub(novel, sortedChapters, novelDataBuffer.ThumbnailImage?.ToArray(), outputDirectory, filenameSuffix);
            await novelService.UpdateAndAddChaptersAsync(novel, newChapters).ConfigureAwait(false);

            foreach (var chapterDataBuffer in chapterDataBuffers)
            {
                chapterDataBuffer.Dispose();
            }

            return;
        }

        var successfulImageChapterDataBuffers = chapterDataBuffers
            .Where(chapterDataBuffer =>
                NovelChapterStateUpdater.IsSuccessfulChapterDownload(chapterDataBuffer, true) &&
                chapterDataBuffer.Pages is { Count: > 0 })
            .ToList();

        if (successfulImageChapterDataBuffers.Count == 0)
        {
            foreach (var chapterDataBuffer in chapterDataBuffers)
            {
                chapterDataBuffer.Dispose();
            }

            await novelService.UpdateAndAddChaptersAsync(novel, newChapters).ConfigureAwait(false);
            return;
        }

        // If the save location is null, assume the novel is a PDF added before CBZ support.
        if (string.IsNullOrEmpty(novel.SaveLocation))
        {
            novel.SaveLocation = Path.Combine(outputDirectory, CommonHelper.SanitizeFileName(novel.Title) + PdfGenerator.PdfFileExtension);
        }

        if (novel.FileType == NovelFileType.Pdf)
        {
            if (novel.SavedFileIsSplit)
            {
                PdfGenerator.CreatePdfByChapter(novel, successfulImageChapterDataBuffers, novel.SaveLocation);
            }
            else
            {
                PdfGenerator.UpdatePdf(
                    novel,
                    successfulImageChapterDataBuffers,
                    configuration,
                    originalPageCountsByChapterNumber);
            }
        }
        else
        {
            novel.SaveLocation = comicBookArchiveGenerator.UpdateComicBookArchive(
                novel,
                successfulImageChapterDataBuffers,
                outputDirectory,
                configuration);
        }

        foreach (var chapterDataBuffer in chapterDataBuffers)
        {
            chapterDataBuffer.Dispose();
        }

        await novelService.UpdateAndAddChaptersAsync(novel, newChapters).ConfigureAwait(false);
    }

    private bool IsThereConfigurationForSite(Uri novelTableOfContentsUri)
    {
        var siteConfigurations = _novelScraperSettings.SiteConfigurations;
        return siteConfigurations.Any(config =>
            config.IsActive &&
            novelTableOfContentsUri.Host.Contains(config.UrlPattern, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ConfigureFlareSolverrAsync(ScraperStrategy scraperStrategy, SiteConfiguration siteConfiguration)
    {
        var flareSolverrSettings = _novelScraperSettings.FlareSolverrSettings;
        if (flareSolverrSettings?.Enabled != true)
        {
            if (siteConfiguration.RequiresFlareSolverr)
            {
                throw new InvalidOperationException(
                    $"{siteConfiguration.SiteName} requires FlareSolverr. Enable it in appsettings.json and run 'docker compose up -d flaresolverr'.");
            }

            return;
        }

        var flareSolverrUrl = flareSolverrSettings.Url ?? "http://localhost:8191";
        var flareSolverrIsAvailable = await scraperStrategy.EnableFlareSolverrAsync(
            flareSolverrUrl,
            siteConfiguration.RequiresFlareSolverr).ConfigureAwait(false);

        if (siteConfiguration.RequiresFlareSolverr && !flareSolverrIsAvailable)
        {
            throw new InvalidOperationException(
                $"{siteConfiguration.SiteName} requires FlareSolverr, but it is not available at {flareSolverrUrl}. Run 'docker compose up -d flaresolverr' and try again.");
        }
    }

    private SiteConfiguration GetSiteConfiguration(Uri novelTableOfContentsUri)
    {
        var siteConfigurations = _novelScraperSettings.SiteConfigurations;
        return siteConfigurations.First(config =>
            config.IsActive &&
            novelTableOfContentsUri.Host.Contains(config.UrlPattern, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class VolumeRange
    {
        public int Begin { get; set; }

        public int End { get; set; }

        public required string Name { get; set; }
    }

    private sealed record IncompleteChapterDownload(Chapter SavedChapter, ChapterLink ChapterLink);
}