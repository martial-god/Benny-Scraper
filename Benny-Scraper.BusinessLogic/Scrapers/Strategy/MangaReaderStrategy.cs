using System.Text;
using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

internal sealed class MangaReaderStrategy : ScraperStrategy
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
            await Task.WhenAll(MangaReaderInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData, this)).ConfigureAwait(false);
            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
        }

        return novelDataBuffer;
    }

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument) => throw new NotImplementedException();

    private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument).ConfigureAwait(false);
        return novelDataBuffer;
    }
}

/// <summary>
/// Strategy for https://mangareader.to/.
/// </summary>
internal abstract class MangaReaderInitializer : NovelDataInitializer
{
    public static async Task FetchNovelContentAsync(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData, ScraperStrategy scraperStrategy)
    {
        _ = int.TryParse(scraperData.SiteTableOfContents?.Segments.Last().Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last(), out int novelId);
        var queryBuilder = new StringBuilder(scraperData.BaseUri?.ToString());
        queryBuilder.Append("ajax/manga/list-chapter-volume?id=");
        queryBuilder.Append(novelId);
        var uriQueryForChapterUrls = new Uri(queryBuilder.ToString());

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

        // Extract chapter URLs and titles in one pass
        scraperStrategy.ExtractChapterUrlsAndTitles(htmlDocument, novelDataBuffer, scraperData);

        // Sort chapters based on site configuration
        scraperStrategy.SortChapters(novelDataBuffer);

        if (!string.IsNullOrEmpty(novelDataBuffer.MostRecentChapterTitle))
        {
            novelDataBuffer.MostRecentChapterTitle = novelDataBuffer.MostRecentChapterTitle.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).First(); // remove new line and everything after
        }
    }
}