using Benny_Scraper.Models;
using HtmlAgilityPack;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    public class MangaKakalotStrategy : ScraperStrategy
    {
        public override NovelData FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        {
            throw new NotImplementedException();
        }

        public override Task<NovelData> ScrapeAsync()
        {
            throw new NotImplementedException();
        }
    }
}