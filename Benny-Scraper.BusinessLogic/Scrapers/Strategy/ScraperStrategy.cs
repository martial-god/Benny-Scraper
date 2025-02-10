using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;
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
using PuppeteerSharp;
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
                NovelStatus,
                ThumbnailUrl,
                LastTableOfContentsPage,
                ChapterUrls,
                FirstChapterUrl,
                CurrentChapter,
                AlternateLastTableOfContentsPage
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
                        Console.WriteLine($"Description line count: {novelDataBuffer.Description.Count}");
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

                    case Attr.NovelStatus:
                        var statusNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelStatus);
                        novelDataBuffer.NovelStatus = statusNode.InnerText.Trim();
                        novelDataBuffer.IsNovelCompleted = novelDataBuffer.NovelStatus.ToLowerInvariant().Contains(scraperData.SiteConfig.CompletedStatus);
                        Console.WriteLine($"NovelStatus: {novelDataBuffer.NovelStatus}");
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
                        try
                        {
                            var lastTableOfContentsPageNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.LastTableOfContentsPage);
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

                    case Attr.CurrentChapter:
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
        private IPuppeteerDriverService? PuppeteerDriverService { get; }
        protected virtual bool RequiresBrowser => false;
        protected ScraperData _scraperData = new ScraperData();
        private int ConcurrentRequestsLimit { get; set; } = 2;

        private const int MaxRetries = 6;
        private const int MinimumParagraphThreshold = 5;
        protected const int TotalPossiblePaginationTabs = 6;
        protected static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        protected static readonly NovelScraperSettings Settings = new NovelScraperSettings();
        private static readonly HttpClient Client = new HttpClient(); // better to keep one instance through the life of the method
        private SemaphoreSlim _httpSemaphore; // limit the number of concurrent requests, prevent posssible rate limiting
        private SemaphoreSlim _puppeteerSemaphore = new SemaphoreSlim(2);
        private static readonly List<string> UserAgents = new List<string>
        {
            "Other", // found at https://stackoverflow.com/questions/62402504/c-sharp-httpclient-postasync-403-forbidden-with-ssl
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36",
            "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36",
            "Mozilla/5.0 (Windows NT 5.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36",
            "Mozilla/5.0 (Windows NT 6.2; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36"
        };
        private static int _userAgentIndex = 0;

        protected ScraperStrategy(IPuppeteerDriverService? puppeteerDriverService = null)
        {
            if (RequiresBrowser && puppeteerDriverService is null)
                throw new ArgumentNullException(nameof(puppeteerDriverService));
            
            PuppeteerDriverService = puppeteerDriverService;
        }

        public abstract Task<NovelDataBuffer> ScrapeAsync();
        /// <summary>
        /// This method is what is used to get the novel data from the table of contents page. i.e. Description, Chapters, Title, Novel Status.
        /// </summary>
        /// <param name="htmlDocument"></param>
        /// <returns></returns>
        /// 
        protected abstract NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument);
        /// <summary>
        /// This method is what is used to get the novel data from the table of contents page. i.e. Description, Chapters, Title, Novel Status.
        /// </summary>
        /// <param name="htmlDocument"></param>
        /// <returns></returns>
        /// 
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
            SetSemaphoreLimit(this.ConcurrentRequestsLimit);
        }

        public SiteConfiguration GetSiteConfiguration()
        {
            return _scraperData.SiteConfig ?? throw new NullReferenceException("SiteConfiguration is null");
        }

        /// <summary>
        /// This should really be called after a page has been created
        /// </summary>
        /// <returns></returns>
        public IPage? GetCurrentPuppeteerPage()
        {
            return PuppeteerDriverService?.GetCurrentPage();
        }

        public Task<IPage> CreateNewPuppeteerBrowserWithPageAsync(Uri uri, bool headless = true)
        {
            return PuppeteerDriverService.CreatePageAsync(uri, headless);
        }

        public async Task<(HtmlDocument document, Uri updatedUri)> LoadHtmlPublicAsync(Uri uri)
        {
            return await LoadHtmlAsync(uri);
        }

        protected async Task<(HtmlDocument document, Uri updatedUri)> LoadHtmlAsync(Uri uri)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .OrResult<(HtmlDocument, Uri)>(result => result is { Item1: null}) // Retry if the result is null
                .WaitAndRetryAsync(MaxRetries, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // exponential back-off
                    (outcome, timeSpan, retryCount, context) =>
                    {
                        // Log the exception details here
                        Logger.Warn(outcome.Exception != null
                            ? $"Error occurred while navigating to {uri}. Attempt: {retryCount}"
                            // Log that the chapter is being skipped
                            : $"Skipping chapter due to repeated failures: {uri}. Attempt: {retryCount}");
                    });

            return await retryPolicy.ExecuteAsync(async context =>
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                string userAgent = UserAgents[++_userAgentIndex % UserAgents.Count];

                requestMessage.Headers.Add("User-Agent", userAgent);
                requestMessage.Options.Set(new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"), TimeSpan.FromSeconds(10));
                Logger.Debug($"Sending request to {uri}");
                var response = await Client.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.TooManyRequests && (int)context["RetryCount"] >= MaxRetries)
                        return (null, uri);
                    throw new HttpRequestException($"Failed to load HTML document from {uri} after {MaxRetries} attempts. NovelStatus code: {response.StatusCode}");
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
                        uri = new Uri(canonicalUrl);  // Update the Uri for next request
                    }
                }

                return (htmlDocument, uri);
            }, new Context { ["RetryCount"] = 0 });
        }

        private static async Task<string> DownloadImageAsync(Uri uri, string tempImageDirectory)
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
                    var userAgent = UserAgents[++_userAgentIndex % UserAgents.Count];
                    requestMessage.Headers.Add("User-Agent", userAgent);
                    var imageStream = await Client.GetStreamAsync(uri);
                    var imagePath = Path.Combine(tempImageDirectory, Path.GetRandomFileName() + ".jpg");
                    await using var fileStream = File.Create(imagePath);
                    await imageStream.CopyToAsync(fileStream);
                    Logger.Debug($"Downloaded image to temp folder {imagePath}");
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

        protected static int GetPageNumberFromUrlQuery(string url, Uri baseUri)
        {
            Uri uriResult;
            bool result = Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uriResult);

            // if the url is relative, then combine it with the base uri
            if (uriResult != null && !uriResult.IsAbsoluteUri)
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
        /// <param name="page"></param>
        /// <returns>Returns a ValueTuple that contains all the chapter urls and the url for the last page of the table of contents</returns>
        protected virtual async Task<(List<string> ChapterUrls, string LastTableOfContentsUrl)> GetPaginatedChapterUrlsAsync(
            Uri tableOfContentUri,
            bool getAllChapters,
            int pageToStopAt,
            int pageToStartAt = 1,
            IPage? page = null)
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
                    HtmlDocument? htmlDocument = null;
                    Uri? uri = null;
                    Logger.Info($"Navigating to {tableOfContentUrl}");
                    switch (RequiresBrowser)
                    {
                        case true:
                            if (page is null)
                                throw new ArgumentNullException($"{nameof(page)} cannot be null when RequiresBrowser is true.");
                            await page.GoToAsync(tableOfContentUrl, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Load } });
                            htmlDocument = await PuppeteerDriverService.GetPageContentAsync(page);
                            break;
                        default:
                            (htmlDocument, uri) = await LoadHtmlAsync(new Uri(tableOfContentUrl));
                            break;
                    }

                    if (_scraperData.BaseUri != null)
                    {
                        List<string> chapterUrlsOnContentPage = GetChapterUrlsInRange(htmlDocument, _scraperData.BaseUri, 1);
                        if (chapterUrlsOnContentPage.Any())
                            chapterUrls.AddRange(chapterUrlsOnContentPage);
                    }

                    if (!getAllChapters && !isPageNew)
                        break;
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
                    (HtmlDocument htmlDocument, Uri uri) = await LoadHtmlAsync(new Uri(tableOfContentUrl));

                    if (_scraperData.BaseUri != null)
                    {
                        List<string> chapterUrlsOnContentPage = GetChapterUrlsInRange(htmlDocument, _scraperData.BaseUri, 1);
                        if (chapterUrlsOnContentPage.Any())
                            chapterUrls.AddRange(chapterUrlsOnContentPage);
                    }

                    if (!getAllChapters && !isPageNew)
                        break;
                }
                catch (HttpRequestException e)
                {
                    Logger.Error($"Error occurred while navigating to {tableOfContentUrl}. Error: {e}");
                }
            }

            return chapterUrls;
        }

        public virtual async Task<List<ChapterDataBuffer>> GetChaptersDataAsync(List<string> chapterUrls, IPage? page = null)
        {
            var tempImageDirectory = string.Empty;
            int sequenceNumber = 1;
            var tasks = new List<Task<ChapterDataBuffer>>();
            var chapterDataBuffers = new List<ChapterDataBuffer>();
            
            try
            {
                Logger.Info("Getting chapters data");

                // property pattern https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#property-pattern
                if (_scraperData.SiteConfig is { HasImagesForChapterContent: true } && RequiresBrowser)
                {
                    if (page is null)
                        throw new ArgumentNullException(
                            $"{nameof(page)} cannot be null when RequiresBrowser is true. Method {nameof(GetChaptersDataAsync)} is not allowed.}}");
                    await GetChaptersWithImagesAsync(chapterUrls, chapterDataBuffers, tasks, page, tempImageDirectory);
                    
                    chapterDataBuffers.ForEach(chapterDataBuffer => chapterDataBuffer.SequenceNumber = sequenceNumber++);
                    return chapterDataBuffers;
                }
                
                Logger.Debug("Using HttpClient to get chapters data");
                foreach (var url in chapterUrls)
                {
                    if (page is not null && RequiresBrowser)
                    {
                        await _puppeteerSemaphore.WaitAsync();
                        try
                        {
                            // process one chapter at time. Still need to clean this up
                            await Task.Delay(Random.Shared.Next(100, 3000));
                            var chapterData = await GetChapterDataAsync(url, page);
                            chapterDataBuffers.Add(chapterData);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error occurred while getting chapters data from {url}. Error: {ex}");
                        }
                        finally
                        {
                            _httpSemaphore.Release();
                        }
                    }
                    else
                    {
                        await _httpSemaphore.WaitAsync();
                        try
                        {
                            tasks.Add(GetChapterDataAsync(url, null));
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error occurred while getting chapter data from {url}. Error: {ex}");
                        }
                        finally
                        {
                            _httpSemaphore.Release();
                        }
                    }
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
            finally
            {
                PuppeteerDriverService?.Dispose();
            }
        }
        
        // TODO: Figure out whether or not to use this. It works great for Puppeteer sites but does not handle parallel request, pretty slow.
        public virtual async Task<List<ChapterDataBuffer>> GetChapterssDataAsync(List<string> chapterUrls, IPage? page = null)
        {
            var chapterDataBuffers = new List<ChapterDataBuffer>();
            int sequenceNumber = 1;
            Logger.Info("Getting chapters data");

            foreach (var url in chapterUrls)
            {
                if (page != null && RequiresBrowser)
                {
                    await _puppeteerSemaphore.WaitAsync();
                    try
                    {
                        // We only acquire once here, but inside the call we handle retries
                        var chapter = await GetChapterDataWithRetryAsync(url, page);
                        chapter.SequenceNumber = sequenceNumber++;
                        chapterDataBuffers.Add(chapter);
                    }
                    finally
                    {
                        _puppeteerSemaphore.Release();
                    }
                }
                else
                {
                    await _httpSemaphore.WaitAsync();
                    try
                    {
                        var chapter = await GetChapterDataWithRetryAsync(url, null);
                        chapterDataBuffers.Add(chapter);
                    }
                    finally
                    {
                        _httpSemaphore.Release();
                    }
                }
            }

            return chapterDataBuffers;
        }
        
        private async Task<ChapterDataBuffer> GetChapterDataWithRetryAsync(string url, IPage? page, int maxRetries = MaxRetries, double initialDelaySeconds = 5.0)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var chapterData = await GetChapterDataNoSemaphoreAsync(url, page);
                    return chapterData;
                }
                catch (Exception)
                {
                    if (attempt == maxRetries)
                    {
                        Logger.Error($"All {maxRetries} attempts failed for {url}.");
                        return new ChapterDataBuffer
                        {
                            Title = "Failed to retrieve content",
                            Content = "No content found",
                            Url = url,
                            DateLastModified = DateTime.Now
                        };
                    }
                    var baseDelay = initialDelaySeconds * Math.Pow(2, attempt - 1);
                    var jitterFactor = 0.5 + Random.Shared.NextDouble(); // 0.5x–1.5x
                    var finalDelay = baseDelay * jitterFactor * 1000;
            
                    Logger.Warn($"Attempt {attempt} failed for {url}. Will retry in {finalDelay}s.");
                    await Task.Delay((int)finalDelay);
                }
            }
            // Should never actually get here
            return new ChapterDataBuffer { Url = url };
        }

        private async Task<ChapterDataBuffer> GetChapterDataNoSemaphoreAsync(string url, IPage? page)
        {
            var chapterDataBuffer = new ChapterDataBuffer();
            var stopwatch = Stopwatch.StartNew();

            if (page != null && RequiresBrowser)
            {
                await page.GoToAsync(url, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Load },
                    Timeout = 10000 
                });
                HtmlDocument html = await PuppeteerDriverService.GetPageContentAsync(page);
                ParseHtmlIntoChapterBuffer(html, chapterDataBuffer);
            }
            else
            {
                (HtmlDocument html, Uri uri) = await LoadHtmlAsync(new Uri(url));
                ParseHtmlIntoChapterBuffer(html, chapterDataBuffer);
            }

            Logger.Info($"Finished processing chapter data from {url}. Time taken: {stopwatch.ElapsedMilliseconds} ms");
            return chapterDataBuffer;
        }

        private void  ParseHtmlIntoChapterBuffer(HtmlDocument htmlDocument, ChapterDataBuffer chapterDataBuffer)
        {
            var titleNode = htmlDocument?.DocumentNode?.SelectSingleNode(_scraperData.SiteConfig?.Selectors.ChapterTitle);
            if (titleNode == null)
                throw new Exception("Title node missing – likely partial page or blocked.");

            chapterDataBuffer.Title = titleNode.InnerText.Trim();

            var paragraphNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.ChapterContent)
                                 ?? throw new Exception("Paragraph nodes missing.");
            var paragraphs = paragraphNodes
                .Select(p => HtmlEntity.DeEntitize(p.InnerText.Trim()))
                .ToList();

            if (paragraphs.Count < MinimumParagraphThreshold)
            {
                Logger.Warn($"Paragraphs count is less than 5. Trying alternative selector");
                HtmlNodeCollection alternateParagraphNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.AlternativeChapterContent);
                List<string> alternateParagraphs = alternateParagraphNodes.Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList();
                Logger.Debug($"Alternate paragraphs count: {alternateParagraphs.Count}");

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
                // Logger.Debug($"No content found for {url}");
                chapterDataBuffer.Content = "No content found";
            }
        }


        private async Task GetChaptersWithImagesAsync(
            List<string> chapterUrls,
            List<ChapterDataBuffer> chapterDataBuffers,
            List<Task<ChapterDataBuffer>> tasks,
            IPage page,
            string tempImageDirectory)
        {
            Logger.Debug("Using Puppeteer to get chapters data");
            await page.GoToAsync(chapterUrls.First(), new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Load } });

            tempImageDirectory = CommonHelper.CreateTempDirectory();

            foreach (var url in chapterUrls)
            {
                tasks.Add(GetChapterDataAsync(page, url, tempImageDirectory));
            }
            try
            {
                var taskResults = await Task.WhenAll(tasks);
                chapterDataBuffers.AddRange(taskResults);
                Logger.Info($"Finished getting chapters data. Total chapters: {chapterDataBuffers.Count}");
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
        }

        protected virtual List<string> GetChapterUrlsInRange(
            HtmlDocument htmlDocument,
            Uri baseSiteUri,
            int? startChapter = null,
            int? endChapter = null)
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

        private void SetSemaphoreLimit(int concurrentRequestLimit)
        {
            _httpSemaphore = new SemaphoreSlim(concurrentRequestLimit);
        }

        private async Task<ChapterDataBuffer> GetChapterDataAsync(IPage page, string singleUrl, string tempImageDirectory)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var uriLastSegment = new Uri(singleUrl).Segments.Last();

            var chapterDataBuffer = new ChapterDataBuffer()
            {
                TempDirectory = tempImageDirectory,
                Url = singleUrl
            };

            Logger.Debug($"Navigating to {singleUrl}");
            try
            {
                Logger.Debug($"Waiting for images on page {singleUrl} to load.");
                await page.GoToAsync(singleUrl, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Load } });
                Logger.Debug("Images have been loaded.");
            }
            catch (PuppeteerException ex)
            {
                Logger.Error($"Timeout while waiting for elements on page {singleUrl}: {ex.Message}");
                chapterDataBuffer.Title = uriLastSegment;
                return chapterDataBuffer;
            }

            Logger.Info($"Finished navigating to {singleUrl} Time taken: {stopwatch.ElapsedMilliseconds} ms");
            var htmlDocument = await PuppeteerDriverService.GetPageContentAsync(page);

            HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(_scraperData.SiteConfig?.Selectors.ChapterTitle);
            chapterDataBuffer.Title = titleNode.InnerText.Trim() ?? uriLastSegment;
            Logger.Debug($"Chapter title: {chapterDataBuffer.Title}");

            HtmlNodeCollection pageUrlNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.ChapterContent);
            var pageUrls = pageUrlNodes.Select(pageUrl => pageUrl.Attributes[_scraperData.SiteConfig?.Selectors?.ChapterContentImageUrlAttribute].Value).ToList();
            bool isValidHttpUrls = pageUrls.Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)).All(value => value);

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

        private async Task<ChapterDataBuffer> GetChapterDataAsync(string url, IPage? page = null)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            HtmlDocument? htmlDocument;
            Uri? uri;

            var chapterDataBuffer = new ChapterDataBuffer();

            try
            {
                Logger.Debug($"Navigating to {url}");
                switch (RequiresBrowser)
                {
                    case true:
                        {
                            if (page is null)
                                throw new ArgumentNullException($"{nameof(page)} cannot be null when {nameof(RequiresBrowser)} is true");
                            await page.GoToAsync(url, new NavigationOptions
                            {
                                WaitUntil = new[] { WaitUntilNavigation.Load },
                                Timeout = 60000
                            });
                            htmlDocument = await PuppeteerWithRetryAsync(page, url, maxRetries: 4);
                            if (htmlDocument is null)
                            {
                                Logger.Error($"Failed to retrieve Puppeteer content after retries: {url}");
                                chapterDataBuffer.Title = "Failed to retrieve content";
                                chapterDataBuffer.Content = "No content found";
                                return chapterDataBuffer;
                            }
                        }
                        break;
                    default:
                        (htmlDocument, uri) = await LoadHtmlAsync(new Uri(url));
                        break;
                }
                Logger.Info($"Finished navigating to {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
                stopwatch.Restart();

                HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(_scraperData.SiteConfig?.Selectors.ChapterTitle);
                chapterDataBuffer.Title = titleNode.InnerText.Trim();
                Logger.Info($"Chapter title: {chapterDataBuffer.Title}");

                HtmlNodeCollection paragraphNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.ChapterContent);
                var paragraphs = paragraphNodes.Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList();

                if (paragraphs.Count < MinimumParagraphThreshold)
                {
                    Logger.Warn($"Paragraphs count is less than 5. Trying alternative selector");
                    HtmlNodeCollection alternateParagraphNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.AlternativeChapterContent);
                    List<string> alternateParagraphs = alternateParagraphNodes.Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList();
                    Logger.Debug($"Alternate paragraphs count: {alternateParagraphs.Count}");

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
                _httpSemaphore.Release();
                _puppeteerSemaphore.Release();
                Logger.Info($"Semaphore released");
            }

            return chapterDataBuffer;
        }
        
        private async Task<HtmlDocument?> PuppeteerWithRetryAsync(IPage page, string url, int maxRetries = 3)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await page.GoToAsync(url, new NavigationOptions
                    {
                        WaitUntil = new[] { WaitUntilNavigation.Load },
                        Timeout = 30000 // 1 minute or your choice
                    });

                    // If navigation succeeds, parse the DOM
                    var htmlDoc = await PuppeteerDriverService.GetPageContentAsync(page);
                    return htmlDoc;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Puppeteer attempt {attempt} failed for {url}: {ex.Message}");
                    if (attempt == maxRetries)
                    {
                        Logger.Error($"All {maxRetries} Puppeteer attempts failed for {url}.");
                        return null;
                    }

                    // Backoff before next attempt
                    // e.g., 2s, 4s, 6s, or exponential with random jitter
                    int delayMs = 2000 * attempt; 
                    await Task.Delay(delayMs);
                }
            }

            return null; // Should never reach here
        }

        #endregion

    }
}
