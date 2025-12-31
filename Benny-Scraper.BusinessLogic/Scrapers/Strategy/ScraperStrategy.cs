using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.Helper;
using Benny_Scraper.BusinessLogic.Utilities;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Polly;
using SeleniumExtras.WaitHelpers;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Cookie = System.Net.Cookie;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    namespace Impl
    {
        //The NovelDataInitializer represents an abstraction around populating a NovelDataBuffer object with its required content.
        //Each strategy implements a corresponding child of the NovelDataInitializer class which is used to neatly
        //encapsulate the strategy-specific content fetching functions in the Impl namespace.
        //
        //Note that the data & methods used by the Initializer are static so that the strategies do not have to
        //contain a data member of the class in order to call the given FetchNovelContent method (implemented on each
        //child class).
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

            public static async Task FetchContentByAttributeAsync(Attr attr, NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData)
            {
                switch (attr)
                {
                    case Attr.Title:
                        var titleNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelTitle);
                        if (titleNodes != null && titleNodes.Any())
                        {
                            novelDataBuffer.Title = HtmlEntity.DeEntitize(titleNodes.First().InnerText.Trim());
                        }
                        Console.WriteLine($"Title: {novelDataBuffer.Title}");
                        break;

                    case Attr.Author:
                        var authorNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelAuthor);
                        novelDataBuffer.Author = authorNode != null ? HtmlEntity.DeEntitize(authorNode.InnerText.Trim()) : string.Empty;
                        Console.WriteLine($"Author: {novelDataBuffer.Author}");
                        break;

                    case Attr.Category:
                        //TODO: Implement
                        break;

                    case Attr.NovelRating:
                        var novelRatingNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelRating);
                        if (novelRatingNode != null && double.TryParse(novelRatingNode.InnerText.Trim(), out double rating))
                        {
                            novelDataBuffer.Rating = rating;
                            Console.WriteLine($"Rating: {novelDataBuffer.Rating}");
                        }
                        break;

                    case Attr.TotalRatings:
                        var totalRatingsNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.TotalRatings);
                        if (totalRatingsNode != null && int.TryParse(totalRatingsNode.InnerText.Trim(), out int totalRatings))
                        {
                            novelDataBuffer.TotalRatings = totalRatings;
                            Console.WriteLine($"Total Ratings: {novelDataBuffer.TotalRatings}");
                        }
                        break;

                    case Attr.Description:
                        var descriptionNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelDescription);
                        if (descriptionNodes != null)
                        {
                            novelDataBuffer.Description = descriptionNodes.Select(description => HtmlEntity.DeEntitize(description.InnerText.Trim())).ToList();
                            Console.WriteLine($"Description line count: {novelDataBuffer.Description.Count}");
                        }
                        break;

                    case Attr.Genres:
                        var genreNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelGenres);
                        if (genreNodes != null)
                        {
                            novelDataBuffer.Genres = genreNodes.Select(genre => HtmlEntity.DeEntitize(genre.InnerText.Trim())).ToList();
                            Console.WriteLine($"Total Genres: {novelDataBuffer.Genres.Count}");
                        }
                        break;

                    case Attr.AlternativeNames:
                        var alternateNameNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelAlternativeNames);
                        if (alternateNameNodes != null)
                        {
                            List<string> alternateNames = alternateNameNodes.Select(alternateName => HtmlEntity.DeEntitize(alternateName.InnerText.Trim())).ToList();
                            if (alternateNames.Any())
                            {
                                // SelectMany flattens a list of lists into a single list.
                                novelDataBuffer.AlternativeNames = alternateNames.SelectMany(altName => SplitByLanguage(altName)).ToList();
                            }
                        }
                        Console.WriteLine($"Checked for alternate names");
                        break;

                    case Attr.NovelStatus:
                        var statusNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelStatus);
                        if (statusNode != null && scraperData?.SiteConfig?.CompletedStatus != null)
                        {
                            novelDataBuffer.NovelStatus = statusNode.InnerText.Trim();
                            novelDataBuffer.IsNovelCompleted = novelDataBuffer.NovelStatus.ToLowerInvariant().Contains(scraperData.SiteConfig.CompletedStatus);
                            Console.WriteLine($"NovelStatus: {novelDataBuffer.NovelStatus}");
                        }
                        break;

                    case Attr.ThumbnailUrl:
                        var urlNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelThumbnailUrl);

                        // Guard: Check if URL node exists and has the required attribute
                        if (urlNode == null || urlNode.Attributes[scraperData.SiteConfig?.Selectors.ThumbnailUrlAttribute] == null)
                            break;

                        var url = urlNode.Attributes[scraperData.SiteConfig?.Selectors.ThumbnailUrlAttribute].Value;
                        bool isValidHttpUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                        Uri absoluteUri = isValidHttpUrl ? new Uri(url) : new Uri(scraperData.BaseUri, url);

                        using (var client = scraperData.HttpClientFactory?.CreateClient() ?? new HttpClient())
                        {
                            try
                            {
                                var thumbnailBytes = await client.GetByteArrayAsync(absoluteUri);
                                novelDataBuffer.ThumbnailImage = thumbnailBytes;
                            }
                            catch (Exception e)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Failed to download thumbnail image for novel {novelDataBuffer.Title} at url {absoluteUri}. Exception: {e}");
                                Console.ResetColor();
                            }
                        }
                        novelDataBuffer.ThumbnailUrl = url;
                        break;

                    case Attr.LastTableOfContentsPage:
                        try
                        {
                            var lastTableOfContentsPageNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.LastTableOfContentsPage);

                            // Guard: Check if node exists and has href attribute
                            if (lastTableOfContentsPageNode == null || lastTableOfContentsPageNode.Attributes["href"] == null)
                                break;

                            novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsPageNode.Attributes["href"].Value;
                            Console.WriteLine($"Last Table of Contents Page: {novelDataBuffer.LastTableOfContentsPageUrl}");
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Failed to get last table of contents page for novel {novelDataBuffer.Title} at url {scraperData.SiteTableOfContents}. Exception: {e}");
                            Console.ResetColor();
                            throw;
                        }
                    case Attr.ChapterUrls:
                        var chapterLinkNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.ChapterLinks);
                        var chapterUrls = new List<string>();

                        // Extract chapter URLs if nodes found
                        if (chapterLinkNodes != null)
                        {
                            chapterUrls = chapterLinkNodes.Select(chapterLink => chapterLink.Attributes["href"].Value).ToList();

                            // Convert relative URLs to absolute URLs
                            if (chapterUrls.Any() && !IsValidHttpUrl(chapterUrls.First()))
                            {
                                chapterUrls = chapterUrls.Select(chapterUrl => new Uri(scraperData.BaseUri, chapterUrl).ToString()).ToList();
                            }
                        }

                        // Guard: Warn if no chapter URLs found
                        if (!chapterUrls.Any())
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Failed to get chapter urls for novel {novelDataBuffer.Title} at url {scraperData.BaseUri}");
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"Please check the appsettings.json for {scraperData.SiteConfig.Name} the chapterLinks key");
                            Console.ResetColor();
                        }

                        novelDataBuffer.ChapterUrls = chapterUrls;
                        Console.WriteLine($"Got chapter urls, total: {chapterUrls.Count}");
                        break;

                    case Attr.FirstChapterUrl:
                        //TODO: Implement
                        break;

                    case Attr.CurrentChapter:
                        var latestChapterNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.LatestChapterLink);

                        // Guard: No latest chapter node found
                        if (latestChapterNode == null)
                            return;

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

                        novelDataBuffer.MostRecentChapterTitle = HtmlEntity.DeEntitize(latestChapterNode.InnerText).Trim();
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
            public static List<string> SplitByLanguage(string input)
            {
                List<string> result = new List<string>();
                StringBuilder currentString = new StringBuilder();
                bool? isLastCharAsian = null;

                foreach (char c in input)
                {
                    //http://www.rikai.com/library/kanjitables/kanji_codes.unicode.shtml
                    bool isCurrentCharAsian = (c >= 0x3000 && c <= 0x9FFF) || (c >= 0x4E00 && c <= 0x9FFF); // range of Asian characters

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
                return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            }

        }
    }

    public sealed class ScraperData
    {
        public SiteConfiguration? SiteConfig { get; set; }
        public Uri? SiteTableOfContents { get; set; }
        public Uri? BaseUri { get; set; }
        public IHttpClientFactory? HttpClientFactory { get; set; }
        public ChapterRange? ChapterRange { get; set; }
        public string? DetectedVolumeName { get; set; }
    }

    public abstract class ScraperStrategy
    {
        protected readonly ScraperData ScraperData = new ScraperData();
        private int ConcurrentRequestsLimit { get; set; } = 2;

        private const int MaxRetries = 6;
        private const int MinimumParagraphThreshold = 5;
        protected const int TotalPossiblePaginationTabs = 6;
        protected static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        protected static readonly NovelScraperSettings Settings = new NovelScraperSettings();
        private readonly IHttpClientFactory _httpClientFactory;
        private SemaphoreSlim _semaphoreSlim; // limit the number of concurrent requests, prevent posssible rate limiting
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

        protected ScraperStrategy(IHttpClientFactory? httpClientFactory = null)
        {
            _httpClientFactory = httpClientFactory ?? new HttpClientFactory();
            ScraperData.HttpClientFactory = _httpClientFactory;
            _semaphoreSlim = new SemaphoreSlim(ConcurrentRequestsLimit);
        }

        public abstract Task<NovelDataBuffer> ScrapeAsync();

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

        public void SetChapterRange(ChapterRange? chapterRange, string? volumeName = null)
        {
            ScraperData.ChapterRange = chapterRange;
            ScraperData.DetectedVolumeName = volumeName;
            if (chapterRange != null)
            {
                Logger.Info($"Chapter range set: {chapterRange}{(volumeName != null ? $" ({volumeName})" : "")}");
            }
        }

        public ChapterRange? GetChapterRange()
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
        public List<string> FilterChaptersByRange(List<string> chapterUrls, out List<string>? filteredTitles, List<string>? chapterTitles = null)
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
        public virtual void ExtractChapterUrlsAndTitles(HtmlDocument htmlDocument, NovelDataBuffer novelDataBuffer, ScraperData scraperData)
        {
            try
            {
                Logger.Info("Extracting chapter URLs and titles from table of contents");

                var chapterLinkNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.ChapterLinks);

                if (chapterLinkNodes == null || !chapterLinkNodes.Any())
                {
                    Logger.Warn("No chapter link nodes found");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to get chapter urls for novel {novelDataBuffer.Title} at url {scraperData.BaseUri}");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"Please check the appsettings.json for {scraperData.SiteConfig.Name} the chapterLinks key");
                    Console.ResetColor();
                    return;
                }

                var chapterUrls = new List<string>();
                var chapterTitles = new List<string>();

                for (int i = 0; i < chapterLinkNodes.Count; i++)
                {
                    var node = chapterLinkNodes[i];

                    // Extract URL
                    var url = node.Attributes["href"]?.Value;
                    if (!string.IsNullOrEmpty(url))
                    {
                        chapterUrls.Add(url);
                    }

                    // Extract title
                    var title = HtmlEntity.DeEntitize(node.InnerText?.Trim());
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = $"Chapter {i + 1}";
                    }
                    chapterTitles.Add(title);
                }

                // Convert relative URLs to absolute URLs if needed
                if (chapterUrls.Any() && !IsValidHttpUrl(chapterUrls.First()))
                {
                    chapterUrls = chapterUrls.Select(url => new Uri(scraperData.BaseUri, url).ToString()).ToList();
                }

                novelDataBuffer.ChapterUrls = chapterUrls;
                novelDataBuffer.ChapterTitles = chapterTitles;

                Logger.Info($"Extracted {chapterUrls.Count} chapter URLs and {chapterTitles.Count} titles");
                Console.WriteLine($"Got chapter urls, total: {chapterUrls.Count}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting chapter URLs and titles: {ex.Message}");
            }
        }

        /// <summary>
        /// Sorts chapters based on the site configuration's ChapterSortOrder setting.
        /// Applies to both ChapterUrls and ChapterTitles in the NovelDataBuffer.
        /// </summary>
        public virtual void SortChapters(NovelDataBuffer novelDataBuffer)
        {
            if (ScraperData.SiteConfig?.ChapterSortOrder == null)
            {
                Logger.Debug("No chapter sort order configured, keeping default order");
                return;
            }

            var sortOrder = ScraperData.SiteConfig.ChapterSortOrder;

            if (sortOrder == Config.ChapterSortOrder.None)
            {
                Logger.Debug("Chapter sort order set to None, keeping original order");
                return;
            }

            if (sortOrder == Config.ChapterSortOrder.Descending)
            {
                Logger.Debug("Reversing chapter order (Descending -> Ascending)");
                novelDataBuffer.ChapterUrls.Reverse();
                novelDataBuffer.ChapterTitles.Reverse();
            }
            else
            {
                Logger.Debug("Chapter sort order is Ascending (default), no reversal needed");
            }
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

        protected async Task<(HtmlDocument document, Uri updatedUri)> LoadHtmlAsync(Uri uri)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<(HtmlDocument, Uri)>(result => false) // Retry if the result is null
                .WaitAndRetryAsync(MaxRetries, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // exponential back-off
                    (outcome, timeSpan, retryCount, context) =>
                    {
                        if (outcome.Exception != null)
                        {
                            // Log the exception details here
                            Logger.Warn($"Error occurred while navigating to {uri}. Code: Attempt: {retryCount}");
                        }
                        else
                        {
                            // Log that the chapter is being skipped
                            Logger.Warn($"Skipping chapter due to repeated failures: {uri}. Attempt: {retryCount}");
                        }
                    });

            return await retryPolicy.ExecuteAsync(async context =>
            {
                using var client = _httpClientFactory.CreateClient();

                var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                string userAgent = _userAgents[System.Threading.Interlocked.Increment(ref _userAgentIndex) % _userAgents.Count];

                if (ScraperData.BaseUri == new Uri("https://www.lightnovelworld.com/"))
                    userAgent = _userAgents[0];

                // Add realistic browser headers to bypass Cloudflare
                requestMessage.Headers.Add("User-Agent", userAgent);
                requestMessage.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
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
                    requestMessage.Headers.Add("Referer", ScraperData.BaseUri.ToString());

                requestMessage.Options.Set(new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"), TimeSpan.FromSeconds(10));

                // Add random delay between 100-500ms to avoid predictable request patterns
                // This helps bypass Cloudflare's bot detection which looks for mechanical timing
                int randomDelay = _random.Next(100, 500);
                await Task.Delay(randomDelay);

                Logger.Debug($"Sending request to {uri}");

                var response = await client.SendAsync(requestMessage);

                Logger.Debug($"Response Status: {(int)response.StatusCode} {response.StatusCode}");
                Logger.Debug($"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"))}");

                // Guard: Handle unsuccessful response
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Request failed to {uri}");
                    Logger.Error($"Status Code: {(int)response.StatusCode} {response.StatusCode}");
                    Logger.Error($"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"))}");

                    try
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();

                        if (string.IsNullOrEmpty(errorContent))
                        {
                            // No content to log
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

                    // Guard: Handle too many requests at max retries
                    if (response.StatusCode == HttpStatusCode.TooManyRequests && (int)context["RetryCount"] >= MaxRetries)
                        return (null, uri);

                    throw new HttpRequestException($"Failed to load HTML document from {uri} after {MaxRetries} attempts. Status code: {response.StatusCode}");
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
            }, new Context { ["RetryCount"] = 0 });
        }

        protected async Task<string> DownloadImageAsync(Uri uri, string tempImageDirectory)
        {
            var uriString = uri.ToString();
            uriString = uriString.Replace("amp;", "");
            uri = new Uri(uriString);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            int retryCount = 0;
            while (retryCount < MaxRetries)
            {
                try
                {
                    using var client = _httpClientFactory.CreateClient();

                    var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                    var userAgent = _userAgents[System.Threading.Interlocked.Increment(ref _userAgentIndex) % _userAgents.Count];

                    // Add realistic browser headers for image downloads
                    requestMessage.Headers.Add("User-Agent", userAgent);
                    requestMessage.Headers.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                    requestMessage.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                    requestMessage.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                    requestMessage.Headers.Add("DNT", "1");
                    requestMessage.Headers.Add("Connection", "keep-alive");
                    requestMessage.Headers.Add("Sec-Fetch-Dest", "image");
                    requestMessage.Headers.Add("Sec-Fetch-Mode", "no-cors");
                    requestMessage.Headers.Add("Sec-Fetch-Site", "cross-site");

                    if (ScraperData.BaseUri != null)
                        requestMessage.Headers.Add("Referer", ScraperData.BaseUri.ToString());

                    using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);
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
                    retryCount++;
                    Logger.Error($"Error occurred while navigating to {uri}. Error: {e}. Attempt: {retryCount}");
                    await Task.Delay(3000);
                }
            }
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                if (Directory.Exists(tempImageDirectory))
                {
                    Directory.Delete(tempImageDirectory, true);
                    Logger.Info($"Application shutdown. Temp directory {tempImageDirectory} deleted");
                }
            };
            throw new HttpRequestException($"Failed to download image from {uri} after {MaxRetries} attempts.");
        }

        #region Url Helpers
        public bool IsValidHttpUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        protected virtual Uri TrimLastUriSegment(Uri siteUri)
        {
            string allSegementsButLast = siteUri.Segments.Take(siteUri.Segments.Length - 1).Aggregate(
                (segment1, segment2) => segment1 + segment2);
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
                uriResult = new Uri(baseUri, uriResult);

            if (result && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                var pageNumberFromQuery = HttpUtility.ParseQueryString(uriResult.Query);
                if (pageNumberFromQuery.AllKeys.Contains("page"))
                {
                    var pageNumber = pageNumberFromQuery["page"];
                    if (int.TryParse(pageNumber, out int page))
                        return page;
                }
            }

            return -1;
        }
        #endregion

        /// <summary>
        /// Gets the chapter urls from the table of contents page that requires pagination to get chapters.
        /// </summary>
        /// <param name="tableOfContentUri"></param>
        /// <param name="getAllChapters"></param>
        /// <param name="pageToStopAt"></param>
        /// <param name="pageToStartAt"></param>
        /// <returns>Returns a ValueTuple that contains all the chapter urls and the url for the last page of the table of contents</returns>
        /// <summary>
        /// Fetches chapter URLs and titles across multiple paginated table of contents pages.
        /// Returns both URLs, titles, and the last table of contents URL.
        /// </summary>
        protected virtual async Task<(List<string> ChapterUrls, List<string> ChapterTitles, string LastTableOfContentsUrl)> GetPaginatedChapterUrlsAsync(Uri tableOfContentUri, bool getAllChapters, int pageToStopAt, int pageToStartAt = 1)
        {
            var chapterUrls = new List<string>();
            var chapterTitles = new List<string>();

            var baseTableOfContentUrl = tableOfContentUri + ScraperData.SiteConfig?.PaginationType;
            var lastTableOfContentsUrl = string.Format(baseTableOfContentUrl, pageToStopAt);

            for (int i = pageToStartAt; i <= pageToStopAt; ++i)
            {
                var tableOfContentUrl = string.Format(baseTableOfContentUrl, i);
                bool isPageNew = i > pageToStartAt;
                try
                {
                    Logger.Info($"Navigating to {tableOfContentUrl}");
                    var (htmlDocument, uri) = await LoadHtmlAsync(new Uri(tableOfContentUrl));

                    var (urlsOnPage, titlesOnPage) = GetChapterUrlsAndTitlesInRange(htmlDocument, ScraperData.BaseUri, 1);
                    if (urlsOnPage.Any())
                    {
                        chapterUrls.AddRange(urlsOnPage);
                        chapterTitles.AddRange(titlesOnPage);
                    }

                    if (!getAllChapters && !isPageNew)
                    {
                        break;
                    }
                }
                catch (HttpRequestException e)
                {
                    Logger.Error($"Error occurred while navigating to {tableOfContentUrl}. Error: {e}");
                }
            }

            return (chapterUrls, chapterTitles, lastTableOfContentsUrl);
        }

        protected virtual async Task<List<string>> GetChapterUrls(Uri tableOfContentUri, bool getAllChapters, int pageToStopAt, int pageToStartAt = 1)
        {
            List<string> chapterUrls = new List<string>();
            string baseTableOfContentUrl = tableOfContentUri + ScraperData.SiteConfig?.PaginationType;

            for (int i = pageToStartAt; i <= pageToStopAt; i++)
            {
                string tableOfContentUrl = string.Format(baseTableOfContentUrl, i);
                bool isPageNew = i > pageToStartAt;
                try
                {
                    Logger.Info($"Navigating to {tableOfContentUrl}");
                    (HtmlDocument htmlDocument, Uri uri) = await LoadHtmlAsync(new Uri(tableOfContentUrl));

                    List<string> chapterUrlsOnContentPage = GetChapterUrlsInRange(htmlDocument, ScraperData.BaseUri, 1);
                    if (chapterUrlsOnContentPage.Any())
                    {
                        chapterUrls.AddRange(chapterUrlsOnContentPage);
                    }

                    if (!getAllChapters && !isPageNew)
                    {
                        break;
                    }
                }
                catch (HttpRequestException e)
                {
                    Logger.Error($"Error occurred while navigating to {tableOfContentUrl}. Error: {e}");
                }
            }

            return chapterUrls;
        }

        public virtual async Task<List<ChapterDataBuffer>> GetChaptersDataAsync(List<string> chapterUrls)
        {
            var tempImageDirectory = string.Empty;
            int sequenceNumber = 1;
            try
            {
                Logger.Info("Getting chapters data");
                var tasks = new List<Task<ChapterDataBuffer>>();
                var chapterDataBuffers = new List<ChapterDataBuffer>();

                // Use Selenium for sites with images or if chapter content requires Selenium
                if (ShouldUseSeleniumForChapters())
                {
                    await ProcessChaptersWithSelenium(chapterUrls, tasks, chapterDataBuffers, tempImageDirectory);
                }
                else
                {
                    await ProcessChaptersWithHttpClient(chapterUrls, tasks, chapterDataBuffers);
                }
                chapterDataBuffers.ForEach(chapterDataBuffer => chapterDataBuffer.SequenceNumber = sequenceNumber++);
                return chapterDataBuffers;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting chapters data. {ex}");
                if (!string.IsNullOrEmpty(tempImageDirectory))
                {
                    Directory.Delete(tempImageDirectory, true);
                    Logger.Info("Finished deleting temp directory");
                }
                throw;
            }
        }

        /// <summary>
        /// Extracts both chapter URLs and titles from an HTML document in a specified range.
        /// This is used for paginated sites where chapters span multiple pages.
        /// </summary>
        protected virtual (List<string> Urls, List<string> Titles) GetChapterUrlsAndTitlesInRange(HtmlDocument htmlDocument, Uri baseSiteUri, int? startChapter = null, int? endChapter = null)
        {
            Logger.Info($"Getting chapter URLs and titles from table of contents");
            try
            {
                HtmlNodeCollection chapterLinks = htmlDocument.DocumentNode.SelectNodes(ScraperData.SiteConfig?.Selectors.ChapterLinks);

                if (chapterLinks == null)
                {
                    Logger.Info("Chapter links Node Collection on table of contents page was null.");
                    return (new List<string>(), new List<string>());
                }

                List<string> chapterUrls = new List<string>();
                List<string> chapterTitles = new List<string>();
                int chapterIndex = 0;

                foreach (var link in chapterLinks)
                {
                    chapterIndex++;
                    string chapterUrl = link.Attributes["href"]?.Value ?? string.Empty;

                    if (!string.IsNullOrEmpty(chapterUrl) &&
                        (startChapter == null || chapterIndex >= startChapter) &&
                        (endChapter == null || chapterIndex <= endChapter))
                    {
                        string fullUrl = new Uri(baseSiteUri, chapterUrl.TrimStart('/')).ToString();
                        chapterUrls.Add(fullUrl);

                        // Extract title
                        var title = HtmlEntity.DeEntitize(link.InnerText?.Trim());
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            title = $"Chapter {chapterUrls.Count}";
                        }
                        chapterTitles.Add(title);
                    }
                }

                return (chapterUrls, chapterTitles);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting chapter urls and titles from table of contents. {ex}");
                return (new List<string>(), new List<string>());
            }
        }

        protected virtual List<string> GetChapterUrlsInRange(HtmlDocument htmlDocument, Uri baseSiteUri, int? startChapter = null, int? endChapter = null)
        {
            Logger.Info($"Getting chapter urls from table of contents");
            try
            {
                HtmlNodeCollection chapterLinks = htmlDocument.DocumentNode.SelectNodes(ScraperData.SiteConfig?.Selectors.ChapterLinks);

                if (chapterLinks == null)
                {
                    Logger.Info("Chapter links Node Collection on table of contents page was null.");
                    return new List<string>();
                }

                List<string> chapterUrls = new List<string>();
                int chapterIndex = 0;

                foreach (var link in chapterLinks)
                {
                    chapterIndex++;
                    string chapterUrl = link.Attributes["href"]?.Value ?? string.Empty;

                    if (!string.IsNullOrEmpty(chapterUrl) &&
                        (startChapter == null || chapterIndex >= startChapter) &&
                        (endChapter == null || chapterIndex <= endChapter))
                    {
                        string fullUrl = new Uri(baseSiteUri, chapterUrl.TrimStart('/')).ToString();
                        chapterUrls.Add(fullUrl);
                    }
                }

                return chapterUrls;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting chapter urls from table of contents. {ex}");
                throw;
            }
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

        #region Private Methods

        private async Task ProcessChaptersWithSelenium(List<string> chapterUrls, List<Task<ChapterDataBuffer>> tasks, List<ChapterDataBuffer> chapterDataBuffers, string tempImageDirectory)
        {
            Logger.Debug("Using Selenium to get chapters data");
            IDriverFactory driverFactory = new DriverFactory();
            var driver = await driverFactory.CreateDriverAsync(chapterUrls.First(), isHeadless: true);

            tempImageDirectory = CommonHelper.CreateTempDirectory();

            foreach (var url in chapterUrls)
            {
                tasks.Add(GetChapterDataAsync(driver, url, tempImageDirectory));
            }

            try
            {
                var taskResults = await Task.WhenAll(tasks);
                chapterDataBuffers.AddRange(taskResults);
                Logger.Info($"Finished getting chapters data. Total chapters: {chapterDataBuffers.Count}");
                Logger.Debug("Disposing all drivers");
                Console.WriteLine($"Total drivers: {driverFactory.GetAllDrivers().Count}");
                driverFactory.DisposeAllDrivers();
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
                driverFactory.DisposeAllDrivers();
                Logger.Debug("Finished closing driver");
            }
        }

        private async Task ProcessChaptersWithHttpClient(List<string> chapterUrls, List<Task<ChapterDataBuffer>> tasks, List<ChapterDataBuffer> chapterDataBuffers)
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
                throw new ArgumentException("Concurrent request limit must be greater than 0");
            if (concurrentRequestLimit > Environment.ProcessorCount)
                concurrentRequestLimit = Environment.ProcessorCount;

            this.ConcurrentRequestsLimit = concurrentRequestLimit;
        }

        public void SetSemaphoreLimit(int concurrentRequestLimit)
        {
            _semaphoreSlim = new SemaphoreSlim(concurrentRequestLimit);
        }

        private async Task<ChapterDataBuffer> GetChapterDataAsync(IWebDriver driver, string urls, string tempImageDirectory)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var uriLastSegment = new Uri(urls).Segments.Last();

            var chapterDataBuffer = new ChapterDataBuffer()
            {
                TempDirectory = tempImageDirectory,
                Url = urls
            };

            Logger.Debug($"Navigating to {urls}");
            driver.Navigate().GoToUrl(urls);
            try
            {
                Logger.Debug($"Waiting for images on page {urls} to load.");
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
                wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(ScraperData.SiteConfig?.Selectors.ChapterContent)));
                Logger.Warn("Images have been loaded.");
            }
            catch (WebDriverTimeoutException ex)
            {
                Logger.Error($"Timeout while waiting for elements on page {urls}: {ex.Message}");
                chapterDataBuffer.Title = uriLastSegment;
                return chapterDataBuffer;
            }

            Logger.Info($"Finished navigating to {urls} Time taken: {stopwatch.ElapsedMilliseconds} ms");
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(driver.PageSource);


            HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(ScraperData.SiteConfig?.Selectors.ChapterTitle);
            chapterDataBuffer.Title = titleNode != null ? titleNode.InnerText.Trim() : uriLastSegment;
            Logger.Debug($"Chapter title: {chapterDataBuffer.Title}");

            HtmlNodeCollection pageUrlNodes = htmlDocument.DocumentNode.SelectNodes(ScraperData.SiteConfig?.Selectors.ChapterContent);

            // Guard: No page content nodes found
            if (pageUrlNodes == null)
            {
                Logger.Error("No page content nodes found");
                return chapterDataBuffer;
            }

            var pageUrls = pageUrlNodes.Select(pageUrl => pageUrl.Attributes[ScraperData.SiteConfig?.Selectors?.ChapterContentImageUrlAttribute].Value);
            bool isValidHttpUrls = pageUrls.Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)).All(value => value);

            // Guard: Invalid page URLs
            if (!isValidHttpUrls)
            {
                Logger.Error("Invalid page urls");
                return chapterDataBuffer;
            }

            chapterDataBuffer.Pages = new List<PageData>();
            foreach (var url in pageUrls)
            {
                Logger.Debug($"Getting page image from {url}");
                stopwatch.Reset();
                var imagePath = await DownloadImageAsync(new Uri(url), tempImageDirectory);
                Logger.Debug($"Finished getting page image from {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
                chapterDataBuffer.Pages.Add(new PageData
                {
                    Url = url,
                    ImagePath = imagePath
                });
            }

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

                HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(ScraperData.SiteConfig?.Selectors.ChapterTitle);
                chapterDataBuffer.Title = titleNode != null ? titleNode.InnerText.Trim() : "Unknown Title";
                Logger.Debug($"Chapter title: {chapterDataBuffer.Title}");

                HtmlNodeCollection paragraphNodes = htmlDocument.DocumentNode.SelectNodes(ScraperData.SiteConfig?.Selectors.ChapterContent);
                var paragraphs = paragraphNodes != null
                    ? paragraphNodes.Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList()
                    : new List<string>();

                // Guard: Try alternative selector if paragraph count is too low
                if (paragraphs.Count < MinimumParagraphThreshold)
                {
                    Logger.Warn($"Paragraphs count is less than 5. Trying alternative selector");
                    HtmlNodeCollection alternateParagraphNodes = htmlDocument.DocumentNode.SelectNodes(ScraperData.SiteConfig?.Selectors.AlternativeChapterContent);
                    List<string> alternateParagraphs = alternateParagraphNodes != null
                        ? alternateParagraphNodes.Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList()
                        : new List<string>();
                    Logger.Debug($"Alternate paragraphs count: {alternateParagraphs.Count}");

                    // Use alternate paragraphs if they provide more content
                    if (alternateParagraphs.Count > paragraphs.Count)
                    {
                        Logger.Debug($"Alternate paragraphs count is greater than paragraphs count. Using alternate paragraphs");
                        paragraphs = alternateParagraphs;
                    }
                }

                chapterDataBuffer.Content = string.Join("\n", paragraphs);
                int contentCount = chapterDataBuffer.Content.Count(c => c == '\n');

                if (string.IsNullOrWhiteSpace(chapterDataBuffer.Content) || contentCount < 5)
                {
                    Logger.Debug($"No content found for {url}");
                    chapterDataBuffer.Content = "No content found";
                }


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
        #endregion

    }
}
