using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;

namespace Benny_Scraper.BusinessLogic
{
    /// <summary>
    /// A selenium implementation of the INovelScraper interface. Use this for sites that require login-in to get the chapter contents like wuxiaworld.com
    /// </summary>
    public class SeleniumNovelScraper : INovelScraper
    {
        public ScraperStrategy GetScraperStrategy(Uri novelTableOfContentsUri, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }
    }
}
