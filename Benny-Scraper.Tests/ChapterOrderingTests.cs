using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Helper;
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
    public void SortChaptersAlwaysYieldsOldestToNewest(
        ChapterSortOrder pageOrder, string topOfPage, string middleOfPage, string bottomOfPage)
    {
        using var strategy = new TestableStrategy();
        strategy.ConfigureSort(pageOrder);
        using var novelDataBuffer = new NovelDataBuffer();
        novelDataBuffer.ChapterLinks.ReplaceWith(new List<ChapterLink>
        {
            new() { Url = topOfPage }, new() { Url = middleOfPage }, new() { Url = bottomOfPage },
        });
        novelDataBuffer.ChapterTitles.ReplaceWith(new List<string> { topOfPage, middleOfPage, bottomOfPage });

        strategy.SortChapters(novelDataBuffer);

        // Regardless of how the page listed them, reading order is oldest -> newest, numbered 1..3.
        Assert.Equal(["oldest", "middle", "newest"], novelDataBuffer.ChapterLinks.Select(chapterLink => chapterLink.Url));
        Assert.Equal([1, 2, 3], novelDataBuffer.ChapterLinks.Select(chapterLink => chapterLink.ChapterNumber));
        Assert.Equal("oldest", novelDataBuffer.FirstChapter);
        Assert.Equal("newest", novelDataBuffer.CurrentChapterUrl);
        Assert.Equal("newest", novelDataBuffer.MostRecentChapterTitle);
    }
}