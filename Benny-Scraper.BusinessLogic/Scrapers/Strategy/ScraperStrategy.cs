using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using NLog;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Web;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    public abstract class ScraperStrategy
    {
        protected SiteConfiguration SiteConfig { get; private set; }
        protected Uri SiteTableOfContents { get; private set; }
        protected Uri BaseUri { get; private set; }

        public const int MaxRetries = 4 ;
        public const int MinimumParagraphThreshold = 5;
        protected const int TotalPossiblePaginationTabs = 6;
        protected const int ConCurrentRequestsLimit = 12;
        protected static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        protected static readonly HttpClient _client = new HttpClient(); // better to keep one instance through the life of the method
        protected static readonly SemaphoreSlim _semaphonreSlim = new SemaphoreSlim(ConCurrentRequestsLimit, ConCurrentRequestsLimit); // limit the number of concurrent requests, prevent posssible rate limiting
        
        public abstract Task<NovelData> ScrapeAsync();
        public abstract NovelData GetNovelDataFromTableOfContent(HtmlDocument htmlDocument);

        // create method that alls both SetSiteConfiguration and SEtSiteTableOfContents
        public void SetVariables(SiteConfiguration siteConfig, Uri siteTableOfContents)
        {
            SetSiteConfiguration(siteConfig);
            SetSiteTableOfContents(siteTableOfContents);
        }        

        public static async Task<HtmlDocument> LoadHtmlDocumentFromUrlAsync(Uri uri)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            int retryCount = 0;
            while (retryCount < MaxRetries)
            {
                try
                {
                    var response = await _client.GetAsync(uri);
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

        protected virtual Uri TrimLastUriSegment(Uri siteUri)
        {
            string allSegementsButLast = siteUri.Segments.Take(siteUri.Segments.Length - 1).Aggregate(
                (segment1, segment2) => segment1 + segment2);
            return new Uri(BaseUri, allSegementsButLast);
        }

        protected void SetBaseUri(Uri siteUri)
        {
            Uri baseUri = new Uri(siteUri.GetLeftPart(UriPartial.Authority));
            BaseUri = baseUri;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tableOfContentUri"></param>
        /// <param name="getAllChapters"></param>
        /// <param name="pageToStopAt"></param>
        /// <param name="pageToStartAt"></param>
        /// <returns>Returns a ValueTuple that contains all the chapter urls and the url for the last page of the table of contents</returns>
        protected virtual async Task<(List<string> ChapterUrls, string LastTableOfContentsUrl)> GetPaginatedChapterUrlsAsync(Uri tableOfContentUri, bool getAllChapters, int pageToStopAt, int pageToStartAt = 1)
        {
            List<string> chapterUrls = new List<string>();

            string baseTableOfContentUrl = tableOfContentUri + SiteConfig.PaginationType;

            string lastTableOfContentsUrl = string.Format(baseTableOfContentUrl, pageToStopAt);

            for (int i = pageToStartAt; i <= pageToStopAt; i++)
            {
                string tableOfContentUrl = string.Format(baseTableOfContentUrl, i);
                bool isPageNew = i > pageToStartAt;
                try
                {
                    Logger.Info($"Navigating to {tableOfContentUrl}");
                    HtmlDocument htmlDocument = await LoadHtmlDocumentFromUrlAsync(new Uri(tableOfContentUrl));

                    List<string> chapterUrlsOnContentPage = GetChapterUrlsInRange(htmlDocument, BaseUri, 1);
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
            string baseTableOfContentUrl = tableOfContentUri + SiteConfig.PaginationType;

            for (int i = pageToStartAt; i <= pageToStopAt; i++)
            {
                string tableOfContentUrl = string.Format(baseTableOfContentUrl, i);
                bool isPageNew = i > pageToStartAt;
                try
                {
                    Logger.Info($"Navigating to {tableOfContentUrl}");
                    HtmlDocument htmlDocument = await LoadHtmlDocumentFromUrlAsync(new Uri(tableOfContentUrl));

                    List<string> chapterUrlsOnContentPage = GetChapterUrlsInRange(htmlDocument, BaseUri, 1);
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
                List<Task<ChapterData>> tasks = new List<Task<ChapterData>>();
                foreach (var url in chapterUrls)
                {
                    await _semaphonreSlim.WaitAsync();
                    tasks.Add(GetChapterDataAsync(url));
                }

                ChapterData[] chapterData = await Task.WhenAll(tasks);
                Logger.Info("Finished getting chapters data");
                return chapterData.ToList();
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
                HtmlNodeCollection chapterLinks = htmlDocument.DocumentNode.SelectNodes(SiteConfig.Selectors.ChapterLinks);

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
        /// Decodes sites that uses HTML encoded characters like class="&#x70;&#x61;&#x67;&#x69;&#x6E;&#x61;&#x74;&#x69;
        /// </summary>
        /// <param name="htmlDocument"></param>
        /// <returns>Decoded HtmlDocument</returns>
        protected virtual HtmlDocument DecodeHtml(HtmlDocument htmlDocument)
        {
            Logger.Debug("Decoding HTML - Start");
            string decodedHtml = WebUtility.HtmlDecode(htmlDocument.DocumentNode.OuterHtml);
            if (decodedHtml == null)
            {
                Logger.Error("Decoded HTML was null");
                return htmlDocument;
            }

            Logger.Debug("Decoding HTML - End");
            HtmlDocument decodedHtmlDocument = new HtmlDocument();
            decodedHtmlDocument.LoadHtml(decodedHtml);
            Logger.Debug("Decoded HTML loaded into HtmlDocument");

            return decodedHtmlDocument;
        }


        #region Private Methods
        private void SetSiteConfiguration(SiteConfiguration siteConfig)
        {
            SiteConfig = siteConfig;
        }

        private void SetSiteTableOfContents(Uri siteTableOfContents)
        {
            SiteTableOfContents = siteTableOfContents;
        }

        private async Task<ChapterData> GetChapterDataAsync(string url)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            ChapterData chapterData = new ChapterData();
            Logger.Info($"Navigating to {url}");
            HtmlDocument htmlDocument = await LoadHtmlDocumentFromUrlAsync(new Uri(url));
            Logger.Info($"Finished navigating to {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart();

            try
            {
                HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.ChapterTitle);
                chapterData.Title = titleNode.InnerText.Trim();
                Logger.Debug($"Chapter title: {chapterData.Title}");

                HtmlNodeCollection paragraphNodes = htmlDocument.DocumentNode.SelectNodes(SiteConfig.Selectors.ChapterContent);
                List<string> paragraphs = paragraphNodes.Select(paragraph => paragraph.InnerText.Trim()).ToList();

                if (paragraphs.Count < MinimumParagraphThreshold)
                {
                    Logger.Warn($"Paragraphs count is less than 5. Trying alternative selector");
                    HtmlNodeCollection alternateParagraphNodes = htmlDocument.DocumentNode.SelectNodes(SiteConfig.Selectors.AlternativeChapterContent);
                    List<string> alternateParagraphs = alternateParagraphNodes.Select(paragraph => paragraph.InnerText.Trim()).ToList();
                    Logger.Info($"Alternate paragraphs count: {alternateParagraphs.Count}");

                    if (alternateParagraphs.Count > paragraphs.Count)
                    {
                        Logger.Info($"Alternate paragraphs count is greater than paragraphs count. Using alternate paragraphs");
                        paragraphs = alternateParagraphs;
                    }
                }

                Logger.Info($"Finished retrieving chapter content. Time taken: {stopwatch.ElapsedMilliseconds} ms");
                stopwatch.Restart();

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
            }
            finally
            {
                _semaphonreSlim.Release();
            }

            return chapterData;
        }
        #endregion

    }
}
