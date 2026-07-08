using HtmlAgilityPack;
using Xunit;
using Xunit.Abstractions;

namespace BennyScraper.Tests;

/// <summary>
/// Verifies a site's REAL chapter-content selector pulls the chapter text from a chapter page.
///
/// HOW TO ADD A SITE:
///   1. In appsettings.json, note the site's selectors.chapterContent value.
///   2. Below, write the smallest HTML that has that content container with a couple of made-up
///      paragraphs (the container element is labelled with the selector).
///   3. Add a ChapterCase row with a phrase you expect to appear in the extracted text.
/// </summary>
public class ChapterParsingTests
{
    private const string RoyalRoadChapter =
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
        new ChapterCase
        {
            UrlPattern = "royalroad.com",
            Chapter = RoyalRoadChapter,
            MustContain = "Lorem ipsum dolor sit amet.",
        },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExtractsChapterContent(ChapterCase site)
    {
        var config = TestConfig.LoadSiteConfig(site.UrlPattern);
        var doc = TestConfig.Parse(site.Chapter);

        var contentNodes = doc.DocumentNode.SelectNodes(config.Selectors.ChapterContent);
        Assert.NotNull(contentNodes);

        var text = string.Join("\n", contentNodes!.Select(n => HtmlEntity.DeEntitize(n.InnerText.Trim())));

        Assert.Contains(site.MustContain, text);
        Assert.Contains("an & entity.", text); // "&amp;" decoded
    }
}

/// <summary>One row of <see cref="ChapterParsingTests"/>: a synthetic chapter page and a phrase it should yield.</summary>
public sealed class ChapterCase : IXunitSerializable
{
    public string UrlPattern { get; set; } = "";

    public string Chapter { get; set; } = "";

    public string MustContain { get; set; } = "";

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(UrlPattern), UrlPattern);
        info.AddValue(nameof(Chapter), Chapter);
        info.AddValue(nameof(MustContain), MustContain);
    }

    public void Deserialize(IXunitSerializationInfo info)
    {
        UrlPattern = info.GetValue<string>(nameof(UrlPattern));
        Chapter = info.GetValue<string>(nameof(Chapter));
        MustContain = info.GetValue<string>(nameof(MustContain));
    }

    public override string ToString() => UrlPattern;
}