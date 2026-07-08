using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;
using System.Diagnostics;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

public abstract class WanderingInnInitializer : NovelDataInitializer
{
    private const string DefaultAuthor = "Pirate Aba";
    private const string DefaultTitle = "The Wandering Inn";
    private const string DefaultDescription =
        "\"No killing Goblins.\"\nSo reads the sign outside of The Wandering Inn, a small building run by a young woman named Erin Solstice. She serves pasta with sausage, blue fruit juice, and dead acid flies on request. And she comes from another world. Ours.\r\n\r\nIt's a bad day when Erin finds herself transported to a fantastical world and nearly gets eaten by a Dragon. She doesn't belong in a place where monster attacks are a fact of life, and where Humans are one species among many. But she must adapt to her new life. Or die.\r\n\r\nIn a dangerous world where magic is real and people can level up and gain classes, Erin Solstice must battle somewhat evil Goblins, deadly Rock Crabs, and hungry [Necromancers]. She is no warrior, no mage. Erin Solstice runs an inn.\nShe's an [Innkeeper].";

    private const string DefaultThumbnailUrl = "https://wanderinginn.com/wp-content/uploads/2023/06/book1-768x1229-1-187x300.png";

    public static async Task FetchNovelContentAsync(
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData,
        ScraperStrategy scraperStrategy,
        List<Attr> attributesToFetch)
    {
        Debug.Assert(scraperData.SiteTableOfContents != null, "scraperData.SiteTableOfContents != null");

        foreach (var attribute in attributesToFetch)
        {
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData);
        }


        if (novelDataBuffer.Description == null || novelDataBuffer.Description.Count == 0)
        {
            novelDataBuffer.Description = new List<string> { DefaultDescription };
        }

        if (string.IsNullOrWhiteSpace(novelDataBuffer.ThumbnailUrl))
        {
            novelDataBuffer.ThumbnailUrl = DefaultThumbnailUrl;
            try
            {
                using var client = scraperData.HttpClientFactory?.CreateClient() ?? new HttpClient();
                novelDataBuffer.ThumbnailImage = await client.GetByteArrayAsync(DefaultThumbnailUrl);
            }
            catch (Exception)
            {
            }
        }

        if (attributesToFetch.Contains(Attr.ChapterUrls))
        {
            scraperStrategy.SortChapters(novelDataBuffer);
            novelDataBuffer.FirstChapter = novelDataBuffer.ChapterLinks.Count != 0
                ? novelDataBuffer.ChapterLinks.First().Url
                : string.Empty;
            novelDataBuffer.CurrentChapterUrl = novelDataBuffer.ChapterLinks.Count != 0
                ? novelDataBuffer.ChapterLinks.Last().Url
                : string.Empty;
        }

        novelDataBuffer.Title = DefaultTitle;
        novelDataBuffer.Author = DefaultAuthor;
    }
}

public class WanderingInnStrategy : ScraperStrategy
{
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        Logger.Info($"Getting novel data for {GetType().Name}");
        if (ScraperData.SiteTableOfContents == null)
        {
            throw new ArgumentNullException(nameof(ScraperData.SiteTableOfContents), "SiteTableOfContents cannot be null.");
        }

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
            var attributesToFetch = new List<NovelDataInitializer.Attr>
            {
                NovelDataInitializer.Attr.ChapterUrls,
            };

            await WanderingInnInitializer.FetchNovelContentAsync(
                novelDataBuffer,
                htmlDocument,
                ScraperData,
                this,
                attributesToFetch);

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

    protected override string NormalizeChapterTitle(string? rawTitle)
    {
        return base.NormalizeChapterTitle(rawTitle).Replace("- The Wandering Inn", "").Trim();
    }

    private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument);
        return novelDataBuffer;
    }
}