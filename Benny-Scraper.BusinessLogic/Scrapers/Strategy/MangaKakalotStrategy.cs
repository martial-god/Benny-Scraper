using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;
using System.Text;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

/// <summary>
/// Strategy for https://mangakakalot.to/
/// </summary>
public abstract class MangaKakalotInitializer : NovelDataInitializer
{
    public static async Task FetchNovelContentAsync(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData, ScraperStrategy scraperStrategy)
    {
        int.TryParse(scraperData.SiteTableOfContents?.Segments.Last().Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last(), out int novelId);
        var queryBuilder = new StringBuilder(scraperData?.BaseUri?.ToString());
        queryBuilder.Append("ajax/manga/list-chapter-volume?id=");
        queryBuilder.Append(novelId);
        var uriQueryForChapterUrls = new Uri(queryBuilder.ToString());
        var (htmlDocumentForChapterUrls, uri) = await scraperStrategy.LoadHtmlPublicAsync(uriQueryForChapterUrls);

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
            if (attribute == Attr.CurrentChapter)
            {
                await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocumentForChapterUrls, scraperData);
                novelDataBuffer.CurrentChapterUrl = new Uri(scraperData.BaseUri, novelDataBuffer.CurrentChapterUrl).ToString();
            }
            else
            {
                await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData);
            }
        }

        scraperStrategy.ExtractChapterUrlsAndTitles(htmlDocumentForChapterUrls, novelDataBuffer, scraperData);
        scraperStrategy.SortChapters(novelDataBuffer);

        if (novelDataBuffer.ChapterLinks.Count != 0)
        {
            novelDataBuffer.FirstChapter = novelDataBuffer.ChapterLinks.First().Url;
        }
    }
}

public class MangaKakalotStrategy : ScraperStrategy
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

    protected override async Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = new NovelDataBuffer();
        try
        {
            await Task.WhenAll(MangaKakalotInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData, this));
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