using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Diagnostics;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy;

/// <summary>
/// Strategy for https://wuxiaworld.com/
/// </summary>
public abstract class WuxiaworldInitializer : NovelDataInitializer
{
    public static async Task FetchNovelContentAsync(
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData,
        ScraperStrategy scraperStrategy,
        List<Attr> attributesToFetch)
    {
        Debug.Assert(scraperData.SiteTableOfContents != null, "scraperData.SiteTableOfContents != null");

        foreach (var attribute in attributesToFetch)
        {
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData);
        }

        if (attributesToFetch.Contains(Attr.ChapterUrls))
        {
            scraperStrategy.ExtractChapterUrlsAndTitles(htmlDocument, novelDataBuffer, scraperData);
            scraperStrategy.SortChapters(novelDataBuffer);
            novelDataBuffer.FirstChapter = novelDataBuffer.ChapterLinks.Count != 0
                ? novelDataBuffer.ChapterLinks.First().Url
                : string.Empty;
        }
    }
}

public class WuxiaWorldStrategy : ScraperStrategy
{
    /// <summary>
    /// This particular scraper requires Selenium for the Chapter Urls and Nowel Imge Thumbnail
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        Logger.Info($"Getting novel data for {GetType().Name}");
        if (ScraperData.SiteTableOfContents == null)
            throw new ArgumentNullException(nameof(ScraperData.SiteTableOfContents), "SiteTableOfContents cannot be null.");

        SetBaseUri(ScraperData.SiteTableOfContents);
        var (htmlDocument, uri) = await LoadHtmlAsync(ScraperData.SiteTableOfContents);

        try
        {
            var novelDataBuffer = await BuildNovelDataAsync(htmlDocument);
            novelDataBuffer.NovelUrl = uri.ToString();

            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error while getting novel data. {e}");
            throw;
        }
    }

    protected override async Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
    {
        Debug.Assert(ScraperData.SiteTableOfContents != null, "scraperData.SiteTableOfContents != null");
        var novelDataBuffer = new NovelDataBuffer();
        try
        {
            var attributesToFetchUsingHttp = new List<NovelDataInitializer.Attr>()
            {
                NovelDataInitializer.Attr.Title,
                NovelDataInitializer.Attr.Author,
                NovelDataInitializer.Attr.NovelStatus,
                NovelDataInitializer.Attr.Description,
                NovelDataInitializer.Attr.CurrentChapter,
                NovelDataInitializer.Attr.Genres
            };
            var attributesToFetchUsingSelenium = new List<NovelDataInitializer.Attr>()
            {
                NovelDataInitializer.Attr.ThumbnailUrl,
                NovelDataInitializer.Attr.ChapterUrls,
            };

            await WuxiaworldInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData, this, attributesToFetchUsingHttp);

            var chapterLinksXPath = ScraperData.SiteConfig!.Selectors.TableOfContents.ChapterLinks;

            var (htmlDocumentUsingSeleniumAsync, _) = await GetHtmlDocumentUsingSeleniumAsync(
                url: ScraperData.SiteTableOfContents.ToString(),
                requiredXPath: chapterLinksXPath,
                steps: null,
                objectToLookFor: "Chapter Links",
                isAllowedToFail: false,
                timeoutSeconds: 30,
                isHeadless: false,
                preWaitAction: async (driver, wait) =>
                {
                    const string chaptersTabXPath = "//div[@role='tablist']//button[@role='tab'][.//span[normalize-space()='Chapters']]";
                    wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions
                        .ElementToBeClickable(OpenQA.Selenium.By.XPath(chaptersTabXPath))).Click();

                    const string collapsedSummariesXPath = "//*[@id='full-width-tabpanel-1']//div[@role='button' and @aria-expanded='false']";
                    var collapsedSummaries = driver.FindElements(OpenQA.Selenium.By.XPath(collapsedSummariesXPath));

                    foreach (var collapsedSummary in collapsedSummaries)
                    {
                        wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(collapsedSummary)).Click();
                        await Task.Delay(150);
                    }
                });

            await WuxiaworldInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocumentUsingSeleniumAsync!, ScraperData, this, attributesToFetchUsingSelenium);

            return novelDataBuffer;
        }
        catch (Exception e)
        {
            Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
        }

        return novelDataBuffer;
    }

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
    {
        throw new NotImplementedException();
    }

    private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument);
        return novelDataBuffer;
    }
}
