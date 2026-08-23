using System.Diagnostics;
using System.Globalization;
using BennyScraper.BusinessLogic.Extensions;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;
using OpenQA.Selenium;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

internal sealed class WuxiaWorldStrategy : ScraperStrategy
{
    /// <summary>
    /// This particular scraper requires Selenium for the Chapter Urls and Nowel Imge Thumbnail.
    /// </summary>
    /// <returns>The populated <see cref="NovelDataBuffer"/> containing the novel's metadata and chapter links.</returns>
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        Logger.Info($"Getting novel data for {GetType().Name}");
        SetBaseUri(ScraperData.SiteTableOfContents);
        var (htmlDocument, uri) = await LoadHtmlAsync(ScraperData.SiteTableOfContents).ConfigureAwait(false);

        try
        {
            var novelDataBuffer = await BuildNovelDataAsync(htmlDocument).ConfigureAwait(false);
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
                NovelDataInitializer.Attr.Genres
            };
            var attributesToFetchUsingSelenium = new List<NovelDataInitializer.Attr>()
            {
                NovelDataInitializer.Attr.ThumbnailUrl,
                NovelDataInitializer.Attr.ChapterUrls,
            };

            await WuxiaworldInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData, this, attributesToFetchUsingHttp).ConfigureAwait(false);

            var chapterLinksXPath = ScraperData.SiteConfig.Selectors.TableOfContents.ChapterLinks ?? string.Empty;

            var (htmlDocumentUsingSeleniumAsync, _) = await GetHtmlDocumentUsingSeleniumAsync(
                url: ScraperData.SiteTableOfContents.ToString(),
                requiredXPath: chapterLinksXPath,
                steps: null,
                objectToLookFor: "Chapter Links",
                isAllowedToFail: false,
                timeoutSeconds: 60,
                isHeadless: !RequiresLogin,
                preWaitAction: async (driver, wait) =>
                {
                    var baseUrl = ScraperData.BaseUri.GetLeftPart(UriPartial.Authority);
                    await Task.Delay(500).ConfigureAwait(false);
                    var isLoggedIn = false;

                    if (RequiresLogin)
                    {
                        var loginMessages = new[] { $"WuxiaWorld Login Instructions" };
                        CommonHelper.DrawBox(loginMessages, ConsoleColor.Cyan);
                        Console.WriteLine("\nA browser window is now open showing the novel page.\n");
                        Console.WriteLine("To access premium chapters:");
                        Console.WriteLine("  1. Click the 'Login' button in the browser");
                        Console.WriteLine("  2. Log in with your WuxiaWorld account");
                        Console.WriteLine("  3. You'll be redirected back to the novel page\n");
                        Console.WriteLine("Or press Enter now to skip login and continue without premium access.\n");

                        Logger.Info("Waiting for user to either login or skip (30 second timeout)");

                        var timeoutSeconds = 30;
                        var stopwatch = Stopwatch.StartNew();
                        var redirectDetected = false;
                        var lastDisplayedSecond = -1;

                        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
                        {
                            if (Console.KeyAvailable)
                            {
                                var key = Console.ReadKey(true);
                                if (key.Key == ConsoleKey.Enter)
                                {
                                    Console.Write("\r" + new string(' ', 60) + "\r");
                                    Logger.Info("User pressed Enter to skip login");
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine("⚠ Skipping login - proceeding without premium access");
                                    Console.ResetColor();
                                    break;
                                }
                            }

                            if (driver.Url.Contains("identity.wuxiaworld.com", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.Write("\r" + new string(' ', 60) + "\r");
                                redirectDetected = true;
                                break;
                            }

                            var currentSecond = (int)stopwatch.Elapsed.TotalSeconds;
                            var remaining = timeoutSeconds - currentSecond;

                            if (currentSecond != lastDisplayedSecond)
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.Write($"\rWaiting for login... ({remaining} seconds remaining)");
                                Console.ResetColor();
                                lastDisplayedSecond = currentSecond;
                            }

                            await Task.Delay(100).ConfigureAwait(false);
                        }

                        if (redirectDetected)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("✓ Login page detected!");
                            Console.WriteLine("\nPlease complete your login in the browser.");
                            Console.WriteLine("After logging in, you'll be redirected back automatically.");
                            Console.WriteLine("\nPress Enter once you see the novel page again to continue...");
                            Console.ResetColor();

                            Console.ReadLine();

                            Logger.Info("User confirmed login completion, waiting for redirect back to main site");

                            wait.Until(_ => driver.Url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase));
                            Logger.Info("Successfully returned to main site after login");
                            isLoggedIn = true;
                        }
                        else if (stopwatch.Elapsed.TotalSeconds >= timeoutSeconds)
                        {
                            Console.Write("\r" + new string(' ', 60) + "\r");
                            Logger.Info("Login timeout - continuing without login");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("⚠ Time's up - continuing without login (premium chapters not accessible)");
                            Console.ResetColor();
                        }

                        await driver.Navigate().GoToUrlAsync(ScraperData.SiteTableOfContents.ToString()).ConfigureAwait(false);
                    }

                    const string chaptersTabXPath = "//div[@role='tablist']//button[@role='tab'][.//span[normalize-space()='Chapters']]";
                    wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions
                        .ElementToBeClickable(By.XPath(chaptersTabXPath))).Click();

                    const string collapsedSummariesXPath = "//*[@id='full-width-tabpanel-1']//h3/button[@type='button' and @aria-expanded='false']";
                    wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(collapsedSummariesXPath)));

                    var collapsedSummaries = driver.FindElements(By.XPath(collapsedSummariesXPath));
                    Logger.Info($"Found {collapsedSummaries.Count} collapsed summaries");
                    var jsExecutor = (IJavaScriptExecutor)driver;

                    const string chapterLinksWithinSummaryXPath = "../following-sibling::*[1]//a[@href]";

                    foreach (var collapsedSummary in collapsedSummaries)
                    {
                        var summarySpans = collapsedSummary.FindElements(By.XPath(".//span"));
                        wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(collapsedSummary)).Click();
                        Console.Write($"\r\tSummary {summarySpans.First().Text} clicked. Waiting for links to load.\t");

                        var summary = collapsedSummary;
                        wait.Until(_ =>
                        {
                            try
                            {
                                return summary.GetAttribute("aria-expanded") == "true"
                                    && summary.FindElements(By.XPath(chapterLinksWithinSummaryXPath)).Count > 0;
                            }
                            catch (StaleElementReferenceException)
                            {
                                return false;
                            }
                        });

                        Console.Write($"Chapter links found:{summary.FindElements(By.XPath(chapterLinksWithinSummaryXPath)).Count}. ");
                    }

                    wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(chapterLinksXPath)));

                    if (isLoggedIn)
                    {
                        var buttonToOpenBalancesXpath =
                            ScraperData.SiteConfig.Selectors.UserCurrencyBalances?["buttonToOpenBalances"] ?? string.Empty;
                        var premiumButton = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions
                            .ElementToBeClickable(By.XPath(buttonToOpenBalancesXpath)));
                        premiumButton.Click();

                        await Task.Delay(500).ConfigureAwait(false);
                        wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions
                            .ElementExists(By.XPath("//ul[@role='menu']")));

                        var menuElement = driver.FindElement(By.XPath("//ul[@role='menu']"));

                        var karmaBalanceXpath =
                            ScraperData.SiteConfig.Selectors.UserCurrencyBalances?[ScraperData.SiteConfig.PremiumInfo!.CurrencyName.ToLowerCase()] ?? string.Empty;
                        var spiritStoneBalanceXpath =
                            ScraperData.SiteConfig.Selectors.UserCurrencyBalances?["spiritStones"] ?? string.Empty;

                        var karmaValue = menuElement.FindElement(By.XPath(karmaBalanceXpath)).Text ?? "-1";
                        var spiritStoneValue = menuElement.FindElement(By.XPath(spiritStoneBalanceXpath)).Text ?? "-1";
                        int.TryParse(
                            karmaValue,
                            NumberStyles.Integer | NumberStyles.AllowThousands,
                            CultureInfo.InvariantCulture,
                            out var karma);
                        int.TryParse(
                            spiritStoneValue,
                            NumberStyles.Integer | NumberStyles.AllowThousands,
                            CultureInfo.InvariantCulture,
                            out var spiritStones);
                        novelDataBuffer.UserPremiumCurrencies.ReplaceWith(new List<UserPremiumCurrency>
                        {
                            new()
                            {
                                CurrencyName = ScraperData.SiteConfig.PremiumInfo!.CurrencyName,
                                Balance = karma
                            },
                            new()
                            {
                                CurrencyName = "Spirit Stones",
                                Balance = spiritStones
                            }
                        });
                        premiumButton.Click(); // close button back.
                        Logger.Info($"User balance - Karma: {karma:N0}, Spirit Stones: {spiritStones:N0}");
                    }

                    novelDataBuffer.IsLoggedIn = isLoggedIn;
                },
                reuseExistingDriver: true).ConfigureAwait(false);

            await WuxiaworldInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocumentUsingSeleniumAsync!, ScraperData, this, attributesToFetchUsingSelenium).ConfigureAwait(false);

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
        var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument).ConfigureAwait(false);
        return novelDataBuffer;
    }
}

/// <summary>
/// Strategy for https://wuxiaworld.com/.
/// </summary>
internal abstract class WuxiaworldInitializer : NovelDataInitializer
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