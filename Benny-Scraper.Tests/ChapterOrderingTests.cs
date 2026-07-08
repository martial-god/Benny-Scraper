using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using BennyScraper.Models;
using HtmlAgilityPack;
using Xunit;

namespace BennyScraper.Tests;

/// <summary>
/// Chapter order is decided in the strategy from the site's chapterSortOrder, then numbered 1..n.
/// Whatever order a site lists chapters in, the result must come out oldest -> newest reading order.
/// Guards the "chapters come out shuffled" bug class (e.g. NovelBin).
/// </summary>
public class ChapterOrderingTests
{
    [Theory]
    [InlineData(ChapterSortOrder.Ascending, "oldest", "middle", "newest")]
    [InlineData(ChapterSortOrder.None, "oldest", "middle", "newest")]
    [InlineData(ChapterSortOrder.Descending, "newest", "middle", "oldest")]
    public void SortChapters_AlwaysYieldsOldestToNewest(
        ChapterSortOrder pageOrder, string topOfPage, string middleOfPage, string bottomOfPage)
    {
        var strategy = new TestableStrategy();
        strategy.ConfigureSort(pageOrder);
        var buffer = new NovelDataBuffer
        {
            ChapterLinks = new List<ChapterLink>
            {
                new() { Url = topOfPage }, new() { Url = middleOfPage }, new() { Url = bottomOfPage },
            },
            ChapterTitles = [topOfPage, middleOfPage, bottomOfPage],
        };

        strategy.SortChapters(buffer);

        // Regardless of how the page listed them, reading order is oldest -> newest, numbered 1..3.
        Assert.Equal(["oldest", "middle", "newest"], buffer.ChapterLinks.Select(c => c.Url));
        Assert.Equal([1, 2, 3], buffer.ChapterLinks.Select(c => c.ChapterNumber));
    }
}

/// <summary>
/// Minimal concrete <see cref="ScraperStrategy"/> for tests. ScraperStrategy is abstract, so this
/// supplies the abstract members (unused here) and exposes a way to set the site config.
/// </summary>
internal sealed class TestableStrategy : ScraperStrategy
{
    public void ConfigureSort(ChapterSortOrder order)
        => ScraperData.SiteConfig = new SiteConfiguration { ChapterSortOrder = order };

    public override Task<NovelDataBuffer> ScrapeAsync() => throw new NotImplementedException();

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        => throw new NotImplementedException();
}