using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Diagnostics;
using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

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
                timeoutSeconds: 60,
                isHeadless: !RequiresLogin,
                preWaitAction: async (driver, wait) =>
                {
                    var baseUrl = ScraperData.BaseUri!.GetLeftPart(UriPartial.Authority);
                    await Task.Delay(500);
                    var isLoggedIn = false;

                    if (RequiresLogin)
                    {
                        // Inform user they need to manually click login
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("\n╔═══════════════════════════════════════════════════╗");
                        Console.WriteLine("║          WuxiaWorld Login Instructions           ║");
                        Console.WriteLine("╚═══════════════════════════════════════════════════╝");
                        Console.ResetColor();
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
                            await Task.Delay(100);
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
                        driver.Navigate().GoToUrl(ScraperData.SiteTableOfContents.ToString());
                    }
                    
                    const string chaptersTabXPath = "//div[@role='tablist']//button[@role='tab'][.//span[normalize-space()='Chapters'] ]";
                    wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions
                        .ElementToBeClickable(By.XPath(chaptersTabXPath))).Click();

                    const string collapsedSummariesXPath = "//*[@id='full-width-tabpanel-1']//div[@role='button' and @aria-expanded='false']";
                    var collapsedSummaries = driver.FindElements(By.XPath(collapsedSummariesXPath));

                    foreach (var collapsedSummary in collapsedSummaries)
                    {
                        wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementToBeClickable(collapsedSummary)).Click();

                        var summary = collapsedSummary;
                        wait.Until(_ =>
                            summary.GetAttribute("aria-expanded") == "true"
                            || summary.FindElements(By.XPath(".//a")).Count > 0);
                        Console.WriteLine("Chapter urls expanded ");
                    }

                    wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(chapterLinksXPath)));
                    
                    if (isLoggedIn)
                    {
                        var buttonToOpenBalancesXpath =
                            ScraperData.SiteConfig.Selectors.UserCurrencyBalances?["buttonToOpenBalances"];
                        var premiumButton = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions
                            .ElementToBeClickable(By.XPath(buttonToOpenBalancesXpath)));
                        premiumButton.Click();

                        // Wait for the dropdown menu to fully appear
                        await Task.Delay(500);
                        wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions
                            .ElementExists(By.XPath("//ul[@role='menu']")));
                        

                        // DEBUG: Get and log the menu HTML to see what's actually there
                        var menuElement = driver.FindElement(By.XPath("//ul[@role='menu']"));
                        // var menuHtml = menuElement.GetAttribute("innerHTML");
                        // var allPTags = menuElement.FindElements(By.XPath(".//p"));
                        // Logger.Info($"DEBUG - Found {allPTags.Count} <p> tags in menu");
                        // foreach (var p in allPTags.Take(5))
                        // {
                        //     Logger.Info($"DEBUG - P tag text: '{p.Text}'");
                        // }

                        var karmaBalanceXpath =
                            ScraperData.SiteConfig.Selectors.UserCurrencyBalances?[ScraperData.SiteConfig.PremiumInfo!.CurrencyName.ToLower()];
                        var spiritStoneBalanceXpath =
                            ScraperData.SiteConfig.Selectors.UserCurrencyBalances?["spiritStones"];

                        var karmaValue = menuElement.FindElement(By.XPath(karmaBalanceXpath)).Text;
                        var spiritStoneValue = menuElement.FindElement(By.XPath(spiritStoneBalanceXpath)).Text;
                        int.TryParse(
                            karmaValue, 
                            NumberStyles.Integer | NumberStyles.AllowThousands,
                            CultureInfo.InvariantCulture, out var karma);
                        int.TryParse(
                            spiritStoneValue,
                            NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.InvariantCulture,
                            out var spiritStones);
                        novelDataBuffer.UserPremiumCurrencies =
                        [
                            new UserPremiumCurrency
                            {
                                CurrencyName = ScraperData.SiteConfig.PremiumInfo!.CurrencyName,
                                Balance = karma
                            },
                            new UserPremiumCurrency
                            {
                                CurrencyName = "Spirit Stones",
                                Balance = spiritStones
                            }
                        ];
                        premiumButton.Click();// close button back.
                        Logger.Info($"User balance - Karma: {karma:N0}, Spirit Stones: {spiritStones:N0}");
                    }
                },
                reuseExistingDriver: true);

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
