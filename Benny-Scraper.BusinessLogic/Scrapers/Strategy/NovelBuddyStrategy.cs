using System.Text.Json;
using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

internal sealed class NovelBuddyStrategy : ScraperStrategy
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
                NovelDataInitializer.Attr.AlternativeNames,
                NovelDataInitializer.Attr.NovelRating,
                NovelDataInitializer.Attr.ThumbnailUrl,
                NovelDataInitializer.Attr.CurrentChapterUrl
            };

            await NovelBuddyInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData, attributesToFetch).ConfigureAwait(false);

            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
        }

        return novelDataBuffer;
    }

    private static string GetTitleId(HtmlDocument htmlDocument)
    {
        var nextDataNode = htmlDocument.DocumentNode.SelectSingleNode("//script[@id='__NEXT_DATA__']");

        if (nextDataNode == null)
        {
            throw new InvalidOperationException("NovelBuddy page data could not be found.");
        }

        using var nextDataDocument = JsonDocument.Parse(nextDataNode.InnerText);

        var titleId = nextDataDocument.RootElement
            .GetProperty("props")
            .GetProperty("pageProps")
            .GetProperty("initialManga")
            .GetProperty("id")
            .GetString();

        if (string.IsNullOrWhiteSpace(titleId))
        {
            throw new InvalidOperationException("NovelBuddy title ID could not be found.");
        }

        return titleId;
    }

    private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument).ConfigureAwait(false);
        await BuildChapterLinksFromChapterJsonAsync(htmlDocument, novelDataBuffer).ConfigureAwait(false);

        SortChapters(novelDataBuffer);

        Logger.Info($"Found {novelDataBuffer.ChapterLinks.Count} chapters");

        return novelDataBuffer;
    }

    private async Task BuildChapterLinksFromChapterJsonAsync(HtmlDocument htmlDocument, NovelDataBuffer novelDataBuffer)
    {
        var titleId = GetTitleId(htmlDocument);
        var chapterApiUri = new Uri($"https://api.novelbuddy.me/titles/{titleId}/chapters");
        var chapterResponse = await LoadJsonAsync<NovelBuddyChapterResponse>(chapterApiUri).ConfigureAwait(false);

        if (chapterResponse is not { Success: true, Data: not null })
        {
            Logger.Error($"NovelBuddy did not return chapter data from {chapterApiUri}");
            return;
        }

        foreach (var chapter in chapterResponse.Data.Chapters)
        {
            if (string.IsNullOrWhiteSpace(chapter.Url))
            {
                Logger.Warn($"NovelBuddy chapter '{chapter.Name}' did not have a URL.");
                continue;
            }

            if (!Uri.TryCreate(ScraperData.BaseUri, chapter.Url, out var chapterUri))
            {
                Logger.Warn($"NovelBuddy returned an invalid chapter URL: {chapter.Url}");
                continue;
            }

            novelDataBuffer.ChapterLinks.Add(new ChapterLink
            {
                Url = chapterUri.ToString(),
                Title = chapter.Name
            });
            novelDataBuffer.ChapterTitles.Add(chapter.Name);
        }
    }
}

internal abstract class NovelBuddyInitializer : NovelDataInitializer
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