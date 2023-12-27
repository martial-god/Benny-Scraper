using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy;

public class NovelDramaInitializer : NovelDataInitializer
{
    public static void FetchNovelContentAsync(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData)
    {
        throw new NotImplementedException();
    }
}

public class NovelDramaStrategy : ScraperStrategy
{
    public override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
    {
        throw new NotImplementedException();
    }

    public override Task<NovelDataBuffer> ScrapeAsync()
    {
        throw new NotImplementedException();
    }
}
