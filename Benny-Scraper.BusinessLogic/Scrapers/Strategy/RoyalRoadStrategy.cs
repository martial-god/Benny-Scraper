using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

/// <summary>
/// Initializer for RoyalRoad site
/// </summary>
public abstract class RoyalRoadInitializer : NovelDataInitializer
{
    public static async Task FetchNovelContentAsync(
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData,
        IReadOnlyList<Attr> attributesToFetch)
    {
        foreach (var attribute in attributesToFetch)
        {
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData);
        }
    }
}

/// <summary>
/// Scraping strategy for royalroad.com
/// HTTP-based scraper, no Selenium required
/// </summary>
public class RoyalRoadStrategy : ScraperStrategy
{
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        Logger.Info($"Getting novel data for {GetType().Name}");
        SetBaseUri(ScraperData.SiteTableOfContents);
        var (htmlDocument, uri) = await LoadHtmlAsync(ScraperData.SiteTableOfContents);

        try
        {
            var novelDataBuffer = await BuildNovelDataAsync(htmlDocument);
            novelDataBuffer.NovelUrl = uri.ToString();

            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error while getting novel data. {e}");
            throw;
        }
    }

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
    {
        throw new NotImplementedException();
    }

    protected override async Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = new NovelDataBuffer();
        try
        {
            var attributesToFetch = new List<NovelDataInitializer.Attr>
            {
                NovelDataInitializer.Attr.Title,
                NovelDataInitializer.Attr.Author,
                NovelDataInitializer.Attr.Description,
                NovelDataInitializer.Attr.Genres,
                NovelDataInitializer.Attr.NovelStatus,
                NovelDataInitializer.Attr.ThumbnailUrl,
                NovelDataInitializer.Attr.ChapterUrls
            };

            await RoyalRoadInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData, attributesToFetch);

            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
        }

        return novelDataBuffer;
    }

    private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument);
        SortChapters(novelDataBuffer);

        Logger.Info($"Found {novelDataBuffer.ChapterLinks.Count} chapters");

        return novelDataBuffer;
    }
}