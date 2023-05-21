using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using NLog;
using System.Globalization;
using System.Net;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    public abstract class ScraperStrategy
    {
        protected SiteConfiguration SiteConfig { get; private set; }
        protected Uri SiteTableOfContents { get; private set; }
        protected Uri BaseUri { get; private set; }

        protected static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        protected static readonly HttpClient _client = new HttpClient(); // better to keep one instance through the life of the method
        protected static readonly SemaphoreSlim _semaphonreSlim = new SemaphoreSlim(7); // limit the number of concurrent requests, prevent posssible rate limiting
        
        public abstract Task<NovelData> ScrapeAsync();
        public abstract NovelData GetNovelDataFromTableOfContent(HtmlDocument htmlDocument);

        // create method that alls both SetSiteConfiguration and SEtSiteTableOfContents
        public void SetVariables(SiteConfiguration siteConfig, Uri siteTableOfContents)
        {
            SetSiteConfiguration(siteConfig);
            SetSiteTableOfContents(siteTableOfContents);
        }

        private void SetSiteConfiguration(SiteConfiguration siteConfig)
        {
            SiteConfig = siteConfig;
        }

        private void SetSiteTableOfContents(Uri siteTableOfContents)
        {
            SiteTableOfContents = siteTableOfContents;
        }

        protected virtual int GetLastTableOfContentsPageNumber(HtmlDocument htmlDocument)
        {
            Logger.Info($"Getting last table of contents page number at {SiteConfig.Selectors.LastTableOfContentsPage}");
            try
            {
                HtmlNode lastPageNode = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.LastTableOfContentsPage);
                string lastPage = lastPageNode.Attributes[SiteConfig.Selectors.LastTableOfContentPageNumberAttribute].Value;

                int lastPageNumber = int.Parse(lastPage, NumberStyles.AllowThousands);

                if (SiteConfig.PageOffSet > 0)
                {
                    lastPageNumber += SiteConfig.PageOffSet;
                }

                Logger.Info($"Last table of contents page number is {lastPage}");
                return lastPageNumber;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error when getting last page table of contents page number. {ex}");
                throw;
            }
        }

        protected static async Task<HtmlDocument> LoadHtmlDocumentFromUrlAsync(Uri uri)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            var response = await _client.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);
            return htmlDocument;
        }
        protected virtual Uri GetAlternateTableOfContentsPageUri(Uri siteUri)
        {
            Uri baseUri = new Uri(siteUri.GetLeftPart(UriPartial.Authority));
            BaseUri = baseUri;
            string allSegementsButLast = siteUri.Segments.Take(siteUri.Segments.Length - 1).Aggregate(
                (segment1, segmenet2) => segment1 + segmenet2);
            return new Uri(baseUri, allSegementsButLast);
        }

        protected async Task<NovelData> RequestPaginatedDataAsync(Uri tableOfContentUri, bool getAllChapters, int pageToStopAt, int pageToStartAt = 1)
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

            HtmlDocument html = await LoadHtmlDocumentFromUrlAsync(new Uri(baseTableOfContentUrl));
            //NovelData novelData = GetNovelDataFromTableOfContent(html, siteConfig);

            NovelData novelData = new NovelData();
            novelData.LastTableOfContentsPageUrl = lastTableOfContentsUrl;
            novelData.RecentChapterUrls = chapterUrls;
            //novelData.ThumbnailUrl = new Uri(siteUri, novelData.ThumbnailUrl.TrimStart('/')).ToString();

            return novelData;
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
                    string chapterUrl = link.Attributes["href"]?.Value;

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

    }
}
