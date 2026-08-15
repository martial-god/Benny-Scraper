using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

/// <summary>
/// Scraping strategy for novelbin.me and novlove.com
/// Handles Cloudflare protection and dynamic chapter list loading via Selenium.
/// </summary>
internal sealed class NovelBinStrategy : ScraperStrategy
{
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        Logger.Info($"Getting novel data for {GetType().Name}");
        SetBaseUri(ScraperData.SiteTableOfContents);

        var chapterLinksXPath = ScraperData.SiteConfig.Selectors.TableOfContents.ChapterLinks;

        var (htmlDocument, _) = await GetHtmlDocumentUsingSeleniumAsync(
            url: ScraperData.SiteTableOfContents.ToString(),
            requiredXPath: chapterLinksXPath ?? string.Empty,
            objectToLookFor: "Chapter Links",
            isAllowedToFail: false,
            timeoutSeconds: 60,
            isHeadless: false,
            preWaitAction: async (driver, wait) =>
            {
                try
                {
                    Logger.Info("Attempting to click 'Chapter List' tab...");
                    var chapterTab = wait.Until(ExpectedConditions.ElementToBeClickable(
                        By.XPath("//a[@id='tab-chapters-title'][@role='tab']")));
                    chapterTab.Click();

                    Logger.Info("Successfully clicked 'Chapter List' tab");

                    try
                    {
                        wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(
                            By.XPath("//ul[@class='list-chapter']/li/a")));
                        Logger.Info("Initial chapter links are visible");
                    }
                    catch (WebDriverTimeoutException)
                    {
                        Logger.Warn("Explicit wait for chapters timed out");
                    }

                    Logger.Info("Waiting for all chapters to lazy load...");
                    var previousCount = 0;
                    var stableCount = 0;
                    const int maxWaitIterations = 30;

                    for (var i = 0; i < maxWaitIterations; i++)
                    {
                        await Task.Delay(1000).ConfigureAwait(false);

                        var currentChapters = driver.FindElements(By.XPath("//ul[@class='list-chapter']/li/a"));
                        var currentCount = currentChapters.Count;

                        if (currentCount == previousCount)
                        {
                            stableCount++;

                            // If count hasn't changed for 3 consecutive checks, assume loading is complete
                            if (stableCount < 3)
                            {
                                continue;
                            }

                            Logger.Info($"Chapter count stabilized at {currentCount} chapters");
                            break;
                        }

                        Logger.Info($"Chapters loading: {currentCount} found...");
                        stableCount = 0;
                        previousCount = currentCount;
                    }

                    Logger.Info("Chapter lazy loading complete");
                }
                catch (WebDriverTimeoutException)
                {
                    Logger.Warn("Could not find 'Chapter List' tab - it might already be active");
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Unexpected error while clicking chapter tab: {ex.Message}");
                }
            },
            reuseExistingDriver: true).ConfigureAwait(false);

        if (htmlDocument == null)
        {
            Logger.Error("Failed to load HTML document using Selenium");
            return new NovelDataBuffer();
        }

        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument).ConfigureAwait(false);
        novelDataBuffer.NovelUrl = ScraperData.SiteTableOfContents.ToString();
        ExtractChapterUrlsAndTitles(htmlDocument, novelDataBuffer, ScraperData);
        SortChapters(novelDataBuffer);

        Logger.Info($"Found {novelDataBuffer.ChapterLinks.Count} chapters");

        return novelDataBuffer;
    }

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument) => throw new NotImplementedException();

    protected override async Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
    {
        var novelDataBuffer = new NovelDataBuffer();

        var attributesToFetch = new List<NovelDataInitializer.Attr>
        {
            NovelDataInitializer.Attr.Title,
            NovelDataInitializer.Attr.Author,
            NovelDataInitializer.Attr.Genres,
            NovelDataInitializer.Attr.NovelStatus,
            NovelDataInitializer.Attr.NovelRating,
            NovelDataInitializer.Attr.TotalRatings,
            NovelDataInitializer.Attr.Description,
            NovelDataInitializer.Attr.ThumbnailUrl,
            NovelDataInitializer.Attr.CurrentChapter
        };

        await NovelBinInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData, this, attributesToFetch).ConfigureAwait(false);

        return novelDataBuffer;
    }
}

/// <summary>
/// Initializer for NovelBin and NovLove sites.
/// </summary>
internal abstract class NovelBinInitializer : NovelDataInitializer
{
    public static async Task FetchNovelContentAsync(
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData,
        ScraperStrategy scraperStrategy,
        IReadOnlyList<Attr> attributesToFetch)
    {
        foreach (var attribute in attributesToFetch)
        {
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData).ConfigureAwait(false);
        }

        if (attributesToFetch.Contains(Attr.ChapterUrls))
        {
            scraperStrategy.SortChapters(novelDataBuffer);
        }
    }
}