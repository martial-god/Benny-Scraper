using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

/// <summary>
/// Scraping strategy for royalroad.com.
/// HTTP-based scraper, no Selenium required.
/// </summary>
internal sealed class RoyalRoadStrategy(
    IHttpClientFactory httpClientFactory,
    IDriverFactory driverFactory) : ScraperStrategy(httpClientFactory, driverFactory)
{
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        Logger.Info($"Getting novel data for {GetType().Name}");
        SetBaseUri(ScraperData.SiteTableOfContents);
        var (htmlDocument, uri) = await LoadHtmlAsync(ScraperData.SiteTableOfContents).ConfigureAwait(false);

        try
        {
            var novelDataBuffer = await BuildNovelDataAsync(htmlDocument).ConfigureAwait(false);
            novelDataBuffer.NovelUrl = uri.ToString();

            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error while getting novel data. {e}");
            throw;
        }
    }

    internal static List<ChapterLink> ReadChapterLinks(HtmlDocument document, Uri novelUri)
    {
        const string variable = "window.chapters";
        var script = document.DocumentNode.SelectSingleNode($"//script[contains(., '{variable}')]")?.InnerText
            ?? throw new InvalidDataException("RoyalRoad page does not contain its chapter list.");
        var assignment = script.IndexOf('=', script.IndexOf(variable, StringComparison.Ordinal));
        if (assignment < 0)
        {
            throw new InvalidDataException("RoyalRoad chapter list assignment is missing.");
        }

        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(script[(assignment + 1)..]));
        var chapters = JsonSerializer.Deserialize<List<RoyalRoadChapter>>(ref reader);
        if (chapters == null || chapters.Count == 0 ||
            chapters.Any(chapter => chapter == null || string.IsNullOrWhiteSpace(chapter.Url) ||
                                    string.IsNullOrWhiteSpace(chapter.Title) || chapter.Order < 0))
        {
            throw new InvalidDataException("RoyalRoad returned an empty or invalid chapter list.");
        }

        return chapters.Select(chapter => new ChapterLink
        {
            Url = new Uri(novelUri, chapter.Url).AbsoluteUri,
            Title = chapter.Title.Trim()
        }).ToList();
    }

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument) => throw new NotImplementedException();

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
                NovelDataInitializer.Attr.ThumbnailUrl
            };

            await RoyalRoadInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData, attributesToFetch).ConfigureAwait(false);
            foreach (var chapter in ReadChapterLinks(htmlDocument, ScraperData.SiteTableOfContents))
            {
                novelDataBuffer.ChapterLinks.Add(chapter);
                novelDataBuffer.ChapterTitles.Add(chapter.Title!);
            }

            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
            novelDataBuffer.Dispose();
            throw;
        }
    }

    private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument).ConfigureAwait(false);
        SortChapters(novelDataBuffer);

        Logger.Info($"Found {novelDataBuffer.ChapterLinks.Count} chapters");

        return novelDataBuffer;
    }

    private sealed record RoyalRoadChapter
    {
        [JsonPropertyName("url")]
        public required string Url { get; init; }

        [JsonPropertyName("title")]
        public required string Title { get; init; }

        [JsonPropertyName("order")]
        public required int Order { get; init; }
    }
}

/// <summary>
/// Initializer for RoyalRoad site.
/// </summary>
internal abstract class RoyalRoadInitializer : NovelDataInitializer
{
    public static async Task FetchNovelContentAsync(
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData,
        IReadOnlyList<Attr> attributesToFetch)
    {
        foreach (var attribute in attributesToFetch)
        {
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData).ConfigureAwait(false);
        }
    }
}