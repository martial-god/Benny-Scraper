using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using BennyScraper.Models;
using HtmlAgilityPack;
using Xunit;

namespace BennyScraper.Tests;

/// <summary>
/// Minimal concrete <see cref="ScraperStrategy"/> for tests. ScraperStrategy is abstract, so this
/// supplies the abstract members (unused here) and exposes a way to set the site config.
/// </summary>
internal sealed class TestableStrategy : ScraperStrategy
{
    public TestableStrategy()
    {
    }

    public TestableStrategy(IHttpClientFactory httpClientFactory)
        : base(httpClientFactory)
    {
    }

    public static int GetPageNumber(string pageValue, Uri baseUri) => GetTableOfContentsPageNumber(pageValue, baseUri);

    public static Uri GetPaginatedUri(Uri tableOfContentsUri, string paginationType, int pageNumber)
        => GetPaginatedTableOfContentsUri(tableOfContentsUri, paginationType, pageNumber);

    public void ConfigurePagination(SiteConfiguration siteConfiguration, Uri tableOfContentsUri)
    {
        ScraperData.SiteConfig = siteConfiguration;
        ScraperData.SiteTableOfContents = tableOfContentsUri;
        ScraperData.BaseUri = new Uri(tableOfContentsUri.GetLeftPart(UriPartial.Authority));
    }

    public Task<(List<ChapterLink> ChapterLinks, string LastTableOfContentsUrl)> GetPaginatedChapterLinksAsync(
        Uri tableOfContentsUri,
        int? pageToStopAt) =>
        GetPaginatedChapterLinksAsync(tableOfContentsUri, true, pageToStopAt);

    public void ConfigureSort(ChapterSortOrder order)
        => ScraperData.SiteConfig = new SiteConfiguration { ChapterSortOrder = order };

    public void ConfigureChapterDownloads(
        SiteConfiguration siteConfiguration,
        Uri tableOfContentsUri,
        int concurrentRequestLimit)
    {
        SetBaseUri(tableOfContentsUri);
        SetVariables(
            siteConfiguration,
            tableOfContentsUri,
            new Configuration { ConcurrencyLimit = concurrentRequestLimit });
    }

    public override Task<NovelDataBuffer> ScrapeAsync() => throw new NotImplementedException();

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        => throw new NotImplementedException();
}