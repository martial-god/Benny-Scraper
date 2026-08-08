using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using Xunit;
using Xunit.Abstractions;
using Attr = BennyScraper.BusinessLogic.Scrapers.Strategy.Impl.NovelDataInitializer.Attr;

namespace BennyScraper.Tests;

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
    private const string _royalRoadTocHtml =
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
            Toc = _royalRoadTocHtml,
            ExpectedTitle = "Test Novel Title",
            ExpectedAuthor = "Test Author",
            ExpectedChapterCount = 3,
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ExtractsTitleAuthorAndChapters(TocCase tocCase)
    {
        var scraperData = TestConfig.ScraperDataFor(tocCase.UrlPattern);
        var htmlDocument = TestConfig.Parse(tocCase.Toc);
        using var novelDataBuffer = new NovelDataBuffer();

        await NovelDataInitializer.FetchContentByAttributeAsync(Attr.Title, novelDataBuffer, htmlDocument, scraperData);
        await NovelDataInitializer.FetchContentByAttributeAsync(Attr.Author, novelDataBuffer, htmlDocument, scraperData);
        await NovelDataInitializer.FetchContentByAttributeAsync(Attr.ChapterUrls, novelDataBuffer, htmlDocument, scraperData);

        Assert.Equal(tocCase.ExpectedTitle, novelDataBuffer.Title);
        Assert.Equal(tocCase.ExpectedAuthor, novelDataBuffer.Author);
        Assert.Equal(tocCase.ExpectedChapterCount, novelDataBuffer.ChapterLinks.Count);

        // Relative hrefs should have been resolved to absolute URLs on the site's real host.
        var firstChapterUrl = novelDataBuffer.ChapterLinks[0].Url;
        Assert.StartsWith("https://", firstChapterUrl, StringComparison.Ordinal);
        Assert.Contains(tocCase.UrlPattern, firstChapterUrl, StringComparison.Ordinal);

        // "&amp;" should have been decoded to "&".
        Assert.Equal("Chapter 2 & More", novelDataBuffer.ChapterLinks[1].Title);
    }
}