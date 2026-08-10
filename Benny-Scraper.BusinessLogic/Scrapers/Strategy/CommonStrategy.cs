using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

public class CommonStrategy : ScraperStrategy
{
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        Logger.Info($"Starting scraper for {GetType().Name}");

        SetBaseUri(ScraperData.SiteTableOfContents);

        var (htmlDocument, uri) = await LoadHtmlAsync(ScraperData.SiteTableOfContents).ConfigureAwait(false);
        try
        {
            var novelDataBuffer = await BuildNovelDataAsync(htmlDocument).ConfigureAwait(false);
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

    private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument).ConfigureAwait(false);

        if (ScraperData.SiteConfig.HasPagination && !string.IsNullOrEmpty(novelDataBuffer.LastTableOfContentsPageUrl))
        {
            var pageToStopAt = GetPageNumberFromUrlQuery(novelDataBuffer.LastTableOfContentsPageUrl, ScraperData.BaseUri);
            var (chapterLinks, lastTableOfContentsUrl) = await GetPaginatedChapterLinksAsync(ScraperData.SiteTableOfContents, true, pageToStopAt).ConfigureAwait(false);

            novelDataBuffer.ChapterLinks.ReplaceWith(chapterLinks);
            novelDataBuffer.ChapterTitles.ReplaceWith(chapterLinks.Select(cl => cl.Title ?? string.Empty));
            novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsUrl;
        }

        SortChapters(novelDataBuffer);

        return novelDataBuffer;
    }
}

public abstract class CommonStrategyInitializer : NovelDataInitializer
{
    public static async Task FetchNovelContentAsync(
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData)
    {
        ArgumentNullException.ThrowIfNull(novelDataBuffer);
        ArgumentNullException.ThrowIfNull(htmlDocument);
        ArgumentNullException.ThrowIfNull(scraperData);
        var attributesToFetch = GetAttributesToFetch(scraperData.SiteConfig);

        foreach (var attribute in attributesToFetch)
        {
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData).ConfigureAwait(false);
        }
    }

    private static List<Attr> GetAttributesToFetch(SiteConfiguration siteConfiguration)
    {
        ArgumentNullException.ThrowIfNull(siteConfiguration);

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

        if (siteConfiguration.HasPagination)
        {
            AddAttributeWhenConfigured(
                tableOfContentsSelectors.LastTableOfContentsPage,
                Attr.LastTableOfContentsPage,
                attributesToFetch);
        }

        AddAttributeWhenConfigured(tableOfContentsSelectors.ChapterLinks, Attr.ChapterUrls, attributesToFetch);
        AddAttributeWhenConfigured(tableOfContentsSelectors.LatestChapterLink, Attr.CurrentChapter, attributesToFetch);

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