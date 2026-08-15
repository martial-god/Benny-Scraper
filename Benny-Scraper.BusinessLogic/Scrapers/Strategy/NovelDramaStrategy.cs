using System.Globalization;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

internal sealed class NovelDramaStrategy : ScraperStrategy
{
    private readonly string _lastTableOfContentsPageNumberXpath = "//*[@id='chapters']/div[2]/div[2]/div/div/input"; // This site does not have a last page button, so we have to get the last page number from the input box
    private readonly string _chapterMaxAttribute = "data-max";

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

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument) => throw new NotImplementedException();

    protected override async Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = new NovelDataBuffer();
        try
        {
            await NovelDramaInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData).ConfigureAwait(false);
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
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument).ConfigureAwait(false);

        int pageToStopAt = FetchLastTableOfContentsPageNumber(htmlDocument);
        var (chapterLinks, lastTableOfContentsUrl) = await GetPaginatedChapterLinksAsync(ScraperData.SiteTableOfContents, true, pageToStopAt).ConfigureAwait(false);

        novelDataBuffer.ChapterLinks.ReplaceWith(chapterLinks);
        novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsUrl; // this needs to be updated as it is not the same as what was set in FetchNovelDataFromTableOfContentsAsync

        // Sort chapters based on site configuration
        SortChapters(novelDataBuffer);

        return novelDataBuffer;
    }

    private int FetchLastTableOfContentsPageNumber(HtmlDocument htmlDocument)
    {
        try
        {
            HtmlNode lastPageNode = htmlDocument.DocumentNode.SelectSingleNode(_lastTableOfContentsPageNumberXpath);
            string lastPage = lastPageNode.Attributes[_chapterMaxAttribute].Value;

            int lastPageNumber = int.Parse(lastPage, NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

            if (ScraperData.SiteConfig.PageOffSet > 0)
            {
                lastPageNumber += ScraperData.SiteConfig.PageOffSet;
            }

            Logger.Info($"Last table of contents page number is {lastPage}");
            return lastPageNumber;
        }
        catch (Exception e)
        {
            Logger.Error($"Error when getting last page table of contents page number. {e}");
            throw;
        }
    }
}

internal abstract class NovelDramaInitializer : NovelDataInitializer
{
    public static async Task FetchNovelContentAsync(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData)
    {
        var tableOfContents = scraperData.SiteTableOfContents;
        var attributesToFetch = new List<Attr>()
        {
            Attr.Author,
            Attr.Title,
            Attr.NovelStatus,
            Attr.Genres,
            Attr.Description,
            Attr.ThumbnailUrl,
            Attr.LastTableOfContentsPage,
            Attr.FirstChapterUrl,
            Attr.CurrentChapter
        };

        foreach (var attribute in attributesToFetch)
        {
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData).ConfigureAwait(false);
        }

        var fullCurrentChapterUrl = new Uri(tableOfContents, novelDataBuffer.CurrentChapterUrl?.TrimStart('/')).ToString();
        var fullThumbnailUrl = new Uri(tableOfContents, novelDataBuffer.ThumbnailUrl?.TrimStart('/')).ToString();
        var fullLastTableOfContentUrl = new Uri(tableOfContents, novelDataBuffer.LastTableOfContentsPageUrl?.TrimStart('/')).ToString();

        novelDataBuffer.ThumbnailUrl = fullThumbnailUrl;
        novelDataBuffer.LastTableOfContentsPageUrl = fullCurrentChapterUrl;
        novelDataBuffer.LastTableOfContentsPageUrl = fullLastTableOfContentUrl;
    }
}