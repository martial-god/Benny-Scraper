using HtmlAgilityPack;
using Xunit;
using Xunit.Abstractions;

namespace BennyScraper.Tests;

/// <summary>
/// Verifies a site's REAL chapter-content selector pulls the chapter text from a chapter page.
///
/// HOW TO ADD A SITE:
///   1. In the site's JSON file, note its selectors.chapterContent value.
///   2. Below, write the smallest HTML that has that content container with a couple of made-up
///      paragraphs (the container element is labelled with the selector).
///   3. Add a ChapterCase row with a phrase you expect to appear in the extracted text.
/// </summary>
public class ChapterParsingTests
{
    private const string _royalRoadChapter =
        """
        <html><body>
          <h1>Chapter 1</h1>
          <!-- chapterContent: //div[@class='chapter-inner chapter-content']//p -->
          <div class="chapter-inner chapter-content">
            <p>Lorem ipsum dolor sit amet.</p>
            <p>Second paragraph with an &amp; entity.</p>
          </div>
        </body></html>
        """;

    public static TheoryData<ChapterCase> Cases() => new()
    {
        new()
        {
            UrlPattern = "royalroad.com",
            Chapter = _royalRoadChapter,
            MustContain = "Lorem ipsum dolor sit amet.",
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExtractsChapterContent(ChapterCase chapterCase)
    {
        var siteConfig = TestConfig.LoadSiteConfig(chapterCase.UrlPattern);
        var htmlDocument = TestConfig.Parse(chapterCase.Chapter);

        var chapterContentSelector = siteConfig.Selectors.ChapterContent;
        Assert.NotNull(chapterContentSelector);
        var chapterContentNodes = htmlDocument.DocumentNode.SelectNodes(chapterContentSelector);
        Assert.NotNull(chapterContentNodes);

        var extractedChapterText = string.Join(
            "\n",
            chapterContentNodes.Select(chapterContentNode => HtmlEntity.DeEntitize(chapterContentNode.InnerText.Trim())));

        Assert.Contains(chapterCase.MustContain, extractedChapterText, StringComparison.Ordinal);
        Assert.Contains("an & entity.", extractedChapterText, StringComparison.Ordinal); // "&amp;" decoded
    }
}