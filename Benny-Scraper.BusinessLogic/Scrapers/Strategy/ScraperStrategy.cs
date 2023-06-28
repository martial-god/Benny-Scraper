using AngleSharp.Dom;
using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    namespace Impl
    {
        //The NovelDataInitializer represents an abstraction around populating a NovelData object with its required content.
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
                //TODO: 'Status' is ambiguous. Should this attribute and the corresponding data member of NovelData be renamed
                //  to clarify it's purpose?
                Status,
                ThumbnailUrl,
                LastTableOfContentsPage,
                ChapterUrls,
                FirstChapterUrl,
                LatestChapter
            }

            protected static void FetchContentByAttribute(Attr attr, NovelData novelData, HtmlDocument htmlDocument, ScraperData scraperData)
            {
                switch (attr)
                {
                    case Attr.Title:
                        var titleNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelTitle);
                        if (titleNodes.Any())
                        {
                            novelData.Title = titleNodes != null ? HtmlEntity.DeEntitize(titleNodes.First().InnerText.Trim()) : string.Empty;
                        }
                        break;

                    case Attr.Author:
                        var authorNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelAuthor);
                        novelData.Author = authorNode != null ? HtmlEntity.DeEntitize(authorNode.InnerText.Trim()) : string.Empty;
                        break;

                    case Attr.Category:
                        //TODO: Implement
                        break;

                    case Attr.NovelRating:
                        var novelRatingNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelRating);
                        novelData.Rating = double.Parse(novelRatingNode.InnerText.Trim());
                        break;

                    case Attr.TotalRatings:
                        var totalRatingsNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.TotalRatings);
                        novelData.TotalRatings = int.Parse(totalRatingsNode.InnerText.Trim());
                        break;

                    case Attr.Description:
                        var descriptionNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelDescription);
                        novelData.Description = descriptionNodes.Select(description => HtmlEntity.DeEntitize(description.InnerText.Trim())).ToList();
                        break;

                    case Attr.Genres:
                        var genreNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelGenres);
                        novelData.Genres = genreNodes.Select(genre => HtmlEntity.DeEntitize(genre.InnerText.Trim())).ToList();
                        break;

                    case Attr.AlternativeNames:
                        var alternateNameNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.NovelAlternativeNames);
                        List<string> alternateNames = alternateNameNodes.Select(alternateName => HtmlEntity.DeEntitize(alternateName.InnerText.Trim())).ToList();
                        if (alternateNames.Any())
                        {
                            // SelectMany flattens a list of lists into a single list.
                            novelData.AlternativeNames = alternateNames.SelectMany(altName => SplitByLanguage(altName)).ToList();
                        }
                        break;

                    case Attr.Status:
                        var statusNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelStatus);
                        novelData.NovelStatus = statusNode.InnerText.Trim();
                        novelData.IsNovelCompleted = novelData.NovelStatus.ToLower().Contains(scraperData.SiteConfig.CompletedStatus);
                        break;

                    case Attr.ThumbnailUrl:
                        var urlNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.NovelThumbnailUrl);
                        var url = urlNode.Attributes[scraperData.SiteConfig?.Selectors.ThumbnailUrlAttribute].Value;
                        bool isValidHttpUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                        Uri absoluteUri = isValidHttpUrl ? new Uri(url) : new Uri(scraperData.BaseUri, url);
                        using (var client = new HttpClient())
                        {
                            var thumbnailBytes = client.GetByteArrayAsync(absoluteUri).Result;
                            novelData.ThumbnailImage = thumbnailBytes;
                        }
                        novelData.ThumbnailUrl = url;
                        break;

                    case Attr.LastTableOfContentsPage:
                        var lastTableOfContentsPageNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig?.Selectors.LastTableOfContentsPage);
                        novelData.LastTableOfContentsPageUrl = lastTableOfContentsPageNode.Attributes["href"].Value;
                        break;

                    case Attr.ChapterUrls:
                        var chapterLinkNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig?.Selectors.ChapterLinks);
                        List<string> chapterUrls = chapterLinkNodes.Select(chapterLink => chapterLink.Attributes["href"].Value).ToList();
                        if (chapterUrls.Any() && !IsValidHttpUrl(chapterUrls.First()))
                        {
                            chapterUrls = chapterUrls.Select(chapterUrl => new Uri(scraperData.BaseUri, chapterUrl).ToString()).ToList();
                        }
                        novelData.ChapterUrls = chapterUrls;
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
                        var currentChapterUrl = latestChapterNode.Attributes["href"].Value;
                        if (!IsValidHttpUrl(currentChapterUrl))
                        {
                            currentChapterUrl = new Uri(scraperData.BaseUri, currentChapterUrl).ToString();
                        }
                        novelData.CurrentChapterUrl = currentChapterUrl;
                        novelData.MostRecentChapterTitle = HtmlEntity.DeEntitize(latestChapterNode.InnerText).Trim();
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
        public static int ConcurrentRequestsLimit { get; set; } = 2;

        public const int MaxRetries = 4;
        public const int MinimumParagraphThreshold = 5;
        protected const int TotalPossiblePaginationTabs = 6;
        protected static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        protected static readonly NovelScraperSettings _settings = new NovelScraperSettings();
        protected static readonly HttpClient _client = new HttpClient(); // better to keep one instance through the life of the method
        protected static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(ConcurrentRequestsLimit, ConcurrentRequestsLimit); // limit the number of concurrent requests, prevent posssible rate limiting
        private static readonly List<string> _userAgents = new List<string>
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36",
            "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36",
            "Mozilla/5.0 (Windows NT 5.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36",
            "Mozilla/5.0 (Windows NT 6.2; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.90 Safari/537.36"
        };
        private static int _userAgentIndex = 0;

        public abstract Task<NovelData> ScrapeAsync();
        public abstract NovelData FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument);
        public virtual Task<NovelData> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
        {
            // this method should always be overridden, I just needed a default implementation so other Strategies would not require it
            return Task.Run(() => FetchNovelDataFromTableOfContents(htmlDocument));
        }

        public void SetVariables(SiteConfiguration siteConfig, Uri siteTableOfContents)
        {
            SetSiteConfiguration(siteConfig);
            SetSiteTableOfContents(siteTableOfContents);
        }

        public async Task<HtmlDocument> LoadHtmlPublicAsync(Uri uri)
        {
            return await LoadHtmlAsync(uri);
        }


        protected static async Task<HtmlDocument> LoadHtmlAsync(Uri uri)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            int retryCount = 0;
            while (retryCount < MaxRetries)
            {
                try
                {
                    var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                    var userAgent = _userAgents[++_userAgentIndex % _userAgents.Count];
                    requestMessage.Headers.Add("User-Agent", userAgent); // some sites require a user agent to be set https://stackoverflow.com/questions/62402504/c-sharp-httpclient-postasync-403-forbidden-with-ssl
                    var response = await _client.SendAsync(requestMessage);
                    response.EnsureSuccessStatusCode(); // Throws an exception if the status code is not successful
                    var content = await response.Content.ReadAsStringAsync();

                    var htmlDocument = new HtmlDocument();
                    htmlDocument.LoadHtml(content);

                    return htmlDocument;
                }
                catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    retryCount++;
                    Logger.Error($"Error occurred while navigating to {uri}. Error: {e}. Attempt: {retryCount}");
                    await Task.Delay(3000);
                }
            }
            throw new HttpRequestException($"Failed to load HTML document from {uri} after {MaxRetries} attempts.");
        }

        protected static async Task<byte[]> LoadByteArrayAsync(Uri uri)
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
                    var response = await _client.SendAsync(requestMessage);
                    response.EnsureSuccessStatusCode();
                    var bytes = await response.Content.ReadAsByteArrayAsync();

                    return bytes;
                }
                catch (HttpRequestException e)
                {
                    retryCount++;
                    Logger.Error($"Error occurred while navigating to {uri}. Error: {e}. Attempt: {retryCount}");
                    await Task.Delay(3000);
                }
            }
            throw new HttpRequestException($"Failed to load byte array from {uri} after {MaxRetries} attempts.");
        }


        protected virtual Uri TrimLastUriSegment(Uri siteUri)
        {
            string allSegementsButLast = siteUri.Segments.Take(siteUri.Segments.Length - 1).Aggregate(
                (segment1, segment2) => segment1 + segment2);
            return new Uri(_scraperData.BaseUri, allSegementsButLast);
        }

        protected void SetBaseUri(Uri siteUri)
        {
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

        public virtual async Task<List<ChapterData>> GetChaptersDataAsync(List<string> chapterUrls)
        {
            try
            {
                Logger.Info("Getting chapters data");
                var tasks = new List<Task<ChapterData>>();
                var chapterData = new List<ChapterData>();

                if (_scraperData.SiteConfig.HasImagesForChapterContent)
                {
                    Logger.Info("Using Selenium to get chapters data");
                    IDriverFactory driverFactory = new DriverFactory();
                    var driver = await driverFactory.CreateDriverAsync(chapterUrls.First(), isHeadless: true);

                    if (_scraperData.SiteConfig.Name == "mangareaders") // element is visisble while not in headless mode
                    {
                        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
                        wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//*[@id=\"first-read\"]/div[1]/div/div[3]/a[1]")));
                        var chapterContent = driver.FindElement(By.XPath("//*[@id=\"first-read\"]/div[1]/div/div[3]/a[1]")); //element that decides orientation of pages
                        if (chapterContent != null)
                        {
                            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].click();", chapterContent);
                        }
                    }
                    
                    foreach (var url in chapterUrls)
                    {
                        tasks.Add(GetChapterDataAsync(driver, url));
                    }
                    try
                    {
                        var taskResults = await Task.WhenAll(tasks);
                        chapterData.AddRange(taskResults);
                        driver.Quit();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error while getting chapters data. {ex}");
                        throw;
                    }
                    finally
                    {
                        Logger.Info("Closing driver");
                        driver.Quit();
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
                        chapterData.AddRange(taskResults);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error while getting chapters data. {ex}");
                        throw;
                    }                   

                    Logger.Info("Finished getting chapters data");
                }
                return chapterData;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting chapters data. {ex}");
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

        public async Task<ChapterData> GetChapterDataThatHasImagesNoSeleniumAsync(string url)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var chapterData = new ChapterData();
            Logger.Info($"Navigating to {url}");
            var htmlDocument = await LoadHtmlAsync(new Uri(url));
            Logger.Info($"Finished navigating to {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart();

            try
            {
                HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(_scraperData.SiteConfig?.Selectors.ChapterTitle);
                chapterData.Title = titleNode.InnerText.Trim() ?? string.Empty;
                Logger.Debug($"Chapter title: {chapterData.Title}");

                HtmlNodeCollection pageUrlNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.ChapterContent);
                var pageUrls = pageUrlNodes.Select(pageUrl => pageUrl.Attributes["data-url"].Value);
                bool isValidHttpUrls = pageUrls.Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)).All(value => value);

                if (!isValidHttpUrls)
                {
                    Logger.Error("Invalid page urls");
                    return chapterData;
                }

                chapterData.Pages = new List<PageData>();
                foreach (var pageUrl in pageUrls)
                {
                    Logger.Info($"Getting page image from {pageUrl}");
                    stopwatch.Reset();
                    var imageBytes = await LoadByteArrayAsync(new Uri(pageUrl));
                    Logger.Info($"Finished getting page image from {pageUrl} Time taken: {stopwatch.ElapsedMilliseconds} ms");
                    chapterData.Pages.Add(new PageData
                    {
                        Url = pageUrl,
                        Image = imageBytes
                    });
                }
                chapterData.Url = url;
                chapterData.DateLastModified = DateTime.Now;

                Logger.Info($"Finished processing chapter data. Time taken: {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                throw;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
            return chapterData;
        }

        private async Task<ChapterData> GetChapterDataAsync(IWebDriver driver, string urls)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var chapterData = new ChapterData();
            Logger.Info($"Navigating to {urls}");
            driver.Navigate().GoToUrl(urls);
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
            wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(_scraperData.SiteConfig?.Selectors.ChapterContent)));
            Logger.Info($"Finished navigating to {urls} Time taken: {stopwatch.ElapsedMilliseconds} ms");
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(driver.PageSource);
            

            HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(_scraperData.SiteConfig?.Selectors.ChapterTitle);
            chapterData.Title = titleNode.InnerText.Trim() ?? string.Empty;
            Logger.Debug($"Chapter title: {chapterData.Title}");

            HtmlNodeCollection pageUrlNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.ChapterContent);
            var pageUrls = pageUrlNodes.Select(pageUrl => pageUrl.Attributes["data-url"].Value);
            bool isValidHttpUrls = pageUrls.Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)).All(value => value);

            if (!isValidHttpUrls)
            {
                Logger.Error("Invalid page urls");
                return chapterData;
            }

            chapterData.Pages = new List<PageData>();
            foreach (var url in pageUrls)
            {
                Logger.Info($"Getting page image from {url}");
                stopwatch.Reset();
                var imageBytes = await LoadByteArrayAsync(new Uri(url));
                Logger.Info($"Finished getting page image from {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
                chapterData.Pages.Add(new PageData
                {
                    Url = url,
                    Image = imageBytes
                });
            }
            chapterData.Url = urls;

            return chapterData;
        }


        private async Task<ChapterData> GetChapterDataAsync(string url)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var chapterData = new ChapterData();
            Logger.Info($"Navigating to {url}");
            var htmlDocument = await LoadHtmlAsync(new Uri(url));
            Logger.Info($"Finished navigating to {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart();

            try
            {
                HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(_scraperData.SiteConfig?.Selectors.ChapterTitle);
                chapterData.Title = titleNode.InnerText.Trim();
                Logger.Debug($"Chapter title: {chapterData.Title}");

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

                chapterData.Content = string.Join("\n", paragraphs);
                int contentCount = chapterData.Content.Count(c => c == '\n');

                if (string.IsNullOrWhiteSpace(chapterData.Content) || contentCount < 5)
                {
                    Logger.Debug($"No content found found for {url}");
                    chapterData.Content = "No content found";
                }

                chapterData.Url = url;
                chapterData.DateLastModified = DateTime.Now;

                Logger.Info($"Finished processing chapter data. Time taken: {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                throw;
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            return chapterData;
        }
        #endregion

    }
}
