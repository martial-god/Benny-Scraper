using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using NLog;
using System.Net;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    public abstract class ScraperStrategy
    {
        protected SiteConfiguration SiteConfig { get; private set; }
        protected Uri SiteTableOfContents { get; private set; }

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
            string allSegementsButLast = siteUri.Segments.Take(siteUri.Segments.Length - 1).Aggregate(
                (segment1, segmenet2) => segment1 + segmenet2);
            return new Uri(baseUri, allSegementsButLast);
        }
    }
}
