using BennyScraper.BusinessLogic.Scrapers.Strategy;
using HtmlAgilityPack;

namespace BennyScraper.Tests;

public sealed class RoyalRoadChapterListTests
{
    private static readonly Uri _novelUri = new("https://example.com/fiction/1/test-novel");

    [Fact]
    public void ReadsTheCompleteScriptListWhenTheTableOnlyShowsSomeChapters()
    {
        var document = new HtmlDocument();
        document.LoadHtml("""
            <table>
              <tr class="chapter-row"><td><a href="/chapter/one">Chapter One</a></td></tr>
            </table>
            <script>
            window.chapters = [
              {"url":"/chapter/one","title":"Chapter One","order":0},
              {"url":"/chapter/two","title":"Chapter Two","order":1},
              {"url":"/chapter/three","title":"Chapter Three","order":2}
            ];
            window.volumes = [];
            </script>
            """);

        var chapters = RoyalRoadStrategy.ReadChapterLinks(document, _novelUri);

        string[] expectedTitles = ["Chapter One", "Chapter Two", "Chapter Three"];
        string[] expectedUrls = ["https://example.com/chapter/one", "https://example.com/chapter/two", "https://example.com/chapter/three"];
        Assert.Equal(expectedTitles, chapters.Select(chapter => chapter.Title));
        Assert.Equal(expectedUrls, chapters.Select(chapter => chapter.Url));
    }

    [Fact]
    public void UsesOrderAndDecodesJsonEscapesWhileIgnoringOtherFields()
    {
        var document = new HtmlDocument();
        document.LoadHtml("""
            <script>
            window.chapters=[
              {"url":"/chapter/two","title":"Second: ]; in a title","order":1,"id":42},
              {"url":"/chapter/one","title":"First \u2014 \"Hello\"","order":0}
            ]; window.volumes = []; otherJavascript();
            </script>
            """);

        var chapters = RoyalRoadStrategy.ReadChapterLinks(document, _novelUri);

        Assert.Equal("First — \"Hello\"", chapters[0].Title);
        Assert.Equal("https://example.com/chapter/one", chapters[0].Url);
        Assert.Equal("Second: ]; in a title", chapters[1].Title);
    }

    [Theory]
    [InlineData("<table><tr class='chapter-row'><td><a href='/one'>Only visible chapter</a></td></tr></table>")]
    [InlineData("<script>window.chapters = [];</script>")]
    [InlineData("<script>window.chapters = null;</script>")]
    [InlineData("<script>window.chapters = [{\"url\":\"\",\"title\":\"One\",\"order\":0}];</script>")]
    public void RejectsMissingOrInvalidChapterLists(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        Assert.Throws<InvalidDataException>(() => RoyalRoadStrategy.ReadChapterLinks(document, _novelUri));
    }
}