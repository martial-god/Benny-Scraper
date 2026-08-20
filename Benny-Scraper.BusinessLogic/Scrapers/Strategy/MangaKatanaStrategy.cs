using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

internal sealed class MangaKatanaStrategy : ScraperStrategy
{
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        Logger.Info($"Getting novel data for {this.GetType().Name}");
        SetBaseUri(ScraperData.SiteTableOfContents);

        var (htmlDocument, uri) = await LoadHtmlAsync(ScraperData.SiteTableOfContents).ConfigureAwait(false);

        try
        {
            NovelDataBuffer novelDataBuffer = await BuildNovelDataAsync(htmlDocument).ConfigureAwait(false);
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
            await Task.WhenAll(MangaKatanaInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData, this)).ConfigureAwait(false);
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
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument).ConfigureAwait(false);
        return novelDataBuffer;
    }
}

/// <summary>
/// Strategy for https://mangakatana.com/.
/// </summary>
internal abstract class MangaKatanaInitializer : NovelDataInitializer
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
            Attr.CurrentChapterUrl
        };

        foreach (var attribute in attributesToFetch)
        {
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData).ConfigureAwait(false);
        }

        scraperStrategy.ExtractChapterUrlsAndTitles(htmlDocument, novelDataBuffer, scraperData);
        scraperStrategy.SortChapters(novelDataBuffer);

        if (!string.IsNullOrEmpty(novelDataBuffer.MostRecentChapterTitle))
        {
            novelDataBuffer.MostRecentChapterTitle = novelDataBuffer.MostRecentChapterTitle.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).First(); // remove new line and everything after
        }
    }
}