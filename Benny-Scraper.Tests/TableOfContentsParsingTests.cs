using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using Xunit;
using Xunit.Abstractions;
using Attr = Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl.NovelDataInitializer.Attr;

namespace Benny_Scraper.Tests;

/// <summary>
/// Verifies a site's REAL selector config (from appsettings.json) extracts the title, author and
/// chapter list from a table-of-contents page.
///
/// HOW TO ADD A SITE:
///   1. Open appsettings.json, find the site, note its tableOfContents selectors:
///      novelTitle, novelAuthor, chapterLinks.
///   2. Below, write the SMALLEST HTML that contains just those target elements (made-up text).
///      Each element is labelled with the selector that targets it — if a selector has no matching
///      element, that field won't extract. Anything no selector points at is noise; leave it out.
///   3. Add a TocCase row with the values you expect.
/// </summary>
public class TableOfContentsParsingTests
{
    private const string RoyalRoadToc =
        """
        <html><body>
          <h1>Test Novel Title</h1>                                                  <!-- novelTitle:  //h1 -->
          <h4><span>by</span><span><a href="/profile/1">Test Author</a></span></h4>  <!-- novelAuthor: //h4/span[2]/a -->

          <!-- chapterLinks: //tr[@class='chapter-row']/td[1]/a   (hrefs are relative on purpose) -->
          <table>
            <tr class="chapter-row"><td><a href="/fiction/1/x/chapter/1/one">Chapter 1</a></td></tr>
            <tr class="chapter-row"><td><a href="/fiction/1/x/chapter/2/two">Chapter 2 &amp; More</a></td></tr>
            <tr class="chapter-row"><td><a href="/fiction/1/x/chapter/3/three">Chapter 3</a></td></tr>
          </table>
        </body></html>
        """;

    public static TheoryData<TocCase> Cases() => new()
    {
        new TocCase
        {
            UrlPattern = "royalroad.com",
            Toc = RoyalRoadToc,
            ExpectedTitle = "Test Novel Title",
            ExpectedAuthor = "Test Author",
            ExpectedChapterCount = 3,
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ExtractsTitleAuthorAndChapters(TocCase site)
    {
        var data = TestConfig.ScraperDataFor(site.UrlPattern);
        var doc = TestConfig.Parse(site.Toc);
        var buffer = new NovelDataBuffer();

        await NovelDataInitializer.FetchContentByAttributeAsync(Attr.Title, buffer, doc, data);
        await NovelDataInitializer.FetchContentByAttributeAsync(Attr.Author, buffer, doc, data);
        await NovelDataInitializer.FetchContentByAttributeAsync(Attr.ChapterUrls, buffer, doc, data);

        Assert.Equal(site.ExpectedTitle, buffer.Title);
        Assert.Equal(site.ExpectedAuthor, buffer.Author);
        Assert.Equal(site.ExpectedChapterCount, buffer.ChapterLinks.Count);

        // Relative hrefs should have been resolved to absolute URLs on the site's real host.
        var firstChapterUrl = buffer.ChapterLinks[0].Url;
        Assert.StartsWith("https://", firstChapterUrl);
        Assert.Contains(site.UrlPattern, firstChapterUrl);

        // "&amp;" should have been decoded to "&".
        Assert.Equal("Chapter 2 & More", buffer.ChapterLinks[1].Title);
    }
}

/// <summary>One row of <see cref="TableOfContentsParsingTests"/>: a synthetic TOC and what it should yield.</summary>
public sealed class TocCase : IXunitSerializable
{
    public string UrlPattern { get; set; } = "";
    public string Toc { get; set; } = "";
    public string ExpectedTitle { get; set; } = "";
    public string ExpectedAuthor { get; set; } = "";
    public int ExpectedChapterCount { get; set; }

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(UrlPattern), UrlPattern);
        info.AddValue(nameof(Toc), Toc);
        info.AddValue(nameof(ExpectedTitle), ExpectedTitle);
        info.AddValue(nameof(ExpectedAuthor), ExpectedAuthor);
        info.AddValue(nameof(ExpectedChapterCount), ExpectedChapterCount);
    }

    public void Deserialize(IXunitSerializationInfo info)
    {
        UrlPattern = info.GetValue<string>(nameof(UrlPattern));
        Toc = info.GetValue<string>(nameof(Toc));
        ExpectedTitle = info.GetValue<string>(nameof(ExpectedTitle));
        ExpectedAuthor = info.GetValue<string>(nameof(ExpectedAuthor));
        ExpectedChapterCount = info.GetValue<int>(nameof(ExpectedChapterCount));
    }

    public override string ToString() => UrlPattern;
}
