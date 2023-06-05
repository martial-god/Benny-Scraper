using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.Interfaces
{
    public interface INovelScraper
    {
        public ScraperStrategy? GetScraperStrategy(Uri novelTableOfContentsUri, SiteConfiguration siteConfig);       
    }
}
