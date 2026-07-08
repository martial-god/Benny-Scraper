using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Utilities;
using BennyScraper.Models;
using HtmlAgilityPack;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Cookie = System.Net.Cookie;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy
{
    namespace Impl
    {
        // The NovelDataInitializer represents an abstraction around populating a NovelDataBuffer object with its required content.
        // Each strategy implements a corresponding child of the NovelDataInitializer class which is used to neatly
        // encapsulate the strategy-specific content fetching functions in the Impl namespace.
        //
        // Note that the data & methods used by the Initializer are static so that the strategies do not have to
        // contain a data member of the class in order to call the given FetchNovelContent method (implemented on each
        // child class).
        public class NovelDataInitializer
        {
            public enum Attr
            {
                Title,
                Author,
                Category,
                NovelRating,
                TotalRatings,
                Description,
                Genres,
                AlternativeNames,
                NovelStatus,
                ThumbnailUrl,
                LastTableOfContentsPage,
                ChapterUrls,
                FirstChapterUrl,
                CurrentChapter,
                AlternateLastTableOfContentsPage
            }

            public static async Task FetchContentByAttributeAsync(Attr attr, NovelDataBuffer novelDataBuffer,
                HtmlDocument htmlDocument, ScraperData scraperData)
            {
                switch (attr)
                {
                    case Attr.Title:
                        var titleNodes =
                            htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.TableOfContents
                                .NovelTitle);
                        if (titleNodes != null && titleNodes.Any())
                        {
                            novelDataBuffer.Title = HtmlEntity.DeEntitize(titleNodes.First().InnerText.Trim());
                        }

                        Console.WriteLine($"Title: {novelDataBuffer.Title}");
                        break;

                    case Attr.Author:
                        var authorNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.TableOfContents
                                .NovelAuthor);
                        novelDataBuffer.Author = authorNode != null
                            ? HtmlEntity.DeEntitize(authorNode.InnerText.Trim())
                            : string.Empty;
                        Console.WriteLine($"Author: {novelDataBuffer.Author}");
                        break;

                    case Attr.Category:
                        // TODO: Implement
                        break;

                    case Attr.NovelRating:
                        var novelRatingNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.TableOfContents
                                .NovelRating);
                        if (novelRatingNode != null &&
                            double.TryParse(novelRatingNode.InnerText.Trim(), out double rating))
                        {
                            novelDataBuffer.Rating = rating;
                            Console.WriteLine($"Rating: {novelDataBuffer.Rating}");
                        }

                        break;

                    case Attr.TotalRatings:
                        var totalRatingsNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.TableOfContents
                                .TotalRatings);
                        if (totalRatingsNode != null &&
                            int.TryParse(totalRatingsNode.InnerText.Trim(), out int totalRatings))
                        {
                            novelDataBuffer.TotalRatings = totalRatings;
                            Console.WriteLine($"Total Ratings: {novelDataBuffer.TotalRatings}");
                        }

                        break;

                    case Attr.Description:
                        var descriptionNodes =
                            htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.TableOfContents
                                .NovelDescription);
                        if (descriptionNodes != null)
                        {
                            novelDataBuffer.Description.ReplaceWith(descriptionNodes
                                .Select(description => HtmlEntity.DeEntitize(description.InnerText.Trim())));
                            Console.WriteLine($"Description line count: {novelDataBuffer.Description.Count}");
                        }

                        break;

                    case Attr.Genres:
                        var genreNodes =
                            htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.TableOfContents
                                .NovelGenres);
                        if (genreNodes != null && genreNodes.Count != 0)
                        {
                            novelDataBuffer.Genres.ReplaceWith(genreNodes
                                .Select(genre => HtmlEntity.DeEntitize(genre.InnerText.Trim())));
                            Console.WriteLine($"Total Genres: {novelDataBuffer.Genres.Count}");
                            Console.WriteLine($"Genres: {string.Join(", ", novelDataBuffer.Genres)}");
                        }

                        break;

                    case Attr.AlternativeNames:
                        var alternateNameNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors
                            .TableOfContents.NovelAlternativeNames);
                        if (alternateNameNodes != null && alternateNameNodes.Count != 0)
                        {
                            List<string> alternateNames = alternateNameNodes.Select(alternateName =>
                                HtmlEntity.DeEntitize(alternateName.InnerText.Trim())).ToList();
                            if (alternateNames.Any())
                            {
                                // SelectMany flattens a list of lists into a single list.
                                novelDataBuffer.AlternativeNames.ReplaceWith(alternateNames
                                    .SelectMany(altName => SplitByLanguage(altName)));
                            }
                        }

                        Console.WriteLine($"Checked for alternate names");
                        break;

                    case Attr.NovelStatus:
                        var statusNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.TableOfContents
                                .NovelStatus);
                        if (statusNode != null && scraperData?.SiteConfig?.CompletedStatus != null)
                        {
                            novelDataBuffer.NovelStatus = statusNode.InnerText.Trim();
                            novelDataBuffer.IsNovelCompleted = novelDataBuffer.NovelStatus.ToLowerInvariant()
                                .Contains(scraperData.SiteConfig.CompletedStatus);
                            Console.WriteLine($"NovelStatus: {novelDataBuffer.NovelStatus}");
                        }

                        break;

                    case Attr.ThumbnailUrl:
                        var urlNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors
                            .TableOfContents.NovelThumbnailUrl);

                        // Guard: Check if URL node exists and has the required attribute
                        if (urlNode == null ||
                            urlNode.Attributes
                                [scraperData.SiteConfig?.Selectors.TableOfContents.ThumbnailUrlAttribute] == null)
                        {
                            break;
                        }

                        var url = urlNode
                            .Attributes[scraperData.SiteConfig?.Selectors.TableOfContents.ThumbnailUrlAttribute].Value;
                        bool isValidHttpUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                                              (uriResult.Scheme == Uri.UriSchemeHttp ||
                                               uriResult.Scheme == Uri.UriSchemeHttps);
                        Uri absoluteUri = isValidHttpUrl ? new Uri(url) : new Uri(scraperData.BaseUri, url);

                        using (var client = scraperData.HttpClientFactory?.CreateClient() ?? new HttpClient())
                        {
                            try
                            {
                                var thumbnailBytes = await client.GetByteArrayAsync(absoluteUri);
                                novelDataBuffer.SetThumbnailImage(thumbnailBytes);
                            }
                            catch (Exception e)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(
                                    $"Failed to download thumbnail image for novel {novelDataBuffer.Title} at url {absoluteUri}. Exception: {e}");
                                Console.ResetColor();
                            }
                        }

                        novelDataBuffer.ThumbnailUrl = url;
                        break;

                    case Attr.LastTableOfContentsPage:
                        try
                        {
                            var lastTableOfContentsPageNode =
                                htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors
                                    .TableOfContents.LastTableOfContentsPage);

                            // Guard: Check if node exists and has href attribute
                            if (lastTableOfContentsPageNode == null ||
                                lastTableOfContentsPageNode.Attributes["href"] == null)
                            {
                                break;
                            }

                            novelDataBuffer.LastTableOfContentsPageUrl =
                                lastTableOfContentsPageNode.Attributes["href"].Value;
                            Console.WriteLine(
                                $"Last Table of Contents Page: {novelDataBuffer.LastTableOfContentsPageUrl}");
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(
                                $"Failed to get last table of contents page for novel {novelDataBuffer.Title} at url {scraperData.SiteTableOfContents}. Exception: {e}");
                            Console.ResetColor();
                            throw;
                        }

                    case Attr.ChapterUrls:
                        var chapterLinkNodes =
                            htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.TableOfContents
                                .ChapterLinks);

                        if (chapterLinkNodes == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(
                                $"No chapter link nodes found for novel {novelDataBuffer.Title} at url {scraperData.BaseUri}");
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine(
                                $"Please check the appsettings.json for {scraperData.SiteConfig.Name} the chapterLinks key");
                            Console.ResetColor();
                            break;
                        }

                        foreach (var chapterLinkNode in chapterLinkNodes)
                        {
                            var isPremium = !string.IsNullOrEmpty(scraperData.SiteConfig.Selectors.TableOfContents
                                .PremiumChapterSelectors.PremiumIndicator)
                                ? chapterLinkNode.SelectSingleNode(
                                    scraperData.SiteConfig.Selectors.TableOfContents.PremiumChapterSelectors
                                        .PremiumIndicator!) != null
                                : false;
                            var premiumCost = isPremium && !string.IsNullOrEmpty(scraperData.SiteConfig.Selectors
                                .TableOfContents.PremiumChapterSelectors.PremiumCost)
                                ? chapterLinkNode.SelectSingleNode(scraperData.SiteConfig.Selectors.TableOfContents
                                    .PremiumChapterSelectors.PremiumCost!)?.InnerText
                                : "0";
                            var chapterUrl = chapterLinkNode.Attributes["href"].Value;
                            var chapterTitle =
                                !string.IsNullOrEmpty(
                                    scraperData.SiteConfig.Selectors.TableOfContents.ChapterTitleInToc)
                                    ? HtmlEntity.DeEntitize(chapterLinkNode
                                        .SelectSingleNode(scraperData.SiteConfig.Selectors.TableOfContents
                                            .ChapterTitleInToc!)?.InnerText?.Trim())
                                    : HtmlEntity.DeEntitize(chapterLinkNode.InnerText?.Trim());
                            chapterUrl = chapterUrl != null && !IsValidHttpUrl(chapterUrl) &&
                                         scraperData.BaseUri != null
                                ? new Uri(scraperData.BaseUri, chapterUrl).ToString()
                                : chapterUrl;
                            var chapterLink = new ChapterLink()
                            {
                                Url = chapterUrl!,
                                Title = chapterTitle,
                                PremiumInfo = isPremium
                                    ? new PremiumChapterInfo()
                                    {
                                        IsPremium = isPremium,
                                        Cost = int.TryParse(premiumCost, out var parsedPremiumCost) ? parsedPremiumCost : 0,
                                        CurrencyName = scraperData.SiteConfig.PremiumInfo?.CurrencyName ?? "Credits"
                                    }
                                    : new PremiumChapterInfo()
                            };
                            novelDataBuffer.ChapterLinks.Add(chapterLink);
                        }

                        Console.WriteLine($"Got chapter urls, total: {novelDataBuffer.ChapterLinks.Count}");
                        break;

                    case Attr.FirstChapterUrl:
                        // TODO: Implement
                        break;

                    case Attr.CurrentChapter:
                        var latestChapterNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.TableOfContents.LatestChapterLink);

                        if (latestChapterNode == null)
                        {
                            return;
                        }

                        // Extract chapter URL if href attribute exists
                        if (latestChapterNode.Attributes["href"] != null)
                        {
                            var currentChapterUrl = latestChapterNode.Attributes["href"].Value;

                            if (!IsValidHttpUrl(currentChapterUrl))
                            {
                                currentChapterUrl = new Uri(scraperData.BaseUri, currentChapterUrl).ToString();
                            }

                            novelDataBuffer.CurrentChapterUrl = currentChapterUrl;
                        }

                        novelDataBuffer.MostRecentChapterTitle =
                            HtmlEntity.DeEntitize(latestChapterNode.InnerText).Trim();
                        Console.WriteLine($"Latest Chapter: {novelDataBuffer.MostRecentChapterTitle}");
                        break;
                    default:
                        Console.WriteLine($"Case: {attr} is not implemented");
                        break;
                }
            }

            /// <summary>
            /// Splits a string into a list of strings, each containing only characters from either the Asian or Latin alphabet.
            /// </summary>
            /// <param name="input"></param>
            /// <returns></returns>
            public static IList<string> SplitByLanguage(string input)
            {
                List<string> result = new List<string>();
                StringBuilder currentString = new StringBuilder();
                bool? isLastCharAsian = null;

                foreach (char c in input)
                {
                    // http://www.rikai.com/library/kanjitables/kanji_codes.unicode.shtml
                    bool isCurrentCharAsian =
                        (c >= 0x3000 && c <= 0x9FFF) || (c >= 0x4E00 && c <= 0x9FFF); // range of Asian characters

                    if (isLastCharAsian.HasValue && isCurrentCharAsian != isLastCharAsian)
                    {
                        result.Add(currentString.ToString().Trim());
                        currentString.Clear();
                    }

                    currentString.Append(c);
                    isLastCharAsian = isCurrentCharAsian;
                }

                result.Add(currentString.ToString().Trim());

                return result;
            }

            public static bool IsValidHttpUrl(string url)
            {
                return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                       (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            }
        }
    }

    public sealed class ScraperData
    {
        public SiteConfiguration SiteConfig { get; set; } = null!;

        public Uri SiteTableOfContents { get; set; } = null!;

        public Uri BaseUri { get; set; } = null!;

        public IHttpClientFactory? HttpClientFactory { get; set; }

        public SelectedChapterRange? ChapterRange { get; set; }

        public string? DetectedVolumeName { get; set; }

        public IList<OpenQA.Selenium.Cookie>? LoginCookies { get; init; }

        public bool IsSessionAuthenticated { get; set; }
    }

    public abstract class ScraperStrategy
    {
        protected readonly ScraperData ScraperData = new ScraperData();

        protected const int TotalPossiblePaginationTabs = 6;

        protected static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        protected static readonly NovelScraperSettings Settings = new NovelScraperSettings();

        private const int DefaultMinimumParagraphThreshold = 5;

        private volatile int _globalBackoffMs;

        private const int MaxGlobalBackoffMs = 30_000;

        // FlareSolverr integration for Cloudflare bypass
        private FlareSolverrService? _flareSolverr;
        private bool _flareSolverrEnabled;
        private string? _flareSolverrUserAgent;

        private readonly IHttpClientFactory _httpClientFactory;

        private readonly IDriverFactory _driverFactory;

        private SemaphoreSlim
            _semaphoreSlim; // limit the number of concurrent requests, prevent posssible rate limiting

        private static readonly Random _random = new Random(); // For request randomization to avoid detection patterns

        private static readonly List<string> _userAgents = new List<string>
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64; rv:133.0) Gecko/20100101 Firefox/133.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0"
        };

        private static int _userAgentIndex = -1;

        protected ScraperStrategy(IHttpClientFactory? httpClientFactory = null, IDriverFactory? driverFactory = null)
        {
            _httpClientFactory = httpClientFactory ?? new HttpClientFactory();
            _driverFactory = driverFactory ?? new DriverFactory();
            ScraperData.HttpClientFactory = _httpClientFactory;
            _semaphoreSlim = new SemaphoreSlim(ConcurrentRequestsLimit);
        }

        protected enum SeleniumPageStepType
        {
            Click,
            WaitForPresence,
            WaitForClickable,
            Custom
        }

        /// <summary>
        /// Gets a value indicating whether FlareSolverr is currently enabled.
        /// </summary>
        public bool IsFlareSolverrEnabled => _flareSolverrEnabled && _flareSolverr != null;

        protected bool RequiresLogin { get; private set; }

        private int ConcurrentRequestsLimit { get; set; } = 2;

        /// <summary>
        /// Enable FlareSolverr for Cloudflare bypass.
        /// FlareSolverr must be running at the specified URL (default: http://localhost:8191).
        ///
        /// To run FlareSolverr with Docker:
        /// docker run -d --name=flaresolverr -p 8191:8191 -e LOG_LEVEL=info ghcr.io/flaresolverr/flaresolverr:latest
        /// </summary>
        /// <param name="flareSolverrUrl">FlareSolverr URL (default: http://localhost:8191)</param>
        /// <returns>True if FlareSolverr is available and enabled</returns>
        public async Task<bool> EnableFlareSolverrAsync(string flareSolverrUrl = "http://localhost:8191")
        {
            _flareSolverr = new FlareSolverrService(flareSolverrUrl);
            _flareSolverrEnabled = await _flareSolverr.CheckHealthAsync();

            if (_flareSolverrEnabled)
            {
                Logger.Info($"FlareSolverr enabled at {flareSolverrUrl}");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ FlareSolverr enabled - Cloudflare challenges will be solved automatically");
                Console.ResetColor();
            }
            else
            {
                Logger.Warn($"FlareSolverr not available at {flareSolverrUrl}");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠ FlareSolverr not available at {flareSolverrUrl}");
                Console.WriteLine("  Cloudflare-protected sites may fail to load.");
                Console.WriteLine(
                    "  To run FlareSolverr: docker run -d -p 8191:8191 ghcr.io/flaresolverr/flaresolverr:latest");
                Console.ResetColor();
            }

            return _flareSolverrEnabled;
        }

        /// <summary>
        /// Disable FlareSolverr and dispose the service.
        /// </summary>
        public void DisableFlareSolverr()
        {
            _flareSolverrEnabled = false;
            _flareSolverr?.Dispose();
            _flareSolverr = null;
            _flareSolverrUserAgent = null;
            Logger.Info("FlareSolverr disabled");
        }

        public abstract Task<NovelDataBuffer> ScrapeAsync();

        public void SetVariables(SiteConfiguration siteConfig, Uri siteTableOfContents, Configuration configuration)
        {
            SetSiteConfiguration(siteConfig);
            SetSiteTableOfContents(siteTableOfContents);
            SetConcurrentRequestLimit(configuration.ConcurrencyLimit);
            SetSemaphoreLimit(ConcurrentRequestsLimit);
        }

        public SiteConfiguration GetSiteConfiguration()
        {
            return ScraperData.SiteConfig ?? throw new NullReferenceException("SiteConfiguration is null");
        }

        public void SetChapterRange(SelectedChapterRange? chapterRange, string? volumeName = null)
        {
            ScraperData.ChapterRange = chapterRange;
            ScraperData.DetectedVolumeName = volumeName;
            if (chapterRange != null)
            {
                Logger.Info($"Chapter range set: {chapterRange}{(volumeName != null ? $" ({volumeName})" : "")}");
            }
        }

        public void SetLoginPreference(bool withLogin)
        {
            RequiresLogin = withLogin;

            // Show informative message for sites with premium chapters
            if (!withLogin || ScraperData.SiteConfig?.HasPremiumChapters != true)
            {
                return;
            }

            var loginMessages = new[] { $"Login Enabled for {ScraperData.SiteConfig.Name}" };
            CommonHelper.DrawBox(loginMessages, ConsoleColor.Cyan);
            Console.WriteLine("A browser window will open for manual login.\n");
            Console.WriteLine("Benefits:");
            Console.WriteLine($"  • Access premium chapters you own");
            Console.WriteLine($"  • Premium content included in your download\n");
            Console.WriteLine("Privacy:");
            Console.WriteLine($"  • Your credentials are NEVER stored");
            Console.WriteLine($"  • Login session ends after scraping completes\n");

            Logger.Info($"Login enabled for {ScraperData.SiteConfig.Name}");
        }

        public void SetSessionAuthenticated(bool isAuthenticated)
        {
            ScraperData.IsSessionAuthenticated = isAuthenticated;
        }

        public SelectedChapterRange? GetChapterRange()
        {
            return ScraperData.ChapterRange;
        }

        public string? GetDetectedVolumeName()
        {
            return ScraperData.DetectedVolumeName;
        }

        /// <summary>
        /// Filters a list of chapter URLs based on the current ChapterRange.
        /// Returns the filtered list and optionally chapter titles if provided.
        /// </summary>
        public IReadOnlyList<string> FilterChaptersByRange(IReadOnlyList<string> chapterUrls, out IReadOnlyList<string>? filteredTitles,
            IReadOnlyList<string>? chapterTitles = null)
        {
            filteredTitles = null;

            if (ScraperData.ChapterRange == null)
            {
                Logger.Debug("No chapter range set, returning all chapter URLs");
                filteredTitles = chapterTitles;
                return chapterUrls;
            }

            var range = ScraperData.ChapterRange;

            if (!range.IsValid())
            {
                Logger.Error($"Invalid chapter range: {range}");
                filteredTitles = chapterTitles;
                return chapterUrls;
            }

            if (range.Begin > chapterUrls.Count || range.End > chapterUrls.Count)
            {
                Logger.Error($"Chapter range {range} exceeds available chapters ({chapterUrls.Count})");
                filteredTitles = chapterTitles;
                return chapterUrls;
            }

            Logger.Info($"Filtering chapters to range: {range}");

            var filteredUrls = chapterUrls
                .Skip(range.Begin - 1)
                .Take(range.Count)
                .ToList();

            if (chapterTitles != null && chapterTitles.Count >= range.End)
            {
                filteredTitles = chapterTitles
                    .Skip(range.Begin - 1)
                    .Take(range.Count)
                    .ToList();
            }

            Logger.Info($"Filtered {chapterUrls.Count} chapters down to {filteredUrls.Count} chapters");

            return filteredUrls;
        }

        /// <summary>
        /// Extracts both chapter URLs and titles from an already-loaded HTML document.
        /// This consolidates extraction to avoid redundant HTTP requests.
        /// Populates the NovelDataBuffer with both ChapterUrls and ChapterTitles.
        /// </summary>
        public virtual void ExtractChapterUrlsAndTitles(HtmlDocument htmlDocument, NovelDataBuffer novelDataBuffer,
            ScraperData scraperData)
        {
            try
            {
                Logger.Info("Extracting chapter URLs and titles from table of contents");

                var chapterLinkNodes =
                    htmlDocument.DocumentNode.SelectNodes(
                        scraperData.SiteConfig?.Selectors.TableOfContents.ChapterLinks);

                if (chapterLinkNodes == null || !chapterLinkNodes.Any())
                {
                    Logger.Warn("No chapter link nodes found");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        $"Failed to get chapter urls for novel {novelDataBuffer.Title} at url {scraperData.BaseUri}");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine(
                        $"Please check the appsettings.json for {scraperData.SiteConfig.Name} the chapterLinks key");
                    Console.ResetColor();
                    return;
                }

                var chapterLinks = new List<ChapterLink>();
                var chapterTitles = new List<string>();

                for (int i = 0; i < chapterLinkNodes.Count; i++)
                {
                    var chapterLinkNode = chapterLinkNodes[i];

                    var href = chapterLinkNode.Attributes["href"]?.Value;
                    if (string.IsNullOrEmpty(href))
                    {
                        continue;
                    }

                    var url = IsValidHttpUrl(href)
                        ? href
                        : scraperData.BaseUri != null
                            ? new Uri(scraperData.BaseUri, href).ToString()
                            : href;

                    var title = HtmlEntity.DeEntitize(chapterLinkNode.InnerText?.Trim());
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = $"Chapter {chapterLinks.Count + 1}";
                    }

                    chapterTitles.Add(title);
                    chapterLinks.Add(new ChapterLink { Url = url, Title = title });
                }

                novelDataBuffer.ChapterLinks.ReplaceWith(chapterLinks);
                novelDataBuffer.ChapterTitles.ReplaceWith(chapterTitles);

                Console.WriteLine($"Extracted {chapterLinks.Count} chapter URLs and {chapterTitles.Count} titles");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting chapter URLs and titles: {ex.Message}");
            }
        }

        /// <summary>
        /// Sorts chapters based on the site configuration's ChapterSortOrder setting.
        /// Applies to both ChapterUrls and ChapterTitles in the NovelDataBuffer.
        /// Also assigns proper ChapterNumber after sorting.
        /// </summary>
        public virtual void SortChapters(NovelDataBuffer novelDataBuffer)
        {
            if (ScraperData.SiteConfig?.ChapterSortOrder == null)
            {
                Logger.Debug("No chapter sort order configured, keeping default order");
                AssignChapterNumbers(novelDataBuffer);

                return;
            }

            var sortOrder = ScraperData.SiteConfig.ChapterSortOrder;

            if (sortOrder == ChapterSortOrder.None)
            {
                Logger.Debug("Chapter sort order set to None, keeping original order");
                AssignChapterNumbers(novelDataBuffer);
                return;
            }

            if (sortOrder == ChapterSortOrder.Descending)
            {
                Logger.Debug("Reversing chapter order (Descending -> Ascending)");
                novelDataBuffer.ChapterLinks.ReverseInPlace();
                novelDataBuffer.ChapterTitles.ReverseInPlace();
            }
            else
            {
                Logger.Debug("Chapter sort order is Ascending (default), no reversal needed");
            }

            // Assign chapter numbers based on final sorted order
            AssignChapterNumbers(novelDataBuffer);
        }

        public async Task<(HtmlDocument document, Uri updatedUri)> LoadHtmlPublicAsync(Uri uri)
        {
            return await LoadHtmlAsync(uri);
        }

        /// <summary>
        /// Inject cookies from the browser to bypass Cloudflare protection.
        /// You can copy cookies from your browser's DevTools (F12 -> Application -> Cookies).
        /// Example: "cf_clearance=abc123; session=xyz789"
        /// </summary>
        /// <param name="uri">The URI for which to set cookies (usually the base site URL)</param>
        /// <param name="cookieHeader">Cookie string from browser (format: "name1=value1; name2=value2")</param>
        public void InjectCookiesFromBrowser(Uri uri, string cookieHeader)
        {
            _httpClientFactory.AddCookiesFromHeader(uri, cookieHeader);
            Logger.Info($"Injected {cookieHeader.Split(';').Length} cookie(s) for {uri.Host}");
        }

        /// <summary>
        /// Add a single cookie for a specific URI.
        /// </summary>
        public void AddCookie(Uri uri, Cookie cookie)
        {
            _httpClientFactory.AddCookie(uri, cookie);
            Logger.Info($"Added cookie '{cookie.Name}' for {uri.Host}");
        }

        public bool IsValidHttpUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Gets the non-paginated chapter contents for each chapter url provided. This method uses either HttpClient or
        /// Selenium based on site requirements.
        /// </summary>
        /// <param name="chapterLinks"></param>
        /// <returns>Returns the list of Chapter Data Buffer which contains the contents</returns>
        public async Task<List<ChapterDataBuffer>> GetChaptersDataAsync(IList<ChapterLink> chapterLinks)
        {
            var tempImageDirectory = string.Empty;
            try
            {
                Logger.Info("Getting chapters data");
                var chapterDataBuffers = new List<ChapterDataBuffer>();

                // Use Selenium for sites with images or if chapter content requires Selenium. Not thread safe, so don't use tasks.
                if (ShouldUseSeleniumForChapters())
                {
                    if (ShouldUseSpaNavigation())
                    {
                        Logger.Info("Using SPA navigation for chapter processing (auth session active, next-chapter button configured)");

                        // Reuse existing driver if one was left alive from TOC scraping
                        IWebDriver driver;
                        if (_driverFactory.GetAllDrivers().Any())
                        {
                            Logger.Debug("Reusing existing Selenium driver for SPA navigation");
                            driver = _driverFactory.GetAllDrivers().Values.First();
                        }
                        else
                        {
                            driver = await _driverFactory.CreateDriverAsync(chapterLinks.First().Url, isHeadless: false);
                        }

                        if (ScraperData.SiteConfig!.HasImagesForChapterContent)
                        {
                            tempImageDirectory = CommonHelper.CreateTempDirectory();
                        }

                        try
                        {
                            await ProcessChaptersWithSpaNavigation(driver, chapterLinks, chapterDataBuffers, tempImageDirectory);
                        }
                        finally
                        {
                            _driverFactory.DisposeAllDrivers();
                        }
                    }
                    else
                    {
                        await ProcessChaptersWithSelenium(chapterLinks, chapterDataBuffers, tempImageDirectory);
                    }
                }
                else
                {
                    var tasks = new List<Task<ChapterDataBuffer>>();
                    // I haven't run into a httpclient site that requires premium so no need to pass the entire chapterLink yet.
                    await ProcessChaptersWithHttpClient(chapterLinks.Select(c => c.Url).ToList(), tasks, chapterDataBuffers);
                }

                for (var i = 0; i < chapterDataBuffers.Count && i < chapterLinks.Count; i++)
                {
                    chapterDataBuffers[i].SequenceNumber = chapterLinks[i].ChapterNumber;
                }

                return chapterDataBuffers;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting chapters data. {ex}");
                if (string.IsNullOrEmpty(tempImageDirectory))
                {
                    throw;
                }

                Directory.Delete(tempImageDirectory, true);
                Logger.Info("Finished deleting temp directory");
                throw;
            }
        }

        public void SetSemaphoreLimit(int concurrentRequestLimit)
        {
            _semaphoreSlim = new SemaphoreSlim(concurrentRequestLimit);
        }

        /// <summary>
        /// This method is what is used to get the novel data from the table of contents page. i.e. Description, Chapters, Title, Novel Status.
        /// </summary>
        /// <param name="htmlDocument"></param>
        /// <returns></returns>
        protected abstract NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument);

        /// <summary>
        /// This method is what is used to get the novel data from the table of contents page. i.e. Description, Chapters, Title, Novel Status.
        /// </summary>
        /// <param name="htmlDocument"></param>
        /// <returns></returns>
        protected virtual Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
        {
            // this method should always be overridden, I just needed a default implementation so other Strategies would not require it
            return Task.Run(() => FetchNovelDataFromTableOfContents(htmlDocument));
        }

        protected async Task<(HtmlDocument document, Uri updatedUri)> LoadHtmlAsync(Uri uri)
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            using var client = _httpClientFactory.CreateClient();

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            // Use FlareSolverr's user-agent if we got one from solving a challenge
            string userAgent = _flareSolverrUserAgent ??
                               _userAgents[
                                   System.Threading.Interlocked.Increment(ref _userAgentIndex) % _userAgents.Count];

            if (ScraperData.BaseUri == new Uri("https://www.lightnovelworld.com/"))
            {
                userAgent = _userAgents[0];
            }

            // Add realistic browser headers to bypass Cloudflare
            requestMessage.Headers.Add("User-Agent", userAgent);
            requestMessage.Headers.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            requestMessage.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            requestMessage.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            requestMessage.Headers.Add("DNT", "1");
            requestMessage.Headers.Add("Connection", "keep-alive");
            requestMessage.Headers.Add("Upgrade-Insecure-Requests", "1");
            requestMessage.Headers.Add("Sec-Fetch-Dest", "document");
            requestMessage.Headers.Add("Sec-Fetch-Mode", "navigate");
            requestMessage.Headers.Add("Sec-Fetch-Site", "none");
            requestMessage.Headers.Add("Sec-Fetch-User", "?1");
            requestMessage.Headers.Add("Cache-Control", "max-age=0");

            if (ScraperData.BaseUri != null)
            {
                requestMessage.Headers.Add("Referer", ScraperData.BaseUri.ToString());
            }

            requestMessage.Options.Set(new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"),
                TimeSpan.FromSeconds(10));

            // Add random delay between 100-500ms to avoid predictable request patterns
            // This helps bypass Cloudflare's bot detection which looks for mechanical timing
            int baseDelay = _random.Next(100, 500);
            int totalDelay = baseDelay + _globalBackoffMs;
            await Task.Delay(totalDelay);

            Logger.Debug($"Sending request to {uri}");

            using var response = await client.SendAsync(requestMessage);

            Logger.Debug($"Response Status: {(int)response.StatusCode} {response.StatusCode}");
            Logger.Debug(
                $"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"))}");

            // Guard: Handle unsuccessful response
            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"Request failed to {uri}");
                Logger.Error($"Status Code: {(int)response.StatusCode} {response.StatusCode}");
                Logger.Error(
                    $"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"))}");

                string? errorContent = null;
                try
                {
                    errorContent = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrEmpty(errorContent))
                    {
                    }
                    else if (errorContent.Length < 1000)
                    {
                        Logger.Error($"Response Body: {errorContent}");
                    }
                    else
                    {
                        Logger.Error($"Response Body (truncated): {errorContent.Substring(0, 1000)}...");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Could not read error response body: {ex.Message}");
                }

                if (_flareSolverrEnabled && _flareSolverr != null &&
                    FlareSolverrService.IsCloudflareChallenge(response.StatusCode, errorContent))
                {
                    Logger.Info($"Cloudflare challenge detected for {uri}. Using FlareSolverr to solve...");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"🔓 Cloudflare challenge detected - solving with FlareSolverr...");
                    Console.ResetColor();

                    var flareSolverrResult = await _flareSolverr.SolveAsync(uri.ToString());
                    if (flareSolverrResult?.Status == "ok" && flareSolverrResult.Solution != null)
                    {
                        var cookieHeader = FlareSolverrService.GetCookieHeader(flareSolverrResult);
                        if (!string.IsNullOrEmpty(cookieHeader))
                        {
                            _httpClientFactory.AddCookiesFromHeader(uri, cookieHeader);
                            Logger.Info(
                                $"Injected {flareSolverrResult.Solution.Cookies?.Count ?? 0} cookies from FlareSolverr");
                        }

                        _flareSolverrUserAgent = flareSolverrResult.Solution.UserAgent;

                        var solvedHtmlDocument = new HtmlDocument();
                        solvedHtmlDocument.LoadHtml(flareSolverrResult.Solution.Response);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Cloudflare challenge solved successfully");
                        Console.ResetColor();

                        var solvedCanonicalNode =
                            solvedHtmlDocument.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
                        if (solvedCanonicalNode != null)
                        {
                            var solvedCanonicalUrl = solvedCanonicalNode.Attributes["href"]?.Value;
                            if (!string.IsNullOrEmpty(solvedCanonicalUrl) && solvedCanonicalUrl != uri.ToString())
                            {
                                Logger.Debug(
                                    $"Canonical URL detected. Old URL: {uri}, Canonical URL: {solvedCanonicalUrl}");
                                uri = new Uri(solvedCanonicalUrl);
                            }
                        }

                        return (solvedHtmlDocument, uri);
                    }

                    Logger.Error(
                        $"FlareSolverr failed to solve challenge: {flareSolverrResult?.Message ?? "Unknown error"}");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ FlareSolverr failed to solve challenge");
                    Console.ResetColor();
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var currentBackoff = _globalBackoffMs;
                    var newBackoff = Math.Min(currentBackoff == 0 ? 3000 : currentBackoff * 2, MaxGlobalBackoffMs);
                    _globalBackoffMs = newBackoff;
                    Logger.Warn($"429 detected for {uri} — global backoff increased to {newBackoff}ms");
                }

                throw new HttpRequestException(
                    $"Failed to load HTML document from {uri}. Status code: {response.StatusCode}");
            }

            if (_globalBackoffMs > 0)
            {
                _globalBackoffMs = Math.Max(0, _globalBackoffMs / 2);
            }

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);

            var canonicalNode = htmlDocument.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
            if (canonicalNode != null)
            {
                var canonicalUrl = canonicalNode.Attributes["href"]?.Value;
                if (!string.IsNullOrEmpty(canonicalUrl) && canonicalUrl != uri.ToString())
                {
                    Logger.Debug($"Canonical URL detected. Old URL: {uri}, Canonical URL: {canonicalUrl}");
                    uri = new Uri(canonicalUrl);
                }
            }

            return (htmlDocument, uri);
        }

        protected async Task<string> DownloadImageAsync(Uri uri, string tempImageDirectory)
        {
            var uriString = uri.ToString();
            uriString = uriString.Replace("amp;", "");
            uri = new Uri(uriString);
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            try
            {
                using var client = _httpClientFactory.CreateClient();

                var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                var userAgent =
                    _userAgents[System.Threading.Interlocked.Increment(ref _userAgentIndex) % _userAgents.Count];

                // Add realistic browser headers for image downloads
                requestMessage.Headers.Add("User-Agent", userAgent);
                requestMessage.Headers.Add("Accept",
                    "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                requestMessage.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                requestMessage.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                requestMessage.Headers.Add("DNT", "1");
                requestMessage.Headers.Add("Connection", "keep-alive");
                requestMessage.Headers.Add("Sec-Fetch-Dest", "image");
                requestMessage.Headers.Add("Sec-Fetch-Mode", "no-cors");
                requestMessage.Headers.Add("Sec-Fetch-Site", "cross-site");

                if (ScraperData.BaseUri != null)
                {
                    requestMessage.Headers.Add("Referer", ScraperData.BaseUri.ToString());
                }

                using var response =
                    await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                await using var imageStream = await response.Content.ReadAsStreamAsync();
                var imagePath = Path.Combine(tempImageDirectory, Path.GetRandomFileName() + ".jpg");
                await using (var fileStream = File.Create(imagePath))
                {
                    await imageStream.CopyToAsync(fileStream);
                    Logger.Debug($"Downloaded image to temp folder {imagePath}");
                }

                return imagePath;
            }
            catch (HttpRequestException e)
            {
                Logger.Error($"Error occurred while navigating to {uri}. Error: {e}");
            }

            throw new HttpRequestException($"Failed to download image from {uri}.");
        }

        protected virtual Uri TrimLastUriSegment(Uri siteUri)
        {
            string allSegementsButLast = siteUri.Segments.Take(siteUri.Segments.Length - 1)
                .Aggregate((segment1, segment2) => segment1 + segment2);
            return new Uri(ScraperData.BaseUri, allSegementsButLast);
        }

        protected void SetBaseUri(Uri siteUri)
        {
            if (siteUri == null)
            {
                Logger.Error($"siteUri, which is the url that was provided by the user is null.");
                throw new ArgumentNullException(nameof(siteUri));
            }

            ScraperData.BaseUri = new Uri(siteUri.GetLeftPart(UriPartial.Authority));
        }

        protected static int GetPageNumberFromUrlQuery(string url, Uri baseUri)
        {
            Uri uriResult;
            bool result = Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uriResult);

            // if the url is relative, then combine it with the base uri
            if (!uriResult.IsAbsoluteUri)
            {
                uriResult = new Uri(baseUri, uriResult);
            }

            if (result && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                var pageNumberFromQuery = HttpUtility.ParseQueryString(uriResult.Query);
                if (pageNumberFromQuery.AllKeys.Contains("page"))
                {
                    var pageNumber = pageNumberFromQuery["page"];
                    if (int.TryParse(pageNumber, out int page))
                    {
                        return page;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Gets the chapter urls from the table of contents page that requires pagination to get chapters.
        /// </summary>
        /// <param name="tableOfContentUri"></param>
        /// <param name="getAllChapters"></param>
        /// <param name="pageToStopAt"></param>
        /// <param name="pageToStartAt"></param>
        /// <returns>Returns a ValueTuple that contains all the chapter urls and the url for the last page of the table of contents</returns>
        /// <summary>
        /// Fetches chapter links across multiple paginated table of contents pages.
        /// Returns ChapterLink list and the last table of contents URL.
        /// </summary>
        protected virtual async Task<(List<ChapterLink> ChapterLinks, string LastTableOfContentsUrl)>
            GetPaginatedChapterLinksAsync(Uri tableOfContentUri, bool getAllChapters, int pageToStopAt,
                int pageToStartAt = 1)
        {
            var chapterLinks = new List<ChapterLink>();
            var tocSelector = ScraperData.SiteConfig?.Selectors.TableOfContents;
            var baseTableOfContentUrl = tableOfContentUri + ScraperData.SiteConfig?.PaginationType;
            var lastTableOfContentsUrl = string.Format(baseTableOfContentUrl, pageToStopAt);

            for (var i = pageToStartAt; i <= pageToStopAt; i++)
            {
                var pageUrl = string.Format(baseTableOfContentUrl, i);
                var isPageNew = i > pageToStartAt;
                try
                {
                    Logger.Info($"Navigating to {pageUrl}");
                    var (htmlDocument, _) = await LoadHtmlAsync(new Uri(pageUrl));

                    var linkNodes = htmlDocument.DocumentNode.SelectNodes(tocSelector?.ChapterLinks);
                    if (linkNodes == null)
                    {
                        continue;
                    }

                    foreach (var node in linkNodes)
                    {
                        var href = node.GetAttributeValue("href", string.Empty);
                        if (string.IsNullOrWhiteSpace(href))
                        {
                            continue;
                        }

                        var url = IsValidHttpUrl(href) ? href : new Uri(ScraperData.BaseUri!, href).ToString();

                        var title = HtmlEntity.DeEntitize(node.InnerText).Trim();
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            title = $"Chapter {chapterLinks.Count + 1}";
                        }

                        var premiumSelectors = tocSelector?.PremiumChapterSelectors;
                        var isPremium = ScraperData.SiteConfig?.HasPremiumChapters == true
                                        && premiumSelectors?.PremiumIndicator != null
                                        && node.SelectSingleNode(premiumSelectors.PremiumIndicator) != null;

                        var costNode = isPremium && premiumSelectors?.PremiumCost != null
                            ? node.SelectSingleNode(premiumSelectors.PremiumCost)
                            : null;
                        var cost = 0;
                        if (costNode != null && int.TryParse(costNode.InnerText.Trim(), out var parsedCost))
                        {
                            cost = parsedCost;
                        }

                        chapterLinks.Add(new ChapterLink
                        {
                            Url = url,
                            Title = title,
                            PremiumInfo = new PremiumChapterInfo
                            {
                                IsPremium = isPremium,
                                Cost = cost,
                                CurrencyName = ScraperData.SiteConfig?.PremiumInfo?.CurrencyName
                            }
                        });
                    }

                    if (!getAllChapters && !isPageNew)
                    {
                        break;
                    }
                }
                catch (HttpRequestException e)
                {
                    Logger.Error($"Error occurred while navigating to {pageUrl}. Error: {e}");
                }
            }

            return (chapterLinks, lastTableOfContentsUrl);
        }

        protected async Task<(HtmlDocument? Document, string? PageSource)> GetHtmlDocumentUsingSeleniumAsync(
            string url,
            string requiredXPath,
            IEnumerable<SeleniumPageStep>? steps = null,
            string objectToLookFor = "Content",
            bool isAllowedToFail = true,
            int timeoutSeconds = 60,
            bool isHeadless = false,
            Func<IWebDriver, WebDriverWait, Task>? preWaitAction = null,
            bool reuseExistingDriver = false)
        {
            IWebDriver? driver = null;

            try
            {
                if (reuseExistingDriver && _driverFactory.GetAllDrivers().Any())
                {
                    driver = _driverFactory.GetAllDrivers().Values.First();
                    Logger.Debug("Reusing existing Selenium driver");
                    await driver.Navigate().GoToUrlAsync(url);
                }
                else
                {
                    driver = await _driverFactory.CreateDriverAsync(url, isHeadless: isHeadless);
                }

                var stopwatch = Stopwatch.StartNew();
                var uriLastSegment = new Uri(url).Segments.LastOrDefault() ?? url;

                try
                {
                    Logger.Debug($"Waiting for {objectToLookFor} on page {url} to load.");

                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));

                    if (preWaitAction != null)
                    {
                        Logger.Debug("[Selenium] Running pre-wait action");
                        await preWaitAction(driver, wait);
                    }

                    if (steps != null)
                    {
                        foreach (var step in steps)
                        {
                            var stepTimeout = TimeSpan.FromSeconds(step.TimeoutSeconds ?? timeoutSeconds);
                            var stepWait = new WebDriverWait(driver, stepTimeout);

                            switch (step.Type)
                            {
                                case SeleniumPageStepType.Click:
                                    {
                                        var msg = "[Selenium] Click: " + (step.Description ?? step.XPath);
                                        Logger.Debug(msg);
                                        stepWait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(step.XPath)))
                                            .Click();
                                        break;
                                    }

                                case SeleniumPageStepType.WaitForPresence:
                                    {
                                        var msg = "[Selenium] WaitForPresence: " + (step.Description ?? step.XPath);
                                        Logger.Debug(msg);
                                        stepWait.Until(
                                            ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(step.XPath)));
                                        break;
                                    }

                                case SeleniumPageStepType.WaitForClickable:
                                    {
                                        var msg = "[Selenium] WaitForClickable: " + (step.Description ?? step.XPath);
                                        Logger.Debug(msg);
                                        stepWait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(step.XPath)));
                                        break;
                                    }

                                case SeleniumPageStepType.Custom:
                                    {
                                        if (step.CustomAction == null)
                                        {
                                            throw new InvalidOperationException("Custom step requires CustomAction");
                                        }

                                        var msg = "[Selenium] Custom: " + (step.Description ?? "(custom action)");
                                        Logger.Debug(msg);
                                        await step.CustomAction(driver, stepWait);
                                        break;
                                    }

                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        }
                    }

                    wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(requiredXPath)));
                    Logger.Debug($"\n{objectToLookFor} loaded for {url}. Time: {stopwatch.ElapsedMilliseconds} ms");

                    var htmlDocument = new HtmlDocument();
                    var pageSource = driver.PageSource;
                    htmlDocument.LoadHtml(pageSource);
                    return (htmlDocument, pageSource);
                }
                catch (WebDriverTimeoutException ex)
                {
                    var message =
                        $"Unable to get {objectToLookFor} from {uriLastSegment} after waiting for {timeoutSeconds} seconds.\n" +
                        $"Required XPath: {requiredXPath}\n" +
                        $"URL: {url}";

                    if (isAllowedToFail)
                    {
                        Logger.Error($"{message}\nException: {ex}");
                        return (null, null);
                    }

                    Logger.Fatal($"{message}\nException: {ex}");
                    throw;
                }
            }
            finally
            {
                // Only dispose the driver if we explicitly don't want to reuse it.
                // When reuseExistingDriver=true, keep the driver alive for subsequent calls
                // (e.g., table of contents needs driver and so does chapter content scraping).
                if (!reuseExistingDriver && driver != null)
                {
                    try
                    {
                        driver.Quit();
                        driver.Dispose();
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        // Backwards-compatible wrapper (keeps existing callers stable)
        protected async Task<HtmlDocument?> GetHtmlDocumentUsingSeleniumAsync(
            string url,
            string xpath,
            string novelTitle,
            string objectToLookFor = "Chapter Urls",
            bool isAllowedToFail = true,
            int timeoutSeconds = 60)
        {
            var (htmlDocument, _) = await GetHtmlDocumentUsingSeleniumAsync(
                url: url,
                requiredXPath: xpath,
                steps: null,
                objectToLookFor: objectToLookFor,
                isAllowedToFail: isAllowedToFail,
                timeoutSeconds: timeoutSeconds,
                isHeadless: true,
                reuseExistingDriver: true);

            return htmlDocument;
        }

        /// <summary>
        /// Decodes sites that use HTML encoded characters like class="&#x70;&#x61;&#x67;&#x69;&#x6E;&#x61;&#x74;&#x69;
        /// </summary>
        /// <param name="htmlDocument"></param>
        /// <returns>Decoded HtmlDocument</returns>
        protected static HtmlDocument DecodeHtml(HtmlDocument htmlDocument)
        {
            Logger.Debug("Decoding HTML - Start");
            var decodedHtml = WebUtility.HtmlDecode(htmlDocument.DocumentNode.OuterHtml);
            if (string.IsNullOrEmpty(decodedHtml))
            {
                Logger.Error("Decoded HTML was null");
                return htmlDocument;
            }

            Logger.Debug("Decoding HTML - End");
            var decodedHtmlDocument = new HtmlDocument();
            decodedHtmlDocument.LoadHtml(decodedHtml);
            Logger.Debug("Decoded HTML loaded into HtmlDocument");

            return decodedHtmlDocument;
        }

        /// <summary>
        /// Determines whether Selenium should be used for scraping chapter content.
        /// Returns true if the site has images for chapter content or if chapter content requires Selenium.
        /// </summary>
        protected virtual bool ShouldUseSeleniumForChapters()
        {
            return ScraperData.SiteConfig.HasImagesForChapterContent
                   || ScraperData.SiteConfig.ChapterContentRequiresSelenium;
        }

        protected virtual string NormalizeChapterTitle(string? rawTitle)
        {
            return rawTitle?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Extracts both chapter URLs and titles from an HTML document in a specified range.
        /// This is typically used for paginated sites where chapters span multiple pages.
        /// </summary>
        protected virtual (List<string> Urls, List<string> Titles) GetChapterUrlsAndTitlesInRange(
            HtmlDocument htmlDocument,
            Uri? baseSiteUri,
            int? startChapter = null,
            int? endChapter = null)
        {
            Logger.Info("Getting chapter URLs and titles from table of contents");

            if (baseSiteUri == null)
            {
                Logger.Error("Base site URI is null; cannot build absolute chapter URLs.");
                return (new List<string>(), new List<string>());
            }

            try
            {
                var chapterLinks =
                    htmlDocument.DocumentNode.SelectNodes(
                        ScraperData.SiteConfig?.Selectors.TableOfContents.ChapterLinks);
                if (chapterLinks == null || chapterLinks.Count == 0)
                {
                    Logger.Info("Chapter links Node Collection on table of contents page was null/empty.");
                    return (new List<string>(), new List<string>());
                }

                var chapterUrls = new List<string>();
                var chapterTitles = new List<string>();

                var index = 0;
                foreach (var link in chapterLinks)
                {
                    index++;

                    if (index < startChapter)
                    {
                        continue;
                    }

                    if (index > endChapter)
                    {
                        break;
                    }

                    var href = link.Attributes["href"]?.Value;
                    if (string.IsNullOrWhiteSpace(href))
                    {
                        continue;
                    }

                    var absoluteUrl = IsValidHttpUrl(href)
                        ? href
                        : new Uri(baseSiteUri, href.TrimStart('/')).ToString();

                    chapterUrls.Add(absoluteUrl);

                    var title = HtmlEntity.DeEntitize(link.InnerText ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = $"Chapter {chapterUrls.Count}";
                    }

                    chapterTitles.Add(title);
                }

                return (chapterUrls, chapterTitles);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting chapter urls and titles from table of contents. {ex}");
                return (new List<string>(), new List<string>());
            }
        }

        private static void AssignChapterNumbers(NovelDataBuffer novelDataBuffer)
        {
            for (var i = 0; i < novelDataBuffer.ChapterLinks.Count; i++)
            {
                // Records support 'with' for non-destructive mutation - creates a new instance with modified properties
                // instead of mutating the existing immutable ChapterLink. So we are swapping based on ChapterNumber 
                novelDataBuffer.ChapterLinks[i] = novelDataBuffer.ChapterLinks[i] with { ChapterNumber = i + 1 };
            }
        }

        /// <summary>
        /// Determines whether SPA (client-side) navigation should be used for chapter-to-chapter transitions.
        /// Returns true when the user is authenticated, the site requires login, and a next-chapter button XPath is configured.
        /// SPA navigation avoids full page reloads that destroy auth state in React/SPA sites.
        /// </summary>
        private bool ShouldUseSpaNavigation()
        {
            return RequiresLogin
                && ScraperData.IsSessionAuthenticated
                && !string.IsNullOrEmpty(ScraperData.SiteConfig?.Selectors.NextChapterButton);
        }

        private async Task ProcessChaptersWithSelenium(
            IList<ChapterLink> chapterLinks,
            List<ChapterDataBuffer> chapterDataBuffers,
            string tempImageDirectory)
        {
            Logger.Debug("Using Selenium to get chapters data");

            IWebDriver driver;
            if (_driverFactory.GetAllDrivers().Any())
            {
                Logger.Debug("Reusing existing Selenium driver from table of contents scraping");
                driver = _driverFactory.GetAllDrivers().Values.First();
                driver.Navigate().GoToUrl(chapterLinks.First().Url);
            }
            else
            {
                driver = await _driverFactory.CreateDriverAsync(chapterLinks.First().Url, isHeadless: false);
            }

            if (ScraperData.SiteConfig!.HasImagesForChapterContent)
            {
                tempImageDirectory = CommonHelper.CreateTempDirectory();
            }

            try
            {
                var totalChapters = chapterLinks.Count;
                var currentChapter = 0;
                foreach (var chapterLink in chapterLinks)
                {
                    currentChapter++;
                    chapterDataBuffers.Add(await GetChapterDataAsync(driver, chapterLink, tempImageDirectory,
                        currentChapter, totalChapters));
                }

                Console.Write("\r" + new string(' ', 80) + "\r"); // Clear the progress line
                Logger.Info($"Finished getting chapters data. Total chapters: {chapterDataBuffers.Count}");
                Logger.Debug("Disposing all drivers");
                Console.WriteLine($"Total drivers: {_driverFactory.GetAllDrivers().Count}");
                _driverFactory.DisposeAllDrivers();
                Logger.Debug("Finished disposing all drivers");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting chapters data. {ex}");

                if (Directory.Exists(tempImageDirectory))
                {
                    Directory.Delete(tempImageDirectory, true);
                    Logger.Debug("Finished deleting temp image directory");
                }
                else
                {
                    Logger.Warn($"Was unable to find temp directory {tempImageDirectory}. Please verify it was deleted successfully");
                }

                throw;
            }
            finally
            {
                Logger.Debug("Closing driver");
                _driverFactory.DisposeAllDrivers();
                Logger.Debug("Finished closing driver");
            }
        }

        /// <summary>
        /// Processes chapters using SPA (client-side) navigation instead of full page reloads.
        /// Clicks the "next chapter" button for chapter-to-chapter transitions, preserving React auth state.
        /// After each navigation, waits for the auth verification element (e.g. VIP button) to reappear
        /// before extracting content — this confirms the SPA transition completed with auth intact.
        /// Falls back to full navigation if the next-chapter click fails or lands on the wrong URL.
        /// </summary>
        private async Task ProcessChaptersWithSpaNavigation(
            IWebDriver driver,
            IList<ChapterLink> chapterLinks,
            List<ChapterDataBuffer> chapterDataBuffers,
            string tempImageDirectory)
        {
            var nextChapterXPath = ScraperData.SiteConfig!.Selectors.NextChapterButton!;
            var authElementXPath = ScraperData.SiteConfig.Selectors.UserCurrencyBalances?["buttonToOpenBalances"];
            var totalChapters = chapterLinks.Count;
            var currentChapter = 0;

            foreach (var chapterLink in chapterLinks)
            {
                currentChapter++;
                var skipNavigation = false;

                if (currentChapter == 1)
                {
                    await driver.Navigate().GoToUrlAsync(chapterLink.Url);

                    if (!string.IsNullOrEmpty(authElementXPath))
                    {
                        try
                        {
                            var authWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                            authWait.Until(ExpectedConditions.ElementExists(By.XPath(authElementXPath)));
                            Logger.Debug("Auth verification passed on first chapter after full navigation");
                        }
                        catch (WebDriverTimeoutException)
                        {
                            Logger.Warn($"Auth verification timed out on first chapter {chapterLink.Url}, proceeding anyway");
                        }
                    }

                    skipNavigation = true;
                }
                else
                {
                    // Subsequent chapters: click next-chapter button (SPA navigation)
                    try
                    {
                        var navWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                        var nextChapterbutton = navWait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(nextChapterXPath)));
                        if (nextChapterbutton != null)
                        {
                            IWebElement? previousContentElement = null;
                            try
                            {
                                previousContentElement = driver.FindElement(By.XPath(ScraperData.SiteConfig!.Selectors.ChapterContent!));
                            }
                            catch (NoSuchElementException)
                            {
                                Logger.Debug("Could not find previous content element for staleness check");
                            }

                            nextChapterbutton.Click();

                            if (previousContentElement != null)
                            {
                                var staleWait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                                staleWait.Until(ExpectedConditions.StalenessOf(previousContentElement));
                                Logger.Debug("Previous chapter content element became stale — SPA transition detected");
                            }
                        }

                        if (!string.IsNullOrEmpty(authElementXPath))
                        {
                            Logger.Debug($"\nWaiting for auth verification element to reappear after SPA navigation to chapter {currentChapter}/{totalChapters}");
                            navWait.Until(ExpectedConditions.ElementIsVisible(By.XPath(authElementXPath)));
                        }

                        skipNavigation = true;
                        Logger.Debug($"SPA navigation to {chapterLink.Url} succeeded (chapter {currentChapter}/{totalChapters})");
                    }
                    catch (WebDriverTimeoutException ex)
                    {
                        Logger.Warn($"SPA navigation failed for {chapterLink.Url}, falling back to full navigation");
                        skipNavigation = false;

                        await driver.Navigate().GoToUrlAsync(chapterLink.Url);
                        if (!string.IsNullOrEmpty(authElementXPath))
                        {
                            try
                            {
                                var authWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                                authWait.Until(ExpectedConditions.ElementExists(By.XPath(authElementXPath)));
                                Logger.Debug("Auth verification passed after fallback full navigation");
                            }
                            catch (WebDriverTimeoutException)
                            {
                                Logger.Warn($"Auth verification timed out after fallback for {chapterLink.Url}");
                            }
                        }

                        skipNavigation = true;
                    }
                }

                chapterDataBuffers.Add(await GetChapterDataAsync(driver, chapterLink, tempImageDirectory, currentChapter, totalChapters, skipNavigation));
            }

            Console.Write("\r" + new string(' ', 80) + "\r");
            Logger.Info($"Finished getting chapters data via SPA navigation. Total chapters: {chapterDataBuffers.Count}");
        }

        private async Task ProcessChaptersWithHttpClient(List<string> chapterUrls, List<Task<ChapterDataBuffer>> tasks,
            List<ChapterDataBuffer> chapterDataBuffers)
        {
            Logger.Debug("Using HttpClient to get chapters data");

            foreach (var url in chapterUrls)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await _semaphoreSlim.WaitAsync();
                    try
                    {
                        return await GetChapterDataAsync(url);
                    }
                    finally
                    {
                        _semaphoreSlim.Release();
                    }
                }));
            }

            try
            {
                var taskResults = await Task.WhenAll(tasks);
                chapterDataBuffers.AddRange(taskResults);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting chapters data. {ex}");
                throw;
            }

            Logger.Info("Finished getting chapters data");
        }

        private void SetSiteConfiguration(SiteConfiguration siteConfig)
        {
            ScraperData.SiteConfig = siteConfig;
        }

        private void SetSiteTableOfContents(Uri siteTableOfContents)
        {
            ScraperData.SiteTableOfContents = siteTableOfContents;
        }

        private void SetConcurrentRequestLimit(int concurrentRequestLimit)
        {
            if (concurrentRequestLimit < 1)
            {
                throw new ArgumentException("Concurrent request limit must be greater than 0");
            }

            if (concurrentRequestLimit > Environment.ProcessorCount)
            {
                concurrentRequestLimit = Environment.ProcessorCount;
            }

            this.ConcurrentRequestsLimit = concurrentRequestLimit;
        }

        private async Task<ChapterDataBuffer> GetChapterDataAsync(IWebDriver driver, ChapterLink chapterLink,
            string tempImageDirectory, int currentChapter = 0, int totalChapters = 0, bool skipNavigation = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var uriLastSegment = new Uri(chapterLink.Url).Segments.Last();
            var waitTarget = ScraperData.SiteConfig!.HasImagesForChapterContent ? "Image content" : "Text content";

            var chapterDataBuffer = new ChapterDataBuffer()
            {
                TempDirectory = tempImageDirectory,
                Url = chapterLink.Url
            };

            // Start animated spinner
            var spinnerCts = new CancellationTokenSource();
            Task? spinnerTask = null;

            if (totalChapters > 0)
            {
                spinnerTask = Task.Run(async () =>
                {
                    var spinner = new[] { '|', '/', '-', '\\' };
                    var spinnerIndex = 0;
                    while (!spinnerCts.Token.IsCancellationRequested)
                    {
                        Console.Write($"\rLoading chapter {currentChapter}/{totalChapters}, waiting for {waitTarget} {spinner[spinnerIndex]} ");
                        spinnerIndex = (spinnerIndex + 1) % spinner.Length;
                        try
                        {
                            await Task.Delay(150, spinnerCts.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                }, spinnerCts.Token);
            }

            if (!skipNavigation)
            {
                await driver.Navigate().GoToUrlAsync(chapterLink.Url);
            }

            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
                wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(
                    By.XPath(ScraperData.SiteConfig.Selectors.ChapterContent!)));
            }
            catch (WebDriverTimeoutException ex)
            {
                Logger.Error($"Timeout while waiting for elements on page {chapterLink.Url}: {ex.Message}");
                chapterDataBuffer.Title = uriLastSegment;
                spinnerCts?.Cancel();
                spinnerTask?.Wait();
                return chapterDataBuffer;
            }
            finally
            {
                await spinnerCts?.CancelAsync()!;
                if (spinnerTask != null)
                {
                    try
                    {
                        await spinnerTask;
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (totalChapters > 0)
            {
                Console.Write(
                    $"\r{new string(' ', 100)}\rLoading chapter {currentChapter}/{totalChapters}, waiting for {waitTarget} - ({stopwatch.ElapsedMilliseconds} ms)");
            }

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(driver.PageSource);

            var titleNode = htmlDocument.DocumentNode.SelectSingleNode(ScraperData.SiteConfig?.Selectors.ChapterTitle);
            chapterDataBuffer.Title = NormalizeChapterTitle(titleNode?.InnerText);
            Logger.Debug($"Chapter title: {chapterDataBuffer.Title}");

            var contentNodes = htmlDocument.DocumentNode.SelectNodes(ScraperData.SiteConfig?.Selectors.ChapterContent);

            if (ScraperData.SiteConfig!.HasImagesForChapterContent)
            {
                chapterDataBuffer = await AddImagePagesContentToChapterDataBuffer(chapterDataBuffer, contentNodes,
                    stopwatch, tempImageDirectory);
            }
            else
            {
                chapterDataBuffer =
                    AddTextContentToChapterDataBuffer(htmlDocument, chapterDataBuffer, contentNodes, chapterLink.Url);
            }

            return chapterDataBuffer;
        }

        private async Task<ChapterDataBuffer> AddImagePagesContentToChapterDataBuffer(
            ChapterDataBuffer chapterDataBuffer,
            HtmlNodeCollection contentNodes,
            Stopwatch stopwatch,
            string tempImageDirectory)
        {
            var pageUrls = contentNodes.Select(pageUrl =>
                pageUrl.Attributes[ScraperData.SiteConfig?.Selectors?.ChapterContentImageUrlAttribute].Value);

            var urls = pageUrls as string[] ?? pageUrls.ToArray();
            var isValidHttpUrls = urls.Select(url =>
                    Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp ||
                        uriResult.Scheme == Uri.UriSchemeHttps))
                .All(value => value);

            if (!isValidHttpUrls)
            {
                Logger.Error("Invalid page urls");
                return chapterDataBuffer;
            }

            chapterDataBuffer.SetPages(new List<PageData>());
            var counter = 0;
            Console.ForegroundColor = ConsoleColor.Cyan;
            foreach (var url in urls)
            {
                Logger.Debug($"Getting page image from {url}");
                stopwatch.Reset();
                var imagePath = await DownloadImageAsync(new Uri(url), tempImageDirectory);
                Logger.Debug($"Finished getting page image from {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
                Console.Write($"\r Downloaded page {++counter}/{contentNodes.Count} - Time taken: {stopwatch.ElapsedMilliseconds} ms");
                chapterDataBuffer.Pages.Add(new PageData
                {
                    Url = url,
                    ImagePath = imagePath
                });
            }

            Console.ResetColor();

            return chapterDataBuffer;
        }

        private async Task<ChapterDataBuffer> GetChapterDataAsync(string url)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var chapterDataBuffer = new ChapterDataBuffer();

            try
            {
                Logger.Debug($"Navigating to {url}");
                var (htmlDocument, uri) = await LoadHtmlAsync(new Uri(url));
                Logger.Info($"Finished navigating to {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
                stopwatch.Restart();

                var titleNode = htmlDocument.DocumentNode.SelectSingleNode(ScraperData.SiteConfig?.Selectors.ChapterTitle);
                chapterDataBuffer.Title =
                    titleNode != null ? NormalizeChapterTitle(titleNode.InnerText) : "Unknown Title";
                Logger.Debug($"Chapter title: {chapterDataBuffer.Title}");

                var paragraphNodes = htmlDocument.DocumentNode.SelectNodes(ScraperData.SiteConfig?.Selectors.ChapterContent);
                chapterDataBuffer = AddTextContentToChapterDataBuffer(htmlDocument, chapterDataBuffer, paragraphNodes, url);
                Logger.Info($"Finished processing chapter data. Time taken: {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return chapterDataBuffer;
            }
            finally
            {
                chapterDataBuffer.DateLastModified = DateTime.Now;
                chapterDataBuffer.Url = url;
                _semaphoreSlim.Release();
            }

            return chapterDataBuffer;
        }

        private ChapterDataBuffer AddTextContentToChapterDataBuffer(
            HtmlDocument htmlDocument,
            ChapterDataBuffer chapterDataBuffer,
            HtmlNodeCollection? paragraphNodes,
            string url)
        {
            var isWuxiaWorld =
                string.Equals(ScraperData.SiteConfig?.Name, "Wuxiaworld", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ScraperData.SiteConfig?.UrlPattern, "wuxiaworld.com",
                    StringComparison.OrdinalIgnoreCase);

            var paragraphs = paragraphNodes != null
                ? (isWuxiaWorld
                    ? paragraphNodes.Select(p => ExtractWuxiaWorldParagraphText(p)).ToList()
                    : paragraphNodes.Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList())
                : new List<string>();

            var minParagraphThreshold = ScraperData.SiteConfig?.MinimumChapterParagraphThreshold ??
                                        DefaultMinimumParagraphThreshold;

            // Guard: Try alternative selector if paragraph count is too low
            if (paragraphs.Count < minParagraphThreshold)
            {
                Logger.Warn(
                    $"Chapter content node count ({paragraphs.Count}) is below threshold ({minParagraphThreshold}) for {url}. Trying AlternativeChapterContent selector.");

                var altSelector = ScraperData.SiteConfig?.Selectors.AlternativeChapterContent;
                if (!string.IsNullOrWhiteSpace(altSelector))
                {
                    var alternateParagraphNodes = htmlDocument.DocumentNode.SelectNodes(altSelector);
                    var alternateParagraphs = alternateParagraphNodes != null
                        ? (isWuxiaWorld
                            ? alternateParagraphNodes.Select(p => ExtractWuxiaWorldParagraphText(p)).ToList()
                            : alternateParagraphNodes
                                .Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList())
                        : new List<string>();

                    Logger.Debug($"AlternativeChapterContent node count: {alternateParagraphs.Count}");

                    // Use alternate paragraphs if they provide more content
                    if (alternateParagraphs.Count > paragraphs.Count)
                    {
                        Logger.Debug("AlternativeChapterContent yielded more nodes; using it.");
                        paragraphs = alternateParagraphs;
                    }
                }
            }

            chapterDataBuffer.Content = string.Join("\n", paragraphs);

            // More robust than counting newlines: check actual non-whitespace character count.
            var nonWhitespaceCharCount = chapterDataBuffer.Content.Count(c => !char.IsWhiteSpace(c));

            if (nonWhitespaceCharCount < 20)
            {
                Logger.Debug(
                    $"No/insufficient text content found for {url} (non-whitespace chars: {nonWhitespaceCharCount}).");
                chapterDataBuffer.Content = "No content found";
            }

            return chapterDataBuffer;
        }

        private static string ExtractWuxiaWorldParagraphText(HtmlNode? pNode)
        {
            if (pNode == null)
            {
                return string.Empty;
            }

            var clone = pNode.CloneNode(true);

            // Remove the inline comment-count chip (and any other aria-hidden UI fragments).
            var ariaHiddenNodes = clone.SelectNodes(".//*[@aria-hidden='true']");
            if (ariaHiddenNodes != null)
            {
                foreach (var node in ariaHiddenNodes)
                {
                    node.Remove();
                }
            }

            // Walk text nodes in DOM order. This naturally merges punctuation-only spans correctly.
            var textNodes = clone.SelectNodes(".//text()");
            if (textNodes == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var textNode in textNodes)
            {
                var chunk = HtmlEntity.DeEntitize(textNode.InnerText);
                if (string.IsNullOrWhiteSpace(chunk))
                {
                    continue;
                }

                // Normalize internal whitespace.
                chunk = Regex.Replace(chunk, @"\s+", " ").Trim();
                if (chunk.Length == 0)
                {
                    continue;
                }

                // Avoid inserting spaces before punctuation.
                var startsWithPunct = chunk.Length > 0 && ".,;:!?)]}".IndexOf(chunk[0]) >= 0;

                if (sb.Length > 0 && !startsWithPunct && sb[^1] != ' ')
                {
                    sb.Append(' ');
                }

                sb.Append(chunk);
            }

            return sb.ToString().Trim();
        }

        protected sealed class SeleniumPageStep
        {
            public SeleniumPageStepType Type { get; }

            public string XPath { get; }

            public string? Description { get; }

            public int? TimeoutSeconds { get; }

            public Func<IWebDriver, WebDriverWait, Task>? CustomAction { get; }

            private SeleniumPageStep(
                SeleniumPageStepType type,
                string xPath,
                string? description,
                int? timeoutSeconds,
                Func<IWebDriver, WebDriverWait, Task>? customAction)
            {
                Type = type;
                XPath = xPath;
                Description = description;
                TimeoutSeconds = timeoutSeconds;
                CustomAction = customAction;
            }

            public static SeleniumPageStep Click(string xPath, string? description = null,
                int? timeoutSeconds = null) =>
                new SeleniumPageStep(SeleniumPageStepType.Click, xPath, description, timeoutSeconds, null);

            public static SeleniumPageStep WaitForPresence(string xPath, string? description = null,
                int? timeoutSeconds = null) =>
                new SeleniumPageStep(SeleniumPageStepType.WaitForPresence, xPath, description, timeoutSeconds, null);

            public static SeleniumPageStep WaitForClickable(string xPath, string? description = null,
                int? timeoutSeconds = null) =>
                new SeleniumPageStep(SeleniumPageStepType.WaitForClickable, xPath, description, timeoutSeconds, null);

            public static SeleniumPageStep Custom(Func<IWebDriver, WebDriverWait, Task> customAction,
                string? description = null, int? timeoutSeconds = null) =>
                new SeleniumPageStep(SeleniumPageStepType.Custom, "(custom)", description, timeoutSeconds,
                    customAction);
        }
    }
}
