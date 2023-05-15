using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    public class NovelFullStrategy : ScraperStrategy
    {
        public override NovelData GetNovelDataFromTableOfContent(HtmlDocument htmlDocument, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }

        public override NovelData Scrape()
        {
            throw new NotImplementedException();
        }
    }
}
