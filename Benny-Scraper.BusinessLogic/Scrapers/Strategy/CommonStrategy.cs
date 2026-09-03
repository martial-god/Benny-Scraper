using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

internal sealed class CommonStrategy : ScraperStrategy
{
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        Logger.Info($"Starting scraper for {nameof(CommonStrategy)} with base uri: {ScraperData.BaseUri}");

        SetBaseUri(ScraperData.SiteTableOfContents);

        var (htmlDocument, uri) = await LoadHtmlAsync(ScraperData.SiteTableOfContents).ConfigureAwait(false);
        try
        {
            var novelDataBuffer = await BuildNovelDataAsync(htmlDocument, uri).ConfigureAwait(false);
            novelDataBuffer.NovelUrl = uri.ToString();

            return novelDataBuffer;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error scraping novel data from {ScraperData.SiteTableOfContents}");
            throw;
        }
    }

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument) => throw new NotImplementedException();

    protected override async Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = new NovelDataBuffer();
        try
        {
            await CommonStrategyInitializer.FetchNovelContentAsync(
                novelDataBuffer,
                htmlDocument,
                ScraperData).ConfigureAwait(false);
            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
        }

        return novelDataBuffer;
    }

    private static Uri ResolveTableOfContentsUri(Uri novelUri, string? tableOfContentsPath)
    {
        if (string.IsNullOrWhiteSpace(tableOfContentsPath))
        {
            return novelUri;
        }

        var tableOfContentsPathUri = $"{novelUri.AbsolutePath.TrimEnd('/')}/{tableOfContentsPath.TrimStart('/')}";

        return new Uri(novelUri, tableOfContentsPathUri);
    }

    private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument, Uri novelUri)
    {
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument).ConfigureAwait(false);
        var tableOfContentsUri = ResolveTableOfContentsUri(novelUri, ScraperData.SiteConfig.TableOfContentsPath);
        var tableOfContentsDocument = htmlDocument;

        if (!string.IsNullOrWhiteSpace(ScraperData.SiteConfig.TableOfContentsPath))
        {
            (tableOfContentsDocument, tableOfContentsUri) = await LoadHtmlAsync(tableOfContentsUri).ConfigureAwait(false);
        }

        await CommonStrategyInitializer.FetchTableOfContentsContentAsync(
            novelDataBuffer,
            tableOfContentsDocument,
            ScraperData).ConfigureAwait(false);

        if (ScraperData.SiteConfig.HasPagination)
        {
            int? pageToStopAt = null;
            if (!string.IsNullOrEmpty(novelDataBuffer.LastTableOfContentsPageUrl))
            {
                var lastTableOfContentsPageNumber = GetTableOfContentsPageNumber(
                    novelDataBuffer.LastTableOfContentsPageUrl,
                    ScraperData.BaseUri);
                pageToStopAt = lastTableOfContentsPageNumber >= 1 ? lastTableOfContentsPageNumber : null;
            }

            var (chapterLinks, lastTableOfContentsUrl) = await GetPaginatedChapterLinksAsync(tableOfContentsUri, true, pageToStopAt).ConfigureAwait(false);

            novelDataBuffer.ChapterLinks.ReplaceWith(chapterLinks);
            novelDataBuffer.ChapterTitles.ReplaceWith(chapterLinks.Select(cl => cl.Title ?? string.Empty));
            novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsUrl;
            Console.WriteLine($"Got chapter urls, total: {novelDataBuffer.ChapterLinks.Count}");
        }
        else
        {
            await CommonStrategyInitializer.FetchChapterLinksAsync(
                novelDataBuffer,
                tableOfContentsDocument,
                ScraperData).ConfigureAwait(false);
        }

        SortChapters(novelDataBuffer);

        return novelDataBuffer;
    }
}

internal abstract class CommonStrategyInitializer : NovelDataInitializer
{
    public static async Task FetchNovelContentAsync(
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData)
    {
        var attributesToFetch = GetNovelAttributesToFetch(scraperData.SiteConfig);

        await FetchAttributesAsync(novelDataBuffer, htmlDocument, scraperData, attributesToFetch).ConfigureAwait(false);
    }

    public static async Task FetchTableOfContentsContentAsync(
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData)
    {
        var attributesToFetch = GetTableOfContentsAttributesToFetch(scraperData.SiteConfig);

        await FetchAttributesAsync(novelDataBuffer, htmlDocument, scraperData, attributesToFetch).ConfigureAwait(false);
    }

    public static Task FetchChapterLinksAsync(
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData) =>
        FetchContentByAttributeAsync(Attr.ChapterUrls, novelDataBuffer, htmlDocument, scraperData);

    private static async Task FetchAttributesAsync(
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData,
        IEnumerable<Attr> attributesToFetch)
    {
        foreach (var attribute in attributesToFetch)
        {
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData).ConfigureAwait(false);
        }
    }

    private static List<Attr> GetNovelAttributesToFetch(SiteConfiguration siteConfiguration)
    {
        var tableOfContentsSelectors = siteConfiguration.Selectors.TableOfContents;
        var attributesToFetch = new List<Attr>();

        AddAttributeWhenConfigured(tableOfContentsSelectors.NovelTitle, Attr.Title, attributesToFetch);
        AddAttributeWhenConfigured(tableOfContentsSelectors.NovelAuthor, Attr.Author, attributesToFetch);
        AddAttributeWhenConfigured(tableOfContentsSelectors.NovelRating, Attr.NovelRating, attributesToFetch);
        AddAttributeWhenConfigured(tableOfContentsSelectors.TotalRatings, Attr.TotalRatings, attributesToFetch);
        AddAttributeWhenConfigured(tableOfContentsSelectors.NovelDescription, Attr.Description, attributesToFetch);
        AddAttributeWhenConfigured(tableOfContentsSelectors.NovelGenres, Attr.Genres, attributesToFetch);
        AddAttributeWhenConfigured(tableOfContentsSelectors.NovelAlternativeNames, Attr.AlternativeNames, attributesToFetch);
        AddAttributeWhenConfigured(tableOfContentsSelectors.NovelStatus, Attr.NovelStatus, attributesToFetch);

        if (!string.IsNullOrWhiteSpace(tableOfContentsSelectors.NovelThumbnailUrl) &&
            !string.IsNullOrWhiteSpace(tableOfContentsSelectors.ThumbnailUrlAttribute))
        {
            attributesToFetch.Add(Attr.ThumbnailUrl);
        }

        return attributesToFetch;
    }

    private static List<Attr> GetTableOfContentsAttributesToFetch(SiteConfiguration siteConfiguration)
    {
        var tableOfContentsSelectors = siteConfiguration.Selectors.TableOfContents;
        var attributesToFetch = new List<Attr>();

        if (siteConfiguration.HasPagination)
        {
            AddAttributeWhenConfigured(
                tableOfContentsSelectors.LastTableOfContentsPage,
                Attr.LastTableOfContentsPage,
                attributesToFetch);
        }

        AddAttributeWhenConfigured(tableOfContentsSelectors.LatestChapterLink, Attr.CurrentChapterUrl, attributesToFetch);

        return attributesToFetch;
    }

    private static void AddAttributeWhenConfigured(
        string? selector,
        Attr attribute,
        List<Attr> attributesToFetch)
    {
        if (!string.IsNullOrWhiteSpace(selector))
        {
            attributesToFetch.Add(attribute);
        }
    }
}