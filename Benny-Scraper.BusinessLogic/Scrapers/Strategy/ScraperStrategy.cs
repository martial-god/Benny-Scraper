using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    public abstract class ScraperStrategy
    {
        protected SiteConfiguration SiteConfig { get; private set; }
        public abstract NovelData Scrape();
        public abstract NovelData GetNovelDataFromTableOfContent(HtmlDocument htmlDocument, SiteConfiguration siteConfig);
        
        public void SetSiteConfiguration(SiteConfiguration siteConfig)
        {
            SiteConfig = siteConfig;
        }
    }
}
