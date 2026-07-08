using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

/// <summary>
/// Strategy for https://mangakatana.com/
/// </summary>
public abstract class MangaKatanaInitializer : NovelDataInitializer
{
    public static async Task FetchNovelContentAsync(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData, ScraperStrategy scraperStrategy)
    {
        var attributesToFetch = new List<Attr>()
        {
            Attr.Title,
            Attr.Author,
            Attr.NovelStatus,
            Attr.Genres,
            Attr.AlternativeNames,
            Attr.Description,
            Attr.ThumbnailUrl,
            Attr.CurrentChapter
        };

        foreach (var attribute in attributesToFetch)
        {
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData);
        }

        scraperStrategy.ExtractChapterUrlsAndTitles(htmlDocument, novelDataBuffer, scraperData);
        scraperStrategy.SortChapters(novelDataBuffer);

        if (novelDataBuffer.ChapterLinks.Any())
        {
            novelDataBuffer.FirstChapter = novelDataBuffer.ChapterLinks.First().Url;
        }

        if (!string.IsNullOrEmpty(novelDataBuffer.MostRecentChapterTitle))
        {
            novelDataBuffer.MostRecentChapterTitle = novelDataBuffer.MostRecentChapterTitle.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).First(); // remove new line and everything after
        }
    }
}

public class MangaKatanaStrategy : ScraperStrategy
{
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        Logger.Info($"Getting novel data for {this.GetType().Name}");
        SetBaseUri(ScraperData.SiteTableOfContents);

        var (htmlDocument, uri) = await LoadHtmlAsync(ScraperData.SiteTableOfContents);

        try
        {
            NovelDataBuffer novelDataBuffer = await BuildNovelDataAsync(htmlDocument);
            novelDataBuffer.NovelUrl = uri.ToString();

            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error while getting novel data. {e}");
            throw;
        }
    }

    protected override async Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = new NovelDataBuffer();
        try
        {
            await Task.WhenAll(MangaKatanaInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData, this));
            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
        }

        return novelDataBuffer;
    }

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
    {
        throw new NotImplementedException();
    }

    private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument);
        return novelDataBuffer;
    }
}