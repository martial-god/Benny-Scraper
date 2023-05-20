using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    public class NovelFullStrategy : ScraperStrategy
    {
        public override NovelData GetNovelDataFromTableOfContent(HtmlDocument htmlDocument)
        {
            throw new NotImplementedException();
        }

        public override Task<NovelData> ScrapeAsync()
        {
            throw new NotImplementedException();
        }
    }
}
