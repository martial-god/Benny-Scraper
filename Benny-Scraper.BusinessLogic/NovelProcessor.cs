using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.FileGenerators;
using Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;
using Benny_Scraper.BusinessLogic.Helper;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.BusinessLogic.Utilities;
using Benny_Scraper.BusinessLogic.Validators;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using NLog;
using System.Reflection;
using Configuration = Benny_Scraper.Models.Configuration;

namespace Benny_Scraper.BusinessLogic;

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
        scraperStrategy.SetLoginPreference(withLogin);

        if (novel == null) // Novel is not in database so add it
        {
            Logger.Info($"Novel with url {novelTableOfContentsUri} is not in database, adding it now.");
            await AddNewNovelAsync(novelTableOfContentsUri, scraperStrategy, configuration, beginChapter, endChapter); // consider creating something to decide whi
            Logger.Info($"Added novel with url {novelTableOfContentsUri} to database.");
        }
        else // make changes or update novelToAdd and newChapters
        {
            var validator = new ValidateObject();
            var errors = validator.Validate(novel);
            Logger.Info($"Novel {novel.Title} found with url {novelTableOfContentsUri} is in database, updating it now. Novel Id: {novel.Id}");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Current saved novel chapter: {novel.CurrentChapter}");
            Console.WriteLine($"Date Created: {novel.DateCreated}");
            Console.WriteLine($"Date Last Updated: {novel.DateLastModified}\n");
            Console.ResetColor();
            await UpdateExistingNovelAsync(novel, novelTableOfContentsUri, scraperStrategy, configuration);
        }

    }

    #region Private Methods
    private async Task AddNewNovelAsync(Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy, Configuration configuration, int? beginChapter = null, int? endChapter = null)
    {
        using var novelDataBuffer = await scraperStrategy.ScrapeAsync();

        ChapterRange? selectedRange = null;
        string? detectedVolumeName = null;
        var chapterRangeSelector = new ChapterRangeSelector();

        if (novelDataBuffer.ChapterLinks.Any())
        {
            Logger.Info("Using cached chapter titles for range selection");
            var chapterTitles = novelDataBuffer.ChapterLinks.Select(l => l.Title).ToList();
            // while (chapterTitles.Count < novelDataBuffer.ChapterLinks.Count)
            // {
            //     chapterTitles.Add($"Chapter {chapterTitles.Count + 1}");
            // }

            if (beginChapter.HasValue || endChapter.HasValue)
            {
                Logger.Info("Using chapter range from command line options");
                selectedRange = chapterRangeSelector.GetRangeFromOptions(novelDataBuffer.ChapterLinks.Count, beginChapter, endChapter);
                chapterRangeSelector.DisplayRangeInfo(selectedRange, chapterTitles);
                chapterRangeSelector.ConfirmPremiumChapters(selectedRange, novelDataBuffer.ChapterLinks, novelDataBuffer?.UserPremiumCurrencies);
            }
            else
            {
                selectedRange = chapterRangeSelector.PromptUserForRange(novelDataBuffer.ChapterLinks, novelDataBuffer?.UserPremiumCurrencies);
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

        IEnumerable<ChapterDataBuffer> chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(chaptersToDownload);
        newNovel.Chapters = CreateChapters(chapterDataBuffers, newNovel.Id);

        var userOutputDirectory = configuration.DetermineSaveLocation((bool)(scraperStrategy.GetSiteConfiguration()?.HasImagesForChapterContent));
        string outputDirectory = CommonHelper.GetOutputDirectoryForTitle(newNovel.Title, outputDirectory = userOutputDirectory);

        var novelId = await novelService.CreateAsync(newNovel);
        Logger.Info("Finished adding novel {0} to database", newNovel.Title);
        var novel = await GetNovelFromDataBase(novelId);

        if (novel == null)
        {
            // This should never happen, somehow some idiot deleted the novel after it was just added within milliseconds, proud of you idiot.
            Logger.Warn($"Novel with url {novelTableOfContentsUri} could not be found in the database, even though it should. Novel will not be stored in database.");
            novel = newNovel;
        }
        else
            Logger.Info($"Novel {novel.Title} found with url {novelTableOfContentsUri} is in database, updating it now. Novel Id: {novel.Id}");

        var filenameSuffix = GenerateFilenameSuffix(selectedRange, detectedVolumeName);

        if (novel.Chapters.Any(chapter => chapter?.Pages != null))
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
            novel.SaveLocation = CreateEpub(novel, novel.Chapters, novelDataBuffer.ThumbnailImage, outputDirectory, filenameSuffix);
            novel.FileType = NovelFileType.Epub;
        }
        await novelService.UpdateAsync(novel);
    }

    private async Task UpdateExistingNovelAsync(Novel novel, Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy, Configuration configuration)
    {
        if (novel == null) throw new ArgumentNullException(nameof(novel));
        if (novel!.IsPartialDownload)
        {
            Logger.Info($"Skipping update for novel {novel.Title} (ID: {novel.Id}) - This is a partial download (volume/chapter range)");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Skipping '{novel.Title}' - Partial downloads are not automatically updated.");
            Console.ResetColor();
            return;
        }

        using var novelDataBuffer = await scraperStrategy.ScrapeAsync();

        if (IsNovelUpToDate(novel, novelDataBuffer, novelTableOfContentsUri))
        {
            novel.DateLastModified = DateTime.Now;
            await novelService.UpdateAsync(novel);
            return;
        }

        var sortedSavedChapters = CommonHelper.SortNovelChaptersByDateCreated(novel.Chapters);
        var chapterUrls = novelDataBuffer.ChapterLinks.Select(chapterLink => chapterLink.Url).ToList();
        var newChapterLinks = DetermineNewChaptersToScrape(novel.CurrentChapterUrl, sortedSavedChapters, novel.Id, novelDataBuffer.ChapterLinks);

        IEnumerable<ChapterDataBuffer> chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(newChapterLinks);
        var newChapters = CreateChapters(chapterDataBuffers, novel.Id);
        var userOutputDirectory = configuration.DetermineSaveLocation((bool)(scraperStrategy.GetSiteConfiguration()?.HasImagesForChapterContent));
        UpdateNovel(novel, novelDataBuffer, newChapters);

        await HandleFileTypeUpdatesAsync(novel, novelDataBuffer, chapterDataBuffers, newChapters, configuration, userOutputDirectory);
    }

    private async Task<Novel?> GetNovelFromDataBase(Guid id)
    {
        var novel = await novelService.GetByIdAsync(id);
        if (novel != null)
            novel.Chapters = novel.Chapters.OrderBy(chapter => chapter.Number).ToList();
        return novel;
    }

    private string CreateEpub(Novel? novel, ICollection<Chapter> chapters, byte[]? thumbnailImage, string outputDirectory, string filenameSuffix = "")
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
    private static string GenerateFilenameSuffix(ChapterRange? chapterRange, string? detectedVolumeName)
    {
        if (chapterRange == null)
            return string.Empty;

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
            novel.Url = novelDataBuffer.NovelUrl;
        if (novelDataBuffer.Genres.Count != 0)
            novel.Genre = string.Join(", ", novelDataBuffer.Genres);
    }

    private async Task HandleFileTypeUpdatesAsync(Novel novel, NovelDataBuffer novelDataBuffer, IEnumerable<ChapterDataBuffer> chapterDataBuffers, List<Chapter> newChapters, Configuration configuration, string userOutputDirectory)
    {
        var outputDirectory = CommonHelper.GetOutputDirectoryForTitle(novel.Title, userOutputDirectory);

        if (newChapters.All(chapter => chapter?.Pages == null) && novel.FileType == NovelFileType.Epub)
        {
            var sortedChapters = CommonHelper.SortNovelChaptersByDateCreated(novel.Chapters);
            novel.SaveLocation = CreateEpub(novel, sortedChapters, novelDataBuffer.ThumbnailImage, outputDirectory);
            await novelService.UpdateAndAddChaptersAsync(novel, newChapters);
            return;
        }

        if (string.IsNullOrEmpty(novel.SaveLocation)) // assume that if the save location is null, then the novel is a pdf and was added before the cbz feature was added
            novel.SaveLocation = Path.Combine(outputDirectory, CommonHelper.SanitizeFileName(novel.Title) + PdfGenerator.PdfFileExtension);
        if (novel.FileType == NovelFileType.Pdf)
        {
            if (novel.SavedFileIsSplit)
                pdfGenerator.CreatePdfByChapter(novel, chapterDataBuffers, novel.SaveLocation);
            else
                pdfGenerator.UpdatePdf(novel, chapterDataBuffers, configuration);
        }
        else
            comicBookArchiveGenerator.UpdateComicBookArchive(novel, chapterDataBuffers, outputDirectory, configuration);
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
            lastChapter.Title != novelDataBuffer.MostRecentChapterTitle) return false;
        Logger.Warn($"Novel {novel.Title} with url {novelTableOfContentsUri} is up to date.\n\t\tCurrent chapter: {novelDataBuffer.MostRecentChapterTitle} Novel Id: {novel.Id}");
        return true;
    }

    private static List<ChapterLink> DetermineNewChaptersToScrape(string currentChapterUrl, ICollection<Chapter> savedChapters, Guid novelId, List<ChapterLink> bufferChapterLinks)
    {
        var indexOfLastChapter = bufferChapterLinks.FindIndex(cl => cl.Url == currentChapterUrl);
        if (indexOfLastChapter == -1 && savedChapters.Any())
            indexOfLastChapter = bufferChapterLinks.FindIndex(cl => cl.Url == savedChapters.Last().Url);
        if (indexOfLastChapter != -1)
            return bufferChapterLinks.Skip(indexOfLastChapter + 1).ToList();

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

    private static Novel CreateNovel(NovelDataBuffer novelDataBuffer, Uri novelTableOfContentsUri, ChapterRange? chapterRange = null)
    {
        return new Novel
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
            CurrentChapterUrl = novelDataBuffer.CurrentChapterUrl ?? string.Empty,
            ChapterRangeBegin = chapterRange?.Begin,
            ChapterRangeEnd = chapterRange?.End,
            IsPartialDownload = chapterRange != null
        };
    }

    private static List<Chapter> CreateChapters(IEnumerable<ChapterDataBuffer> chapterDataBuffers, Guid novelId)
    {
        return chapterDataBuffers.Select(data => new Chapter
        {
            NovelId = novelId,
            Url = data.Url ?? string.Empty,
            Content = HtmlEntity.DeEntitize(data.Content),
            Title = HtmlEntity.DeEntitize(data.Title) ?? string.Empty,
            Number = data.SequenceNumber,
            Pages = data.Pages?.Select(p => new Page
            {
                Url = p.Url,
                Image = null,
            }).ToList(),
            DateCreated = DateTime.Now,
            DateLastModified = data.DateLastModified
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
            int? detectedVolume = null;

            foreach (var pattern in volumePatterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(title, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!match.Success || match.Groups.Count <= 1) continue;
                if (!int.TryParse(match.Groups[1].Value, out var volNum)) continue;
                detectedVolume = volNum;
                break;
            }

            if (!detectedVolume.HasValue)
                continue;

            if (currentVolumeNumber.HasValue && detectedVolume.Value != currentVolumeNumber.Value)
            {
                // End previous volume and start new one
                if (currentVolumeStart.HasValue)
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
                continue;
            }

            // Start first volume
            if (currentVolumeNumber.HasValue) continue;
            currentVolumeStart = i + 1;
            currentVolumeNumber = detectedVolume.Value;
        }

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

    private class VolumeRange
    {
        public int Begin { get; set; }
        public int End { get; set; }
        public string Name { get; set; }
    }
    #endregion
}
