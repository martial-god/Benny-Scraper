using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using NLog;

namespace Benny_Scraper.BusinessLogic
{
    /// <summary>
    /// A http implementation of the INovelScraper interface. Use this for sites that don't require login-in to get the chapter contents.
    /// </summary>
    public class HttpNovelScraper : INovelScraper
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private ScraperStrategy _scraperStrategy;
        private Dictionary<string, ScraperStrategy> _websiteMap = new Dictionary<string, ScraperStrategy>();

        public HttpNovelScraper()
        {
            AddSupportForWebsite();
        }

        #region setup maps
        public void AddSiteToMap(string siteName, ScraperStrategy scraperStrategy)
        {
            _websiteMap.Add(siteName, scraperStrategy);
        }

        void AddSupportForWebsite()
        {
            AddSiteToMap("https://www.lightnovelworld.com", new LightNovelWorldStrategy());
            AddSiteToMap("https://novelfull.com", new NovelFullStrategy());
            AddSiteToMap("https://mangakakalot.to", new MangaKakalotStrategy());
            AddSiteToMap("https://mangareader.to", new MangaReaderStrategy());
            AddSiteToMap("https://mangakatana.com", new MangaKatanaStrategy());
        }
        #endregion

        /// <summary>
        /// Returns the scraper strategy for the given site. If no strategy is found, null is returned. Classes are added to the map in the constructor.
        /// </summary>
        /// <param name="novelTableOfContentsUri"></param>
        /// <param name="siteConfig"></param>
        /// <returns></returns>
        public ScraperStrategy? GetScraperStrategy(Uri novelTableOfContentsUri, SiteConfiguration siteConfig)
        {
            string baseUrl = novelTableOfContentsUri.GetLeftPart(UriPartial.Authority);

            if (_websiteMap.TryGetValue(baseUrl, out _scraperStrategy))
            {
                return _scraperStrategy;
            }

            Logger.Error($"No scraper strategy found for {baseUrl}");
            return null;
        }
    }
}
