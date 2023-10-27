using System.Diagnostics;
using System.Net;
using System.Text;
using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.Helper;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Polly;
using SeleniumExtras.WaitHelpers;

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
            protected enum Attr
            {
                Title,
                Author,
                Category,
                NovelRating,
                TotalRatings,
                Description,
                Genres,
                AlternativeNames,
                //TODO: 'Status' is ambiguous. Should this attribute and the corresponding data member of NovelDataBuffer be renamed
                //  to clarify it's purpose?
                Status,
                ThumbnailUrl,
                LastTableOfContentsPage,
                ChapterUrls,
                FirstChapterUrl,
                LatestChapter
            }

            protected static void FetchContentByAttribute(Attr attr, NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData)
            {
                switch (attr)
                {
                    case Attr.Title:
                        var titleNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelTitle);
                        if (titleNodes.Any())
                        {
                            novelDataBuffer.Title = titleNodes != null ? HtmlEntity.DeEntitize(titleNodes.First().InnerText.Trim()) : string.Empty;
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
                        novelDataBuffer.Rating = double.Parse(novelRatingNode.InnerText.Trim());
                        Console.WriteLine($"Rating: {novelDataBuffer.Rating}");
                        break;

                    case Attr.TotalRatings:
                        var totalRatingsNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.TotalRatings);
                        novelDataBuffer.TotalRatings = int.Parse(totalRatingsNode.InnerText.Trim());
                        Console.WriteLine($"Total Ratings: {novelDataBuffer.TotalRatings}");
                        break;

                    case Attr.Description:
                        var descriptionNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelDescription);
                        novelDataBuffer.Description = descriptionNodes.Select(description => HtmlEntity.DeEntitize(description.InnerText.Trim())).ToList();
                        Console.WriteLine($"Description word count: {novelDataBuffer.Description.Count}");
                        break;

                    case Attr.Genres:
                        var genreNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelGenres);
                        novelDataBuffer.Genres = genreNodes.Select(genre => HtmlEntity.DeEntitize(genre.InnerText.Trim())).ToList();
                        Console.WriteLine($"Total Genres: {novelDataBuffer.Genres.Count}");
                        break;

                    case Attr.AlternativeNames:
                        var alternateNameNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelAlternativeNames);
                        List<string> alternateNames = alternateNameNodes.Select(alternateName => HtmlEntity.DeEntitize(alternateName.InnerText.Trim())).ToList();
                        if (alternateNames.Any())
                        {
                            // SelectMany flattens a list of lists into a single list.
                            novelDataBuffer.AlternativeNames = alternateNames.SelectMany(altName => SplitByLanguage(altName)).ToList();
                        }
                        Console.WriteLine($"Checked for alternate names");
                        break;

                    case Attr.Status:
                        var statusNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelStatus);
                        novelDataBuffer.NovelStatus = statusNode.InnerText.Trim();
                        novelDataBuffer.IsNovelCompleted = novelDataBuffer.NovelStatus.ToLowerInvariant().Contains(scraperData.SiteConfig.CompletedStatus);
                        Console.WriteLine($"Status: {novelDataBuffer.NovelStatus}");
                        break;

                    case Attr.ThumbnailUrl:
                        var urlNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelThumbnailUrl);
                        var url = urlNode.Attributes[scraperData.SiteConfig?.Selectors.ThumbnailUrlAttribute].Value;
                        bool isValidHttpUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                        Uri absoluteUri = isValidHttpUrl ? new Uri(url) : new Uri(scraperData.BaseUri, url);
                        using (var client = new HttpClient())
                        {
                            try
                            {
                                var thumbnailBytes = client.GetByteArrayAsync(absoluteUri).Result;
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
                        var lastTableOfContentsPageNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.LastTableOfContentsPage);
                        novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsPageNode.Attributes["href"].Value;
                        Console.WriteLine($"Last Table of Contents Page: {novelDataBuffer.LastTableOfContentsPageUrl}");
                        break;

                    case Attr.ChapterUrls:
                        var chapterLinkNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.ChapterLinks);
                        List<string> chapterUrls = chapterLinkNodes.Select(chapterLink => chapterLink.Attributes["href"].Value).ToList();
                        if (chapterUrls.Any() && !IsValidHttpUrl(chapterUrls.First()))
                        {
                            chapterUrls = chapterUrls.Select(chapterUrl => new Uri(scraperData.BaseUri, chapterUrl).ToString()).ToList();
                        }
                        if (!chapterUrls.Any())
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Failed to get chapter urls for novel {novelDataBuffer.Title} at url {scraperData.BaseUri}");
                            Console.WriteLine($"Please check the appsettings.json for {scraperData.SiteConfig.Name} the chapterLinks key");
                        }
                        novelDataBuffer.ChapterUrls = chapterUrls;
                        Console.WriteLine($"Got chapter urls, total: {chapterUrls.Count}");
                        break;

                    case Attr.FirstChapterUrl:
                        //TODO: Implement
                        break;

                    case Attr.LatestChapter:
                        var latestChapterNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.LatestChapterLink);
                        if (latestChapterNode == null)
                        {
                            return;
                        }
                        if (latestChapterNode?.Attributes["href"] != null) //chapter url is most likely on this page
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

    public class ScraperData
    {
        public SiteConfiguration? SiteConfig { get; set; }
        public Uri? SiteTableOfContents { get; set; }
        public Uri? BaseUri { get; set; }
    }

    public abstract class ScraperStrategy
    {
        protected ScraperData _scraperData = new ScraperData();
        private int ConcurrentRequestsLimit { get; set; } = 2;

        public const int MaxRetries = 6;
        public const int MinimumParagraphThreshold = 5;
        protected const int TotalPossiblePaginationTabs = 6;
        protected static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        protected static readonly NovelScraperSettings _settings = new NovelScraperSettings();
        protected static readonly HttpClient _client = new HttpClient(); // better to keep one instance through the life of the method
        private SemaphoreSlim _semaphoreSlim; // limit the number of concurrent requests, prevent posssible rate limiting
        private static readonly List<string> _userAgents = new List<string>
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36",
            "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36",
            "Mozilla/5.0 (Windows NT 5.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36",
            "Mozilla/5.0 (Windows NT 6.2; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36"
        };
        private static int _userAgentIndex = 0;

        public abstract Task<NovelDataBuffer> ScrapeAsync();
        public abstract NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument);
        public virtual Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
        {
            // this method should always be overridden, I just needed a default implementation so other Strategies would not require it
            return Task.Run(() => FetchNovelDataFromTableOfContents(htmlDocument));
        }

        public void SetVariables(SiteConfiguration siteConfig, Uri siteTableOfContents, Configuration configuration)
        {
            SetSiteConfiguration(siteConfig);
            SetSiteTableOfContents(siteTableOfContents);
            SetConcurrentRequestLimit(configuration.ConcurrencyLimit);
            SetSemaphoreLimit(this.ConcurrentRequestsLimit);
        }

        public SiteConfiguration GetSiteConfiguration()
        {
            return _scraperData.SiteConfig ?? throw new NullReferenceException("SiteConfiguration is null");
        }

        public async Task<HtmlDocument> LoadHtmlPublicAsync(Uri uri)
        {
            return await LoadHtmlAsync(uri);
        }

        protected static async Task<HtmlDocument> LoadHtmlAsync(Uri uri)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<HtmlDocument>(htmlDoc => htmlDoc == null) // Retry if the result is null
                .WaitAndRetryAsync(MaxRetries, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // exponential back-off
                    (outcome, timeSpan, retryCount, context) =>
                    {
                        if (outcome.Exception != null)
                        {
                            // Log the exception details here
                            Logger.Warn($"Error occurred while navigating to {uri}. Attempt: {retryCount}");
                        }
                        else
                        {
                            // Log that the chapter is being skipped
                            Logger.Warn($"Skipping chapter due to repeated failures: {uri}. Attempt: {retryCount}");
                        }
                    });

            return await retryPolicy.ExecuteAsync(async context =>
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                var userAgent = _userAgents[++_userAgentIndex % _userAgents.Count];
                requestMessage.Headers.Add("User-Agent", userAgent);
                requestMessage.Options.Set(new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"), TimeSpan.FromSeconds(10));
                Logger.Debug($"Sending request to {uri}");
                var response = await _client.SendAsync(requestMessage);
                Logger.Info($"Response status code: {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.TooManyRequests && (int)context["RetryCount"] >= MaxRetries)
                    {
                        // Skip this chapter and return null
                        return null;
                    }
                    throw new HttpRequestException($"Failed to load HTML document from {uri} after {MaxRetries} attempts. Status code: {response.StatusCode}");
                }
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(content);
                return htmlDocument;
            }, new Context { ["RetryCount"] = 0 });
        }

        protected static async Task<string> DownloadImageAsync(Uri uri, string tempImageDirectory)
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
                    var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                    var userAgent = _userAgents[++_userAgentIndex % _userAgents.Count];
                    requestMessage.Headers.Add("User-Agent", userAgent);
                    var imageStream = await _client.GetStreamAsync(uri);
                    var imagePath = Path.Combine(tempImageDirectory, Path.GetRandomFileName() + ".jpg");
                    using (var fileStream = File.Create(imagePath))
                    {
                        await imageStream.CopyToAsync(fileStream);
                        Logger.Info($"Downloaded image to temp folder {imagePath}");
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


        protected virtual Uri TrimLastUriSegment(Uri siteUri)
        {
            string allSegementsButLast = siteUri.Segments.Take(siteUri.Segments.Length - 1).Aggregate(
                (segment1, segment2) => segment1 + segment2);
            return new Uri(_scraperData.BaseUri, allSegementsButLast);
        }

        protected void SetBaseUri(Uri siteUri)
        {
            if (siteUri == null)
            {
                Logger.Error($"siteUri, which is the url that was provided by the user is null.");
                throw new ArgumentNullException(nameof(siteUri));
            }
            _scraperData.BaseUri = new Uri(siteUri.GetLeftPart(UriPartial.Authority));
        }

        /// <summary>
        /// Gets the chapter urls from the table of contents page that requires pagination to get chapters.
        /// </summary>
        /// <param name="tableOfContentUri"></param>
        /// <param name="getAllChapters"></param>
        /// <param name="pageToStopAt"></param>
        /// <param name="pageToStartAt"></param>
        /// <returns>Returns a ValueTuple that contains all the chapter urls and the url for the last page of the table of contents</returns>
        protected virtual async Task<(List<string> ChapterUrls, string LastTableOfContentsUrl)> GetPaginatedChapterUrlsAsync(Uri tableOfContentUri, bool getAllChapters, int pageToStopAt, int pageToStartAt = 1)
        {
            var chapterUrls = new List<string>();

            var baseTableOfContentUrl = tableOfContentUri + _scraperData.SiteConfig?.PaginationType;
            var lastTableOfContentsUrl = string.Format(baseTableOfContentUrl, pageToStopAt);

            for (int i = pageToStartAt; i <= pageToStopAt; ++i)
            {
                var tableOfContentUrl = string.Format(baseTableOfContentUrl, i);
                bool isPageNew = i > pageToStartAt;
                try
                {
                    Logger.Info($"Navigating to {tableOfContentUrl}");
                    var htmlDocument = await LoadHtmlAsync(new Uri(tableOfContentUrl));

                    List<string> chapterUrlsOnContentPage = GetChapterUrlsInRange(htmlDocument, _scraperData.BaseUri, 1);
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

            return (chapterUrls, lastTableOfContentsUrl);
        }

        protected virtual async Task<List<string>> GetChapterUrls(Uri tableOfContentUri, bool getAllChapters, int pageToStopAt, int pageToStartAt = 1)
        {
            List<string> chapterUrls = new List<string>();
            string baseTableOfContentUrl = tableOfContentUri + _scraperData.SiteConfig?.PaginationType;

            for (int i = pageToStartAt; i <= pageToStopAt; i++)
            {
                string tableOfContentUrl = string.Format(baseTableOfContentUrl, i);
                bool isPageNew = i > pageToStartAt;
                try
                {
                    Logger.Info($"Navigating to {tableOfContentUrl}");
                    HtmlDocument htmlDocument = await LoadHtmlAsync(new Uri(tableOfContentUrl));

                    List<string> chapterUrlsOnContentPage = GetChapterUrlsInRange(htmlDocument, _scraperData.BaseUri, 1);
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
            try
            {
                Logger.Info("Getting chapters data");
                var tasks = new List<Task<ChapterDataBuffer>>();
                var chapterDataBuffers = new List<ChapterDataBuffer>();

                if (_scraperData.SiteConfig.HasImagesForChapterContent)
                {
                    Logger.Info("Using Selenium to get chapters data");
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
                        Logger.Info("Disposing all drivers");
                        Console.WriteLine($"Total drivers: {driverFactory.GetAllDrivers().Count}");
                        driverFactory.DisposeAllDrivers();
                        Logger.Info("Finished disposing all drivers");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error while getting chapters data. {ex}");
                        if (Directory.Exists(tempImageDirectory))
                        {
                            Directory.Delete(tempImageDirectory, true);
                            Logger.Info("Finished deleting temp image directory");
                        }
                        else
                        {
                            Logger.Warn($"Was unable to find temp directory {tempImageDirectory}. Please verify it was deleted successfully");
                        }
                        throw;
                    }
                    finally
                    {
                        Logger.Info("Closing driver");
                        driverFactory.DisposeAllDrivers();
                        Logger.Info("Finished closing driver");
                    }

                }
                else
                {
                    Logger.Info("Using HttpClient to get chapters data");
                    foreach (var url in chapterUrls)
                    {
                        await _semaphoreSlim.WaitAsync();
                        tasks.Add(GetChapterDataAsync(url));
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

        protected virtual List<string> GetChapterUrlsInRange(HtmlDocument htmlDocument, Uri baseSiteUri, int? startChapter = null, int? endChapter = null)
        {
            Logger.Info($"Getting chapter urls from table of contents");
            try
            {
                HtmlNodeCollection chapterLinks = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.ChapterLinks);

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

        #region Private Methods
        private void SetSiteConfiguration(SiteConfiguration siteConfig)
        {
            _scraperData.SiteConfig = siteConfig;
        }

        private void SetSiteTableOfContents(Uri siteTableOfContents)
        {
            _scraperData.SiteTableOfContents = siteTableOfContents;
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

            Logger.Info($"Navigating to {urls}");
            driver.Navigate().GoToUrl(urls);
            try
            {
                Logger.Info($"Waiting for images on page {urls} to load.");
                WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
                wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(_scraperData.SiteConfig?.Selectors.ChapterContent)));
                Logger.Info("Images have been loaded.");
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


            HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(_scraperData.SiteConfig?.Selectors.ChapterTitle);
            chapterDataBuffer.Title = titleNode.InnerText.Trim() ?? uriLastSegment;
            Logger.Debug($"Chapter title: {chapterDataBuffer.Title}");

            HtmlNodeCollection pageUrlNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.ChapterContent);
            var pageUrls = pageUrlNodes.Select(pageUrl => pageUrl.Attributes[_scraperData.SiteConfig?.Selectors?.ChapterContentImageUrlAttribute].Value);
            bool isValidHttpUrls = pageUrls.Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)).All(value => value);

            if (!isValidHttpUrls)
            {
                Logger.Error("Invalid page urls");
                return chapterDataBuffer;
            }

            chapterDataBuffer.Pages = new List<PageData>();
            foreach (var url in pageUrls)
            {
                Logger.Info($"Getting page image from {url}");
                stopwatch.Reset();
                var imagePath = await DownloadImageAsync(new Uri(url), tempImageDirectory);
                Logger.Info($"Finished getting page image from {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
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
                Logger.Info($"Navigating to {url}");
                var htmlDocument = await LoadHtmlAsync(new Uri(url));
                Logger.Info($"Finished navigating to {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
                stopwatch.Restart();

                HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(_scraperData.SiteConfig?.Selectors.ChapterTitle);
                chapterDataBuffer.Title = titleNode.InnerText.Trim();
                Logger.Debug($"Chapter title: {chapterDataBuffer.Title}");

                HtmlNodeCollection paragraphNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.ChapterContent);
                var paragraphs = paragraphNodes.Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList();

                if (paragraphs.Count < MinimumParagraphThreshold)
                {
                    Logger.Warn($"Paragraphs count is less than 5. Trying alternative selector");
                    HtmlNodeCollection alternateParagraphNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.AlternativeChapterContent);
                    List<string> alternateParagraphs = alternateParagraphNodes.Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList();
                    Logger.Info($"Alternate paragraphs count: {alternateParagraphs.Count}");

                    if (alternateParagraphs.Count > paragraphs.Count)
                    {
                        Logger.Info($"Alternate paragraphs count is greater than paragraphs count. Using alternate paragraphs");
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
