using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy;

public abstract class TestStrategyInitializer : NovelDataInitializer
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Tests a single attribute field for the interactive testing mode
    /// </summary>
    public static async Task TestSingleFieldAsync(
        Attr attribute,
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData)
    {
        await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData);
    }

    /// <summary>
    /// Validates all configured fields for an existing site configuration
    /// </summary>
    public static async Task<(string fieldName, bool success)> ValidateFieldAsync(
        string fieldName,
        Attr attribute,
        HtmlDocument htmlDocument,
        ScraperData scraperData,
        bool hasSelector)
    {
        if (!hasSelector)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  ⊘ {fieldName}: Not configured (skipped)");
            Console.ResetColor();
            return (fieldName, true);
        }

        try
        {
            var novelDataBuffer = new NovelDataBuffer();
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData);

            var hasData = attribute switch
            {
                Attr.Title => !string.IsNullOrEmpty(novelDataBuffer.Title),
                Attr.Author => !string.IsNullOrEmpty(novelDataBuffer.Author),
                Attr.Description => novelDataBuffer.Description?.Any() == true,
                Attr.Genres => novelDataBuffer.Genres?.Any() == true,
                Attr.NovelStatus => !string.IsNullOrEmpty(novelDataBuffer.NovelStatus),
                Attr.AlternativeNames => novelDataBuffer.AlternativeNames?.Any() == true,
                Attr.ThumbnailUrl => !string.IsNullOrEmpty(novelDataBuffer.ThumbnailUrl),
                Attr.NovelRating => novelDataBuffer.Rating > 0,
                Attr.TotalRatings => novelDataBuffer.TotalRatings > 0,
                _ => false
            };

            if (!hasData)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ {fieldName}: No data found");
                Console.ResetColor();
                return (fieldName, false);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            var dataPreview = attribute switch
            {
                Attr.Title => $"  ✓ {fieldName}: {novelDataBuffer.Title}",
                Attr.Author => $"  ✓ {fieldName}: {novelDataBuffer.Author}",
                Attr.Description => $"  ✓ {fieldName}: {novelDataBuffer.Description?.Count ?? 0} line(s)",
                Attr.Genres => $"  ✓ {fieldName}: {string.Join(", ", novelDataBuffer.Genres?.Take(3) ?? new List<string>())}{(novelDataBuffer.Genres?.Count > 3 ? "..." : "")}",
                Attr.NovelStatus => $"  ✓ {fieldName}: {novelDataBuffer.NovelStatus}",
                Attr.AlternativeNames => $"  ✓ {fieldName}: {novelDataBuffer.AlternativeNames?.Count ?? 0} name(s)",
                Attr.ThumbnailUrl => $"  ✓ {fieldName}: {novelDataBuffer.ThumbnailUrl}",
                Attr.NovelRating => $"  ✓ {fieldName}: {novelDataBuffer.Rating}",
                Attr.TotalRatings => $"  ✓ {fieldName}: {novelDataBuffer.TotalRatings}",
                _ => $"  ✓ {fieldName}: Found"
            };
            Console.WriteLine(dataPreview);
            Console.ResetColor();

            return (fieldName, true);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ {fieldName}: Error - {ex.Message}");
            Console.ResetColor();
            Logger.Error($"Validation error for {fieldName}: {ex}");
            return (fieldName, false);
        }
    }
}

/// <summary>
/// Simple test strategy for testing site connectivity without implementing a full scraper.
/// Provides single-attempt testing without retry logic for faster testing.
/// </summary>
public class TestStrategy(IHttpClientFactory httpClientFactory, IDriverFactory? driverFactory = null) : ScraperStrategy(httpClientFactory, driverFactory)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IDriverFactory _driverFactory = driverFactory ?? new DriverFactory();
    private HtmlDocument _htmlDocument;
    private Uri _testUri;
    private SiteConfiguration _config;
    private ScraperData _scraperData;
    private HtmlDocument _chapterHtmlDocument;
    private bool _requiredFieldsFailed;
    private bool _titleFailed;
    private bool _chapterLinksFailed;

    public async Task RunInteractiveTestAsync(Uri testUri)
    {
        _testUri = testUri;

        Console.WriteLine($"\n{new string('=', 70)}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Interactive Site Testing Mode");
        Console.ResetColor();
        Console.WriteLine($"{new string('=', 70)}\n");

        Console.WriteLine($"Testing URL: {testUri}\n");

        var (htmlDocument, updatedUri, statusCode, cloudflareDetected) = await TestLoadHtmlAsync(testUri);

        if (htmlDocument == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Failed to load page (Status: {statusCode})");
            if (cloudflareDetected)
            {
                Console.WriteLine("✗ Cloudflare protection detected");
            }
            Console.ResetColor();
            return;
        }

        _htmlDocument = htmlDocument;
        _testUri = updatedUri;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Page loaded successfully (Status: {statusCode})\n");
        Console.ResetColor();

        InitializeConfiguration();
        await TestFieldsInteractivelyAsync();

        await TestChapterContentInteractivelyAsync();

        var keepModifying = true;
        while (keepModifying)
        {
            if (_requiredFieldsFailed)
            {
                Console.WriteLine($"\n{new string('=', 70)}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Configuration Incomplete - Required Fields Missing");
                Console.ResetColor();
                Console.WriteLine($"{new string('=', 70)}\n");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("The following required fields failed:");
                if (_titleFailed)
                {
                    Console.WriteLine("  - Title");
                }
                if (_chapterLinksFailed)
                {
                    Console.WriteLine("  - Chapter Links");
                }
                Console.WriteLine("\nYou must provide valid selectors for these fields to create a working configuration.");
                Console.ResetColor();

                Console.WriteLine("\nWhat would you like to do?");
                Console.WriteLine("  1. Retry failed required fields");
                Console.WriteLine("  2. Modify any field");
                Console.WriteLine("  3. Save incomplete configuration anyway");
                Console.WriteLine("  4. Exit without saving");
                Console.Write("\nChoice (1-4): ");
            }
            else
            {
                Console.WriteLine($"\n{new string('=', 70)}");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ All required fields completed");
                Console.ResetColor();
                Console.WriteLine($"{new string('=', 70)}\n");

                Console.WriteLine("What would you like to do?");
                Console.WriteLine("  1. Modify any field");
                Console.WriteLine("  2. Continue to save configuration");
                Console.WriteLine("  3. Exit without saving");
                Console.Write("\nChoice (1-3): ");
            }

            var choice = Console.ReadLine()?.Trim();

            if (_requiredFieldsFailed)
            {
                switch (choice)
                {
                    case "1":
                        await RetryFailedRequiredFieldsAsync();
                        break;
                    case "2":
                        await ModifyFieldsInteractivelyAsync();
                        break;
                    case "3":
                        keepModifying = false;
                        break;
                    case "4":
                        Console.WriteLine("\nConfiguration not saved.");
                        return;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("✗ Invalid choice");
                        Console.ResetColor();
                        break;
                }
            }
            else
            {
                switch (choice)
                {
                    case "1":
                        await ModifyFieldsInteractivelyAsync();
                        break;
                    case "2":
                        keepModifying = false;
                        break;
                    case "3":
                        Console.WriteLine("\nConfiguration not saved.");
                        return;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("✗ Invalid choice");
                        Console.ResetColor();
                        break;
                }
            }
        }

        GenerateAndDisplayConfig();
    }

    private void InitializeConfiguration()
    {
        var host = _testUri.Host.Replace("www.", "");
        var siteName = host.Split('.')[0];
        siteName = char.ToUpper(siteName[0]) + siteName.Substring(1);

        _config = new SiteConfiguration
        {
            Name = siteName,
            UrlPattern = host,
            Selectors = new Selectors()
            {
                TableOfContents = new TableOfContentsSelectors()
            },
            HasPagination = false,
            PaginationType = null,
            PaginationQueryPartial = null,
            CompletedStatus = null,
            ChaptersPerPage = 0,
            PageOffSet = 0,
            HasNovelInfoOnDifferentPage = false,
            EntireSiteRequiresSelenium = false,
            TableOfContentsRequiresSelenium = false,
            ChapterContentRequiresSelenium = false,
            CloudflareProtection = CloudflareProtectionLevel.None,
            IsActive = true,
            ChapterSortOrder = ChapterSortOrder.Ascending
        };

        _scraperData = new ScraperData
        {
            SiteConfig = _config,
            SiteTableOfContents = _testUri,
            BaseUri = new Uri(_testUri.GetLeftPart(UriPartial.Authority)),
            HttpClientFactory = _httpClientFactory
        };

        Console.WriteLine($"Site Name: {_config.Name}");
        Console.WriteLine($"URL Pattern: {_config.UrlPattern}\n");
    }

    private async Task TestFieldsInteractivelyAsync()
    {
        Console.WriteLine($"{new string('-', 70)}");
        Console.WriteLine("Field Testing - Press Enter to skip optional fields");
        Console.WriteLine($"{new string('-', 70)}\n");

        await TestFieldAsync("Title", "//h1[@class='heading']/text()", NovelDataInitializer.Attr.Title, xpath => _config.Selectors.TableOfContents.NovelTitle = xpath, true);
        await TestFieldAsync("Author", "//a[@class='author']/text()", NovelDataInitializer.Attr.Author, xpath => _config.Selectors.TableOfContents.NovelAuthor = xpath, false);
        await TestFieldAsync("Description", "//div[@class='description']/p/text()", NovelDataInitializer.Attr.Description, xpath => _config.Selectors.TableOfContents.NovelDescription = xpath, false);
        await TestFieldAsync("Current Chapter Link", "//*[@id='en-chapters']/li[1]/a", NovelDataInitializer.Attr.CurrentChapter, xpath => _config.Selectors.TableOfContents.LatestChapterLink = xpath, false);
        await TestFieldAsync("Genres", "//div[@class='genres']/a/text()", NovelDataInitializer.Attr.Genres, xpath => _config.Selectors.TableOfContents.NovelGenres = xpath, false);

        TestCompletedStatusSetting();

        await TestFieldAsync("Status", "//span[@class='status']/text()", NovelDataInitializer.Attr.NovelStatus, xpath => _config.Selectors.TableOfContents.NovelStatus = xpath, false);
        await TestFieldAsync("Alternative Names", "//div[@class='alt-names']/text()", NovelDataInitializer.Attr.AlternativeNames, xpath => _config.Selectors.TableOfContents.NovelAlternativeNames = xpath, false);

        await TestNovelRatingFieldAsync();

        await TestThumbnailFieldAsync();
        TestChapterLinksField();

        TestPaginationSettings();
        TestChapterSortOrderSetting();
        TestContentTypeSetting();
    }

    private async Task RetryFailedRequiredFieldsAsync()
    {
        Console.WriteLine($"\n{new string('-', 70)}");
        Console.WriteLine("Retrying Failed Required Fields");
        Console.WriteLine($"{new string('-', 70)}\n");

        if (_titleFailed)
        {
            _titleFailed = false;
            await TestFieldAsync("Title", "//h1[@class='heading']/text()", NovelDataInitializer.Attr.Title, xpath => _config.Selectors.TableOfContents.NovelTitle = xpath, true);
        }

        if (_chapterLinksFailed)
        {
            _chapterLinksFailed = false;
            TestChapterLinksField();
        }

        _requiredFieldsFailed = _titleFailed || _chapterLinksFailed;
    }

    private async Task ModifyFieldsInteractivelyAsync()
    {
        Console.WriteLine($"\n{new string('-', 70)}");
        Console.WriteLine("Modify Fields");
        Console.WriteLine($"{new string('-', 70)}\n");

        Console.WriteLine("Which field would you like to modify?");
        Console.WriteLine("  1. Title");
        Console.WriteLine("  2. Author");
        Console.WriteLine("  3. Description");
        Console.WriteLine("  4. Genres");
        Console.WriteLine("  5. Status");
        Console.WriteLine("  6. Alternative Names");
        Console.WriteLine("  7. Thumbnail");
        Console.WriteLine("  8. Chapter Links");
        Console.WriteLine("  9. Pagination Settings");
        Console.WriteLine(" 10. Content Type");
        Console.WriteLine(" 11. Completed Status");
        Console.WriteLine(" 12. Chapter Sort Order");
        Console.WriteLine(" 13. Novel Rating");
        Console.WriteLine(" 14. Total Ratings");

        if (_chapterHtmlDocument != null)
        {
            Console.WriteLine(" 15. Chapter Title");
            Console.WriteLine(" 16. Chapter Content");
            Console.WriteLine(" 17. Next Chapter Button");
        }

        Console.WriteLine("  0. Done modifying");
        Console.Write("\nChoice: ");

        var input = Console.ReadLine()?.Trim();
        if (!int.TryParse(input, out var choice))
        {
            choice = -1;
        }

        switch (choice)
        {
            case 0:
                break;
            case 1:
                _titleFailed = false;
                await TestFieldAsync("Title", "//h1[@class='heading']/text()", NovelDataInitializer.Attr.Title, xpath => _config.Selectors.TableOfContents.NovelTitle = xpath, true);
                _requiredFieldsFailed = _titleFailed || _chapterLinksFailed;
                await ModifyFieldsInteractivelyAsync(); // Allow modifying more
                break;
            case 2:
                await TestFieldAsync("Author", "//a[@class='author']/text()", NovelDataInitializer.Attr.Author, xpath => _config.Selectors.TableOfContents.NovelAuthor = xpath, false);
                await ModifyFieldsInteractivelyAsync();
                break;
            case 3:
                await TestFieldAsync("Description", "//div[@class='description']/p/text()", NovelDataInitializer.Attr.Description, xpath => _config.Selectors.TableOfContents.NovelDescription = xpath, false);
                await ModifyFieldsInteractivelyAsync();
                break;
            case 4:
                await TestFieldAsync("Genres", "//div[@class='genres']/a/text()", NovelDataInitializer.Attr.Genres, xpath => _config.Selectors.TableOfContents.NovelGenres = xpath, false);
                await ModifyFieldsInteractivelyAsync();
                break;
            case 5:
                await TestFieldAsync("Status", "//span[@class='status']/text()", NovelDataInitializer.Attr.NovelStatus, xpath => _config.Selectors.TableOfContents.NovelStatus = xpath, false);
                await ModifyFieldsInteractivelyAsync();
                break;
            case 6:
                await TestFieldAsync("Alternative Names", "//div[@class='alt-names']/text()", NovelDataInitializer.Attr.AlternativeNames, xpath => _config.Selectors.TableOfContents.NovelAlternativeNames = xpath, false);
                await ModifyFieldsInteractivelyAsync();
                break;
            case 7:
                await TestThumbnailFieldAsync();
                await ModifyFieldsInteractivelyAsync();
                break;
            case 8:
                _chapterLinksFailed = false;
                TestChapterLinksField();
                _requiredFieldsFailed = _titleFailed || _chapterLinksFailed;
                await ModifyFieldsInteractivelyAsync();
                break;
            case 9:
                TestPaginationSettings();
                await ModifyFieldsInteractivelyAsync();
                break;
            case 10:
                TestContentTypeSetting();
                await ModifyFieldsInteractivelyAsync();
                break;
            case 11:
                TestCompletedStatusSetting();
                await ModifyFieldsInteractivelyAsync();
                break;
            case 12:
                TestChapterSortOrderSetting();
                await ModifyFieldsInteractivelyAsync();
                break;
            case 13:
                await TestFieldAsync("Novel Rating", "//span[@class='rating']/text()", NovelDataInitializer.Attr.NovelRating, xpath => _config.Selectors.TableOfContents.NovelRating = xpath, false);
                await ModifyFieldsInteractivelyAsync();
                break;
            case 14:
                await TestFieldAsync("Total Ratings", "//span[@class='rating-count']/text()", NovelDataInitializer.Attr.TotalRatings, xpath => _config.Selectors.TableOfContents.TotalRatings = xpath, false);
                await ModifyFieldsInteractivelyAsync();
                break;
            case 15 when _chapterHtmlDocument != null:
                TestChapterTitle(_chapterHtmlDocument);
                await ModifyFieldsInteractivelyAsync();
                break;
            case 16 when _chapterHtmlDocument != null:
                TestChapterContent(_chapterHtmlDocument);
                await ModifyFieldsInteractivelyAsync();
                break;
            case 17 when _chapterHtmlDocument != null:
                TestNextChapterButtonField(_chapterHtmlDocument);
                await ModifyFieldsInteractivelyAsync();
                break;
            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Invalid choice");
                Console.ResetColor();
                await ModifyFieldsInteractivelyAsync();
                break;
        }
    }

    private async Task TestFieldAsync(string fieldName, string exampleXPath, NovelDataInitializer.Attr attribute, Action<string> setSelectorAction, bool isRequired)
    {
        var fieldVerified = false;

        while (!fieldVerified)
        {
            Console.WriteLine($"\n[{fieldName}]");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Example: {exampleXPath}");
            Console.ResetColor();

            if (!isRequired)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("(Optional) ");
                Console.ResetColor();
            }

            Console.Write("XPath: ");
            var xpath = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(xpath))
            {
                if (isRequired)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Required field - skipped");
                    Console.ResetColor();

                    PromptSeleniumForTocIfNeeded(fieldName);

                    _requiredFieldsFailed = true;

                    if (fieldName.Equals("Title", StringComparison.OrdinalIgnoreCase))
                    {
                        _titleFailed = true;
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("⊘ Skipped");
                    Console.ResetColor();
                }
                return;
            }

            setSelectorAction(xpath);
            var success = await ValidateFieldAsync(attribute);

            Console.Write("\nAre you happy with this result? (y/n/retry, default: y): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();

            switch (response)
            {
                case "y" or "yes":
                    {
                        fieldVerified = true;

                        if (!isRequired || success) continue;
                        _requiredFieldsFailed = true;

                        if (fieldName.Equals("Title", StringComparison.OrdinalIgnoreCase))
                        {
                            _titleFailed = true;
                        }

                        break;
                    }
                case "n":
                case "no":
                case "retry":
                case "r":
                    {
                        if (!success)
                        {
                            PromptSeleniumForTocIfNeeded(fieldName);
                        }

                        setSelectorAction(string.Empty);
                        continue;
                    }
                default:
                    {
                        fieldVerified = true;

                        if (isRequired && !success)
                        {
                            _requiredFieldsFailed = true;

                            if (fieldName.Equals("Title", StringComparison.OrdinalIgnoreCase))
                            {
                                _titleFailed = true;
                            }
                        }

                        break;
                    }
            }
        }
    }

    private void PromptSeleniumForTocIfNeeded(string fieldName)
    {
        Console.WriteLine($"\n⚠ {fieldName} field could not be extracted.");
        Console.WriteLine("If the content is loaded via JavaScript or requires interaction, Selenium may be needed.");
        Console.Write("\nDoes the table of contents require Selenium to load this field? (y/n): ");
        var seleniumResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (seleniumResponse != "y" && seleniumResponse != "yes") return;
        _config.TableOfContentsRequiresSelenium = true;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("✓ Marked site as requiring Selenium for table of contents");
        Console.ResetColor();
    }

    private async Task<bool> ValidateFieldAsync(NovelDataInitializer.Attr attribute)
    {
        try
        {
            var novelDataBuffer = new NovelDataBuffer();
            await TestStrategyInitializer.TestSingleFieldAsync(attribute, novelDataBuffer, _htmlDocument, _scraperData);

            var hasData = attribute switch
            {
                NovelDataInitializer.Attr.Title => !string.IsNullOrEmpty(novelDataBuffer.Title),
                NovelDataInitializer.Attr.Author => !string.IsNullOrEmpty(novelDataBuffer.Author),
                NovelDataInitializer.Attr.Description => novelDataBuffer.Description?.Any() == true,
                NovelDataInitializer.Attr.Genres => novelDataBuffer.Genres?.Any() == true,
                NovelDataInitializer.Attr.NovelStatus => !string.IsNullOrEmpty(novelDataBuffer.NovelStatus),
                NovelDataInitializer.Attr.AlternativeNames => novelDataBuffer.AlternativeNames?.Any() == true,
                NovelDataInitializer.Attr.ThumbnailUrl => !string.IsNullOrEmpty(novelDataBuffer.ThumbnailUrl),
                NovelDataInitializer.Attr.NovelRating => novelDataBuffer.Rating > 0,
                NovelDataInitializer.Attr.TotalRatings => novelDataBuffer.TotalRatings > 0,
                _ => true
            };

            if (!hasData)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ No data found");
                Console.ResetColor();
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Success");
            Console.ResetColor();
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error validating field: {ex.Message}");
            Console.ResetColor();
            Logger.Error($"Field validation error: {ex}");
            return false;
        }
    }

    private async Task TestThumbnailFieldAsync()
    {
        var fieldVerified = false;

        while (!fieldVerified)
        {
            Console.WriteLine($"\n[Thumbnail URL]");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Example: //img[@class='cover']");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("(Optional) ");
            Console.ResetColor();
            Console.Write("XPath: ");

            var xpath = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(xpath))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("⊘ Skipped");
                Console.ResetColor();
                return;
            }

            _config.Selectors.TableOfContents.NovelThumbnailUrl = xpath;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Common attributes: src, data-src, data-lazy");
            Console.ResetColor();
            Console.Write("Attribute name (default: src): ");

            var attribute = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(attribute))
            {
                attribute = "src";
            }

            _config.Selectors.TableOfContents.ThumbnailUrlAttribute = attribute;
            var success = await ValidateFieldAsync(NovelDataInitializer.Attr.ThumbnailUrl);

            Console.Write("\nAre you happy with this result? (y/n/retry, default: y): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();

            switch (response)
            {
                case "y":
                case "yes":
                    fieldVerified = true;
                    break;
                case "n":
                case "no":
                case "retry":
                case "r":
                    {
                        // Prompt about Selenium if field failed
                        if (!success)
                        {
                            PromptSeleniumForTocIfNeeded("Thumbnail");
                        }

                        // Clear the selectors to retry
                        _config.Selectors.TableOfContents.NovelThumbnailUrl = string.Empty;
                        _config.Selectors.TableOfContents.ThumbnailUrlAttribute = string.Empty;
                        continue;
                    }
                default:
                    // Default to accepting the result
                    fieldVerified = true;
                    break;
            }
        }
    }

    private void TestChapterLinksField()
    {
        var linksVerified = false;

        while (!linksVerified)
        {
            Console.WriteLine($"\n[Chapter Links] (Required)");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Example: //ul[@class='chapters']//a/@href");
            Console.ResetColor();
            Console.Write("XPath: ");

            var xpath = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(xpath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Required field - skipped");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n⚠ Chapter links are required for scraping.");
                Console.WriteLine("If you're skipping because links aren't in the HTML, they may be:");
                Console.WriteLine("  1. Loaded via JavaScript (requires Selenium)");
                Console.WriteLine("  2. Loaded via AJAX (check XHR in DevTools Network tab)");
                Console.ResetColor();

                Console.Write("\nWill this site require Selenium to load chapter links? (y/n): ");
                var seleniumResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (seleniumResponse == "y" || seleniumResponse == "yes")
                {
                    _config.TableOfContentsRequiresSelenium = true;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("✓ Marked site as requiring Selenium for table of contents");
                    Console.ResetColor();
                }

                _requiredFieldsFailed = true;
                _chapterLinksFailed = true;
                return;
            }

            _config.Selectors.TableOfContents.ChapterLinks = xpath;

            try
            {
                var nodes = _htmlDocument.DocumentNode.SelectNodes(xpath);

                if (nodes == null || !nodes.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ No chapter links found");
                    Console.ResetColor();

                    Console.Write("\nDo you want to retry with a different XPath? (y/n, default: y): ");
                    var retryResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

                    if (retryResponse == "y" || retryResponse == "yes" || retryResponse == "")
                    {
                        continue;
                    }

                    _requiredFieldsFailed = true;
                    _chapterLinksFailed = true;
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Found {nodes.Count} chapters");
                Console.ResetColor();

                Console.WriteLine("\nFirst 3 chapters:");
                for (var i = 0; i < Math.Min(3, nodes.Count); i++)
                {
                    var value = nodes[i].GetAttributeValue("href", nodes[i].InnerText?.Trim());
                    Console.WriteLine($"  [{i + 1}] {value}");
                }

                if (nodes.Count > 3)
                {
                    Console.WriteLine($"\nLast 3 chapters:");
                    for (var i = Math.Max(0, nodes.Count - 3); i < nodes.Count; i++)
                    {
                        var value = nodes[i].GetAttributeValue("href", nodes[i].InnerText?.Trim());
                        Console.WriteLine($"  [{i + 1}] {value}");
                    }
                }

                Console.Write("\nDo the chapter links look correct? (y/n, default: y): ");
                var verifyResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (verifyResponse == "y" || verifyResponse == "yes" || verifyResponse == "")
                {
                    linksVerified = true;
                    break;
                }

                Console.Write("Do you want to retry with a different XPath, or continue anyway? (retry/continue): ");
                var nextStepResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (nextStepResponse == "retry" || nextStepResponse == "r")
                {
                    continue;
                }

                // User wants to continue despite incorrect links
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n⚠ Chapter links may not be loading correctly.");
                Console.WriteLine("This could be due to:");
                Console.WriteLine("  1. Content loaded via JavaScript (requires Selenium)");
                Console.WriteLine("  2. Content loaded via AJAX (check XHR in DevTools Network tab)");
                Console.ResetColor();
                Console.WriteLine("\nExample: MangaKakalot uses AJAX to load chapter links.");
                Console.WriteLine("You can inspect the Network tab > XHR to find the endpoint.");

                Console.Write("\nWill this site require Selenium to load chapter links? (y/n): ");
                var seleniumResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (seleniumResponse == "y" || seleniumResponse == "yes")
                {
                    _config.TableOfContentsRequiresSelenium = true;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("✓ Marked site as requiring Selenium for table of contents");
                    Console.ResetColor();
                }

                linksVerified = true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Invalid XPath: {ex.Message}");
                Console.ResetColor();

                Console.Write("\nDo you want to retry with a different XPath? (y/n, default: y): ");
                var retryResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (retryResponse == "y" || retryResponse == "yes" || retryResponse == "")
                {
                    continue;
                }

                _requiredFieldsFailed = true;
                _chapterLinksFailed = true;
                return;
            }
        }
    }

    private void TestPaginationSettings()
    {
        Console.WriteLine($"\n[Pagination]");
        Console.Write("Does this site paginate chapter lists? (y/n, default: n): ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();

        _config.HasPagination = response == "y" || response == "yes";

        if (_config.HasPagination)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Example: ?page={0}  or  /page-{0}");
            Console.ResetColor();
            Console.Write("Pagination format: ");
            _config.PaginationType = Console.ReadLine()?.Trim();

            Console.Write("Pagination query partial (e.g., '?page='): ");
            _config.PaginationQueryPartial = Console.ReadLine()?.Trim();

            Console.Write("Chapters per page (default: 50): ");
            var chaptersPerPage = Console.ReadLine()?.Trim();
            _config.ChaptersPerPage = int.TryParse(chaptersPerPage, out int cpp) ? cpp : 50;

            Console.Write("Page offset (0 or 1, default: 1): ");
            var offset = Console.ReadLine()?.Trim();
            _config.PageOffSet = int.TryParse(offset, out int off) ? off : 1;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ No pagination");
            Console.ResetColor();
        }
    }

    private void TestCompletedStatusSetting()
    {
        Console.WriteLine($"\n[Completed Status]");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("What text indicates a completed novel? (e.g., 'Completed', 'Finished')");
        Console.ResetColor();
        Console.Write("Completed status text (or press Enter for null): ");

        var status = Console.ReadLine()?.Trim();
        _config.CompletedStatus = string.IsNullOrEmpty(status) ? null : status.ToLowerInvariant();

        if (string.IsNullOrEmpty(_config.CompletedStatus)) return;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Completed status: '{_config.CompletedStatus}'");
        Console.ResetColor();
    }

    private void TestChapterSortOrderSetting()
    {
        Console.WriteLine($"\n[Chapter Sort Order]");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("How are chapters ordered on this site's table of contents page?");
        Console.ResetColor();
        Console.WriteLine("  1. Ascending (oldest first) (default)");
        Console.WriteLine("  2. Descending (newest first)");
        Console.WriteLine("  3. None (no specific order)");
        Console.Write("Choice (1-3): ");

        var choice = Console.ReadLine()?.Trim();

        _config.ChapterSortOrder = choice switch
        {
            "2" => ChapterSortOrder.Descending,
            "3" => ChapterSortOrder.None,
            _ => ChapterSortOrder.Ascending
        };

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Chapter sort order: {_config.ChapterSortOrder}");
        Console.ResetColor();
    }

    private async Task TestNovelRatingFieldAsync()
    {
        Console.WriteLine($"\n[Novel Rating]");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("XPath for the novel's rating score (e.g., 4.5 out of 5)");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("(Optional) ");
        Console.ResetColor();
        Console.Write("XPath: ");

        var xpath = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(xpath))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("⊘ Skipped");
            Console.ResetColor();
            return;
        }

        _config.Selectors.TableOfContents.NovelRating = xpath;
        await ValidateFieldAsync(NovelDataInitializer.Attr.NovelRating);

        await TestTotalRatingsFieldAsync();
    }

    private async Task TestTotalRatingsFieldAsync()
    {
        Console.WriteLine($"\n[Total Ratings]");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("XPath for the total number of ratings/votes");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("(Optional) ");
        Console.ResetColor();
        Console.Write("XPath: ");

        var xpath = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(xpath))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("⊘ Skipped");
            Console.ResetColor();
            return;
        }

        _config.Selectors.TableOfContents.TotalRatings = xpath;
        await ValidateFieldAsync(NovelDataInitializer.Attr.TotalRatings);
    }

    private void TestContentTypeSetting()
    {
        Console.WriteLine($"\n[Content Type]");
        Console.Write("Does this site have images for chapter content (manga/comic)? (y/n, default: n): ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();

        _config.HasImagesForChapterContent = response == "y" || response == "yes";

        if (_config.HasImagesForChapterContent)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Image-based content (manga/comic)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Text-based content (novel)");
        }

        Console.ResetColor();
    }

    private async Task TestChapterContentInteractivelyAsync()
    {
        Console.WriteLine($"\n{new string('-', 70)}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Chapter Content Testing (Optional)");
        Console.ResetColor();
        Console.WriteLine($"{new string('-', 70)}\n");

        Console.WriteLine("Do you want to test chapter content selectors?");
        Console.WriteLine("(Chapter Title and Chapter Content)");
        Console.Write("Test chapter content? (y/n, default: n): ");
        var response = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (response != "y" && response != "yes")
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("⊘ Chapter content testing skipped");
            Console.ResetColor();
            return;
        }

        Console.WriteLine("\nProvide a chapter page URL to test on:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Example: https://www.wuxiaworld.com/novel/keyboard-immortal/ki-chapter-1");
        Console.ResetColor();
        Console.Write("Chapter URL: ");
        var chapterUrl = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(chapterUrl) || !Uri.TryCreate(chapterUrl, UriKind.Absolute, out var chapterUri))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Invalid URL - skipping chapter content testing");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"\nLoading chapter page: {chapterUrl}");
        var (chapterHtml, updatedUri, statusCode, cloudflareDetected) = await TestLoadHtmlAsync(chapterUri);

        if (chapterHtml == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Failed to load chapter page (Status: {statusCode})");
            if (cloudflareDetected)
            {
                Console.WriteLine("✗ Cloudflare protection detected");
            }
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Chapter page loaded successfully (Status: {statusCode})\n");
        Console.ResetColor();

        _chapterHtmlDocument = chapterHtml;

        TestChapterTitle(chapterHtml);
        TestChapterContent(chapterHtml);
        TestNextChapterButtonField(chapterHtml);
    }

    private void TestChapterTitle(HtmlDocument chapterHtml)
    {
        var fieldVerified = false;

        while (!fieldVerified)
        {
            Console.WriteLine($"\n[Chapter Title]");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Example: //h1[@class='chapter-title']/text()");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("(Optional) ");
            Console.ResetColor();
            Console.Write("XPath: ");

            var xpath = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(xpath))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("⊘ Skipped");
                Console.ResetColor();
                return;
            }

            _config.Selectors.ChapterTitle = xpath;
            var success = false;

            try
            {
                var titleNode = chapterHtml.DocumentNode.SelectSingleNode(xpath);

                if (titleNode == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ No element found with this XPath");
                    Console.ResetColor();
                }
                else
                {
                    var title = titleNode.InnerText?.Trim();

                    if (string.IsNullOrEmpty(title))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("✗ No title text found");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Chapter Title: {title}");
                        Console.WriteLine("✓ Success");
                        Console.ResetColor();
                        success = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error: {ex.Message}");
                Console.ResetColor();
            }

            // Ask user if they're happy with the result
            Console.Write("\nAre you happy with this result? (y/n/retry, default: y): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();

            switch (response)
            {
                case "y":
                case "yes":
                    fieldVerified = true;
                    break;
                case "n":
                case "no":
                case "retry":
                case "r":
                    {
                        // Prompt about Selenium if field failed
                        if (!success)
                        {
                            PromptSeleniumForChapterContent("Chapter Title");
                        }

                        // Clear the selector to retry
                        _config.Selectors.ChapterTitle = string.Empty;
                        continue;
                    }
                default:
                    // Default to accepting the result
                    fieldVerified = true;
                    break;
            }
        }
    }

    private void PromptSeleniumForChapterContent(string fieldName)
    {
        Console.WriteLine($"\n⚠ {fieldName} field could not be extracted.");
        Console.WriteLine("If the content is loaded via JavaScript or requires interaction, Selenium may be needed.");
        Console.Write("\nDoes the chapter content require Selenium to load this field? (y/n): ");
        var seleniumResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (seleniumResponse != "y" && seleniumResponse != "yes") return;
        _config.ChapterContentRequiresSelenium = true;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("✓ Marked site as requiring Selenium for chapter content");
        Console.ResetColor();
    }

    private void TestChapterContent(HtmlDocument chapterHtml)
    {
        var fieldVerified = false;

        while (!fieldVerified)
        {
            Console.WriteLine($"\n[Chapter Content]");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            var success = false;

            if (_config.HasImagesForChapterContent)
            {
                Console.WriteLine("Example (images): //div[@id='chapter-content']//img");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("(Optional) ");
                Console.ResetColor();
                Console.Write("XPath: ");

                var xpath = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(xpath))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("⊘ Skipped");
                    Console.ResetColor();
                    return;
                }

                _config.Selectors.ChapterContent = xpath;

                try
                {
                    var imageNodes = chapterHtml.DocumentNode.SelectNodes(xpath);

                    if (imageNodes == null || !imageNodes.Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("✗ No images found");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Found {imageNodes.Count} images");
                        Console.ResetColor();

                        Console.WriteLine("\nFirst 3 image sources:");
                        for (var i = 0; i < Math.Min(3, imageNodes.Count); i++)
                        {
                            var src = imageNodes[i].GetAttributeValue("src", "(no src attribute)");
                            Console.WriteLine($"  [{i + 1}] {src}");
                        }

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("\nCommon image attributes: src, data-src, data-url");
                        Console.ResetColor();
                        Console.Write("Image URL attribute (default: src): ");
                        var attr = Console.ReadLine()?.Trim();
                        _config.Selectors.ChapterContentImageUrlAttribute = string.IsNullOrEmpty(attr) ? "src" : attr;
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Error: {ex.Message}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine("Example (text): //div[@id='chapter-content']/p");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("(Optional) ");
                Console.ResetColor();
                Console.Write("XPath: ");

                var xpath = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(xpath))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("⊘ Skipped");
                    Console.ResetColor();
                    return;
                }

                _config.Selectors.ChapterContent = xpath;

                try
                {
                    var contentNodes = chapterHtml.DocumentNode.SelectNodes(xpath);

                    if (contentNodes == null || !contentNodes.Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("✗ No content found");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Found {contentNodes.Count} paragraphs");
                        Console.ResetColor();

                        Console.WriteLine("\nFirst 3 paragraphs:");
                        for (int i = 0; i < Math.Min(3, contentNodes.Count); i++)
                        {
                            var text = contentNodes[i].InnerText?.Trim();
                            var preview = text?.Length > 60 ? text.Substring(0, 60) + "..." : text;
                            Console.WriteLine($"  [{i + 1}] {preview}");
                        }
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Error: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.Write("\nAre you happy with this result? (y/n/retry, default: y): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();

            switch (response)
            {
                case "y":
                case "yes":
                    fieldVerified = true;
                    break;
                case "n":
                case "no":
                case "retry":
                case "r":
                    {
                        // Prompt about Selenium if field failed
                        if (!success)
                        {
                            PromptSeleniumForChapterContent("Chapter Content");
                        }

                        // Clear the selectors to retry
                        _config.Selectors.ChapterContent = string.Empty;
                        _config.Selectors.ChapterContentImageUrlAttribute = string.Empty;
                        continue;
                    }
                default:
                    // Default to accepting the result
                    fieldVerified = true;
                    break;
            }
        }
    }

    private void TestNextChapterButtonField(HtmlDocument chapterHtml)
    {
        var fieldVerified = false;

        while (!fieldVerified)
        {
            Console.WriteLine($"\n[Next Chapter Button]");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("XPath for the 'next chapter' navigation link/button.");
            Console.WriteLine("Used for SPA-style sites that navigate via next-chapter buttons instead of TOC links.");
            Console.WriteLine("Example: //a[@class='next-chap']");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("(Optional) ");
            Console.ResetColor();
            Console.Write("XPath: ");

            var xpath = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(xpath))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("⊘ Skipped");
                Console.ResetColor();
                return;
            }

            _config.Selectors.NextChapterButton = xpath;

            try
            {
                var node = chapterHtml.DocumentNode.SelectSingleNode(xpath);

                if (node == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ No element found with this XPath");
                    Console.ResetColor();
                }
                else
                {
                    var text = node.InnerText?.Trim();
                    var href = node.GetAttributeValue("href", "(no href)");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Found: text=\"{text}\", href=\"{href}\"");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error: {ex.Message}");
                Console.ResetColor();
            }

            Console.Write("\nAre you happy with this result? (y/n/retry, default: y): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();

            switch (response)
            {
                case "y":
                case "yes":
                    fieldVerified = true;
                    break;
                case "n":
                case "no":
                case "retry":
                case "r":
                    _config.Selectors.NextChapterButton = string.Empty;
                    continue;
                default:
                    fieldVerified = true;
                    break;
            }
        }
    }

    private void GenerateAndDisplayConfig()
    {
        Console.WriteLine($"\n{new string('=', 70)}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Configuration Generated");
        Console.ResetColor();
        Console.WriteLine($"{new string('=', 70)}\n");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        var json = JsonSerializer.Serialize(_config, options);

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(json);
        Console.ResetColor();

        try
        {
            TextCopy.ClipboardService.SetText(json);
            Console.WriteLine($"\n{new string('-', 70)}");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Configuration copied to clipboard");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Could not copy to clipboard: {ex.Message}");
            Console.ResetColor();
        }

        SaveConfigToFile(json);

        Console.WriteLine($"\nNext steps:");
        Console.WriteLine("1. Review the configuration above");
        Console.WriteLine("2. Paste into appsettings.json under \"SiteConfigurations\"");
        Console.WriteLine("3. Create a new strategy class inheriting from ScraperStrategy");
        Console.WriteLine("4. Test with a real download");
        Console.WriteLine($"{new string('=', 70)}\n");
    }

    private void SaveConfigToFile(string json)
    {
        try
        {
            var configDir = Path.Combine(Directory.GetCurrentDirectory(), "test-configs");
            Directory.CreateDirectory(configDir);

            var fileName = $"{_config.Name.ToLowerInvariant()}-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
            var filePath = Path.Combine(configDir, fileName);

            File.WriteAllText(filePath, json);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Saved to: {filePath}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Could not copy to clipboard: {ex.Message}");
            Console.ResetColor();
            Logger.Warn($"Failed to save config to file: {ex}");
        }
    }

    public async Task<bool> TestSingleFieldAsync(Uri testUri, string fieldName, string xpath, bool useSelenium = false, bool headless = true)
    {
        Console.WriteLine($"\n{new string('=', 70)}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Testing Field: {fieldName}");
        Console.ResetColor();
        Console.WriteLine($"{new string('=', 70)}");
        Console.WriteLine($"URL: {testUri}");
        Console.WriteLine($"XPath: {xpath}");
        if (useSelenium)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Mode: Selenium (Headless: {headless})");
            Console.ResetColor();
        }
        Console.WriteLine();

        HtmlDocument htmlDocument;
        int statusCode = 200;
        bool cloudflareDetected = false;

        try
        {
            if (useSelenium)
            {
                // Use Selenium to load the page
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("🌐 Loading page with Selenium...");
                Console.ResetColor();

                var (seleniumDoc, _) = await LoadHtmlWithSeleniumAsync(testUri, xpath, fieldName, headless);

                if (seleniumDoc == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Failed to load page using Selenium");
                    Console.ResetColor();
                    return false;
                }

                htmlDocument = seleniumDoc;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Page loaded successfully using Selenium\n");
                Console.ResetColor();
            }
            else
            {
                // Existing HTTP-based loading
                var (httpDoc, updatedUri, status, cloudflare) = await TestLoadHtmlAsync(testUri);
                htmlDocument = httpDoc;
                statusCode = status;
                cloudflareDetected = cloudflare;

                if (htmlDocument == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ Failed to load page (Status: {statusCode})");
                    if (cloudflareDetected)
                    {
                        Console.WriteLine("✗ Cloudflare protection detected - try using --use-selenium flag");
                    }
                    Console.ResetColor();
                    return false;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Page loaded successfully (Status: {statusCode})\n");
                Console.ResetColor();
            }

            var config = new SiteConfiguration
            {
                Selectors = new Selectors
                {
                    TableOfContents = new TableOfContentsSelectors()
                }
            };

            var scraperData = new ScraperData
            {
                SiteConfig = config,
                SiteTableOfContents = testUri,
                BaseUri = new Uri(testUri.GetLeftPart(UriPartial.Authority)),
                HttpClientFactory = _httpClientFactory
            };

            var fieldUpper = fieldName.ToUpperInvariant();
            var success = fieldUpper switch
            {
                "TITLE" => await TestSpecificField(NovelDataInitializer.Attr.Title, xpath, htmlDocument, scraperData, config, s => config.Selectors.TableOfContents.NovelTitle = s),
                "AUTHOR" => await TestSpecificField(NovelDataInitializer.Attr.Author, xpath, htmlDocument, scraperData, config, s => config.Selectors.TableOfContents.NovelAuthor = s),
                "DESCRIPTION" => await TestSpecificField(NovelDataInitializer.Attr.Description, xpath, htmlDocument, scraperData, config, s => config.Selectors.TableOfContents.NovelDescription = s),
                "GENRES" => await TestSpecificField(NovelDataInitializer.Attr.Genres, xpath, htmlDocument, scraperData, config, s => config.Selectors.TableOfContents.NovelGenres = s),
                "STATUS" => await TestSpecificField(NovelDataInitializer.Attr.NovelStatus, xpath, htmlDocument, scraperData, config, s => config.Selectors.TableOfContents.NovelStatus = s),
                "ALTERNATIVENAMES" => await TestSpecificField(NovelDataInitializer.Attr.AlternativeNames, xpath, htmlDocument, scraperData, config, s => config.Selectors.TableOfContents.NovelAlternativeNames = s),
                "THUMBNAIL" => await TestSpecificField(NovelDataInitializer.Attr.ThumbnailUrl, xpath, htmlDocument, scraperData, config, s => config.Selectors.TableOfContents.NovelThumbnailUrl = s),
                "CHAPTERLINKS" => TestChapterLinksField(xpath, htmlDocument),
                "CHAPTERTITLE" => TestChapterTitleField(xpath, htmlDocument),
                "CHAPTERCONTENT" => TestChapterContentField(xpath, htmlDocument),
                "NOVELRATING" => await TestSpecificField(NovelDataInitializer.Attr.NovelRating, xpath, htmlDocument, scraperData, config, s => config.Selectors.TableOfContents.NovelRating = s),
                "TOTALRATINGS" => await TestSpecificField(NovelDataInitializer.Attr.TotalRatings, xpath, htmlDocument, scraperData, config, s => config.Selectors.TableOfContents.TotalRatings = s),
                "CHAPTERTITLEINTOC" => TestChapterTitleInTocField(xpath, htmlDocument),
                "NEXTCHAPTERBUTTON" => TestNextChapterButtonSingleField(xpath, htmlDocument),
                _ => HandleUnknownField(fieldName)
            };

            Console.WriteLine($"\n{new string('=', 70)}\n");
            return success;
        }
        finally
        {
            // Clean up any Selenium drivers that were created
            if (useSelenium)
            {
                _driverFactory.DisposeAllDrivers();
            }
        }
    }

    private async Task<(HtmlDocument? Document, string? PageSource)> LoadHtmlWithSeleniumAsync(
        Uri testUri,
        string xpath,
        string fieldName,
        bool headless)
    {
        IWebDriver? driver = null;

        try
        {
            Logger.Info($"Creating Selenium driver (headless: {headless})...");
            driver = await _driverFactory.CreateDriverAsync(testUri.ToString(), isHeadless: headless);

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));

            // Special handling for NovelBin/NovLove sites - click chapter tab
            if (testUri.Host.Contains("novelbin") || testUri.Host.Contains("novlove"))
            {
                try
                {
                    Logger.Info("NovelBin/NovLove site detected - attempting to click 'Chapter List' tab...");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("🔍 Detected NovelBin/NovLove site - clicking 'Chapter List' tab...");
                    Console.ResetColor();

                    var chapterTab = wait.Until(ExpectedConditions.ElementToBeClickable(
                        By.XPath("//a[@id='tab-chapters-title'][@role='tab']")
                    ));
                    chapterTab.Click();

                    Logger.Info("Successfully clicked 'Chapter List' tab");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Chapter tab clicked");
                    Console.ResetColor();

                    // Wait for initial chapters to appear
                    try
                    {
                        wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(
                            By.XPath("//ul[@class='list-chapter']/li/a")
                        ));
                        Logger.Info("Initial chapter links are visible");
                    }
                    catch (WebDriverTimeoutException)
                    {
                        Logger.Warn("Explicit wait for chapters timed out");
                    }

                    // Wait for lazy-loaded chapters to finish loading
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("⏳ Waiting for all chapters to lazy load...");
                    Console.ResetColor();
                    Logger.Info("Waiting for all chapters to lazy load...");

                    var previousCount = 0;
                    var stableCount = 0;
                    var maxWaitIterations = 30; // 30 seconds max wait

                    for (int i = 0; i < maxWaitIterations; i++)
                    {
                        await Task.Delay(1000); // Wait 1 second between checks

                        var currentChapters = driver.FindElements(By.XPath("//ul[@class='list-chapter']/li/a"));
                        var currentCount = currentChapters.Count;

                        if (currentCount == previousCount)
                        {
                            stableCount++;
                            // If count hasn't changed for 3 consecutive checks, assume loading is complete
                            if (stableCount >= 3)
                            {
                                Logger.Info($"Chapter count stabilized at {currentCount} chapters");
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"✓ All chapters loaded ({currentCount} total)");
                                Console.ResetColor();
                                break;
                            }
                        }
                        else
                        {
                            Logger.Info($"Chapters loading: {currentCount} found...");
                            Console.WriteLine($"  Loading: {currentCount} chapters found...");
                            stableCount = 0;
                            previousCount = currentCount;
                        }
                    }

                    Logger.Info("Chapter lazy loading complete");
                }
                catch (WebDriverTimeoutException)
                {
                    Logger.Warn("Could not find 'Chapter List' tab - it might already be active");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠ Chapter tab not found - may already be active");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Unexpected error while clicking chapter tab: {ex.Message}");
                }
            }

            // Wait for the requested XPath to be present
            Logger.Info($"Waiting for XPath to be present: {xpath}");
            wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(xpath)));

            var htmlDocument = new HtmlDocument();
            var pageSource = driver.PageSource;
            htmlDocument.LoadHtml(pageSource);

            return (htmlDocument, pageSource);
        }
        catch (WebDriverTimeoutException ex)
        {
            Logger.Error($"Selenium timeout: Unable to find element with XPath '{xpath}' after 60 seconds. Error: {ex.Message}");
            return (null, null);
        }
        catch (Exception ex)
        {
            Logger.Error($"Selenium error: {ex}");
            return (null, null);
        }
        finally
        {
            // Driver will be cleaned up by DisposeAllDrivers in the calling method's finally block
        }
    }

    private static async Task<bool> TestSpecificField(NovelDataInitializer.Attr attribute, string xpath, HtmlDocument htmlDocument, ScraperData scraperData, SiteConfiguration config, Action<string> setSelector)
    {
        setSelector(xpath);

        try
        {
            var novelDataBuffer = new NovelDataBuffer();
            await TestStrategyInitializer.TestSingleFieldAsync(attribute, novelDataBuffer, htmlDocument, scraperData);

            var hasData = attribute switch
            {
                NovelDataInitializer.Attr.Title => !string.IsNullOrEmpty(novelDataBuffer.Title),
                NovelDataInitializer.Attr.Author => !string.IsNullOrEmpty(novelDataBuffer.Author),
                NovelDataInitializer.Attr.Description => novelDataBuffer.Description?.Any() == true,
                NovelDataInitializer.Attr.Genres => novelDataBuffer.Genres?.Any() == true,
                NovelDataInitializer.Attr.NovelStatus => !string.IsNullOrEmpty(novelDataBuffer.NovelStatus),
                NovelDataInitializer.Attr.AlternativeNames => novelDataBuffer.AlternativeNames?.Any() == true,
                NovelDataInitializer.Attr.ThumbnailUrl => !string.IsNullOrEmpty(novelDataBuffer.ThumbnailUrl),
                NovelDataInitializer.Attr.NovelRating => novelDataBuffer.Rating > 0,
                NovelDataInitializer.Attr.TotalRatings => novelDataBuffer.TotalRatings > 0,
                _ => false
            };

            if (!hasData)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ No data found with this XPath");
                Console.ResetColor();
            }

            return hasData;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static bool TestChapterLinksField(string xpath, HtmlDocument htmlDocument)
    {
        try
        {
            var nodes = htmlDocument.DocumentNode.SelectNodes(xpath);

            if (nodes == null || !nodes.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ No chapter links found");
                Console.ResetColor();
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Found {nodes.Count} chapters");
            Console.ResetColor();

            Console.WriteLine("\nFirst 5 chapters:");
            for (var i = 0; i < Math.Min(5, nodes.Count); i++)
            {
                var value = nodes[i].GetAttributeValue("href", nodes[i].InnerText?.Trim());
                Console.WriteLine($"  [{i + 1}] {value}");
            }

            if (nodes.Count <= 5) return true;
            Console.WriteLine($"\nLast 5 chapters:");
            for (int i = Math.Max(0, nodes.Count - 5); i < nodes.Count; i++)
            {
                var value = nodes[i].GetAttributeValue("href", nodes[i].InnerText?.Trim());
                Console.WriteLine($"  [{i + 1}] {value}");
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Invalid XPath: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private bool TestChapterTitleField(string xpath, HtmlDocument htmlDocument)
    {
        try
        {
            var titleNode = htmlDocument.DocumentNode.SelectSingleNode(xpath);

            if (titleNode == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ No element found with this XPath");
                Console.ResetColor();
                return false;
            }

            var title = titleNode.InnerText?.Trim();

            if (string.IsNullOrEmpty(title))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ No title text found");
                Console.ResetColor();
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Chapter Title: {title}");
            Console.WriteLine("✓ Success");
            Console.ResetColor();
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static bool TestChapterContentField(string xpath, HtmlDocument htmlDocument)
    {
        try
        {
            var nodes = htmlDocument.DocumentNode.SelectNodes(xpath);

            if (nodes == null || !nodes.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ No content found");
                Console.ResetColor();
                return false;
            }

            var isImageContent = nodes.First().Name.ToLowerInvariant() == "img";

            if (isImageContent)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ Found {nodes.Count} images");
                Console.ResetColor();

                Console.WriteLine("\nFirst 5 image sources:");
                for (int i = 0; i < Math.Min(5, nodes.Count); i++)
                {
                    var src = nodes[i].GetAttributeValue("src", "(no src attribute)");
                    Console.WriteLine($"  [{i + 1}] {src}");
                }
                return true;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Found {nodes.Count} content elements");
            Console.ResetColor();

            Console.WriteLine("\nFirst 5 content elements:");
            for (var i = 0; i < Math.Min(5, nodes.Count); i++)
            {
                var text = nodes[i].InnerText?.Trim();
                var preview = text?.Length > 80 ? text.Substring(0, 80) + "..." : text;
                Console.WriteLine($"  [{i + 1}] {preview}");
            }
            Console.WriteLine("\nLast content element:");
            var lastText = nodes.Last().InnerText?.Trim();
            var lastPreview = lastText?.Length > 80 ? lastText.Substring(0, 80) + "..." : lastText;
            Console.WriteLine($"  [{nodes.Count}] {lastPreview}");
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static bool TestChapterTitleInTocField(string xpath, HtmlDocument htmlDocument)
    {
        try
        {
            // ChapterTitleInToc uses a relative XPath evaluated against each chapter link node.
            // First, find chapter link nodes to test against.
            var chapterLinkNodes = htmlDocument.DocumentNode.SelectNodes("//a[contains(@href, 'chapter') or contains(@href, 'ch')]");

            if (chapterLinkNodes == null || !chapterLinkNodes.Any())
            {
                // Fallback: try all anchor tags
                chapterLinkNodes = htmlDocument.DocumentNode.SelectNodes("//a[@href]");
            }

            if (chapterLinkNodes == null || !chapterLinkNodes.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ No chapter link nodes found to test relative XPath against");
                Console.ResetColor();
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Testing relative XPath against {chapterLinkNodes.Count} link nodes...");
            Console.ResetColor();

            var foundCount = 0;
            var sampleResults = new List<string>();

            foreach (var linkNode in chapterLinkNodes.Take(10))
            {
                var titleNode = linkNode.SelectSingleNode(xpath);
                if (titleNode == null) continue;

                foundCount++;
                var text = titleNode.InnerText?.Trim();
                if (!string.IsNullOrEmpty(text) && sampleResults.Count < 5)
                {
                    sampleResults.Add(text);
                }
            }

            if (foundCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Relative XPath matched no elements on any link node");
                Console.ResetColor();

                // Show fallback: test as absolute XPath
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\nTrying as absolute XPath for reference:");
                Console.ResetColor();
                var absoluteNodes = htmlDocument.DocumentNode.SelectNodes(xpath);
                if (absoluteNodes != null && absoluteNodes.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  Found {absoluteNodes.Count} nodes as absolute XPath");
                    for (var i = 0; i < Math.Min(3, absoluteNodes.Count); i++)
                    {
                        Console.WriteLine($"  [{i + 1}] {absoluteNodes[i].InnerText?.Trim()}");
                    }
                    Console.ResetColor();
                    Console.WriteLine("  Note: ChapterTitleInToc expects a relative XPath (evaluated per chapter link)");
                }
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Matched on {foundCount}/{Math.Min(10, chapterLinkNodes.Count)} tested links");
            Console.ResetColor();

            if (sampleResults.Any())
            {
                Console.WriteLine("\nSample titles found:");
                for (var i = 0; i < sampleResults.Count; i++)
                {
                    var preview = sampleResults[i].Length > 80 ? sampleResults[i].Substring(0, 80) + "..." : sampleResults[i];
                    Console.WriteLine($"  [{i + 1}] {preview}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static bool TestNextChapterButtonSingleField(string xpath, HtmlDocument htmlDocument)
    {
        try
        {
            var node = htmlDocument.DocumentNode.SelectSingleNode(xpath);

            if (node == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ No element found with this XPath");
                Console.ResetColor();
                return false;
            }

            var text = node.InnerText?.Trim();
            var href = node.GetAttributeValue("href", "(no href)");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Found next chapter button");
            Console.ResetColor();
            Console.WriteLine($"  Text: {text}");
            Console.WriteLine($"  Href: {href}");

            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static bool HandleUnknownField(string fieldName)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"✗ Unknown field: {fieldName}");
        Console.ResetColor();
        Console.WriteLine("\nTable of Contents fields (use TOC URL):");
        Console.WriteLine("  - Title");
        Console.WriteLine("  - Author");
        Console.WriteLine("  - Description");
        Console.WriteLine("  - Genres");
        Console.WriteLine("  - Status");
        Console.WriteLine("  - AlternativeNames");
        Console.WriteLine("  - Thumbnail");
        Console.WriteLine("  - ChapterLinks");
        Console.WriteLine("  - NovelRating");
        Console.WriteLine("  - TotalRatings");
        Console.WriteLine("  - ChapterTitleInToc  (uses relative XPath evaluated per chapter link)");
        Console.WriteLine("\nChapter fields (use chapter URL):");
        Console.WriteLine("  - ChapterTitle");
        Console.WriteLine("  - ChapterContent");
        Console.WriteLine("  - NextChapterButton");
        return false;
    }

    private (string fieldName, bool success) ValidateChapterLinks(string fieldName, HtmlDocument htmlDocument, string? xpath)
    {
        if (string.IsNullOrEmpty(xpath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ {fieldName}: Required field not configured");
            Console.ResetColor();
            return (fieldName, false);
        }

        try
        {
            var nodes = htmlDocument.DocumentNode.SelectNodes(xpath);

            if (nodes == null || !nodes.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ✗ {fieldName}: No chapters found");
                Console.ResetColor();
                return (fieldName, false);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ {fieldName}: Found {nodes.Count} chapters");
            Console.ResetColor();
            return (fieldName, true);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ {fieldName}: Error - {ex.Message}");
            Console.ResetColor();
            return (fieldName, false);
        }
    }

    /// <summary>
    /// Tests loading HTML with a single attempt (no retries) for faster testing.
    /// </summary>
    public async Task<(HtmlDocument? document, Uri updatedUri, int statusCode, bool cloudflareDetected)> TestLoadHtmlAsync(Uri uri)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            var userAgent = GetNextUserAgent();

            requestMessage.Headers.Add("User-Agent", userAgent);
            requestMessage.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            requestMessage.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            requestMessage.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            requestMessage.Headers.Add("DNT", "1");
            requestMessage.Headers.Add("Connection", "keep-alive");
            requestMessage.Headers.Add("Upgrade-Insecure-Requests", "1");
            requestMessage.Headers.Add("Sec-Fetch-Dest", "document");
            requestMessage.Headers.Add("Sec-Fetch-Mode", "navigate");
            requestMessage.Headers.Add("Sec-Fetch-Site", "none");
            requestMessage.Headers.Add("Sec-Fetch-User", "?1");
            requestMessage.Headers.Add("Cache-Control", "max-age=0");

            requestMessage.Options.Set(new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"), TimeSpan.FromSeconds(10));

            var response = await client.SendAsync(requestMessage);
            var statusCode = (int)response.StatusCode;

            var content = await response.Content.ReadAsStringAsync();
            var isCloudflareDetected = DetectCloudflare(response, content, statusCode);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"[TEST] Request failed to {uri} - Status: {statusCode}");
                if (isCloudflareDetected)
                {
                    Logger.Warn($"[TEST] Cloudflare protection detected");
                }
                return (null, uri, statusCode, isCloudflareDetected);
            }

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);

            var canonicalNode = htmlDocument.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
            if (canonicalNode == null) return (htmlDocument, uri, statusCode, isCloudflareDetected);
            var canonicalUrl = canonicalNode.Attributes["href"]?.Value;
            if (string.IsNullOrEmpty(canonicalUrl) || canonicalUrl == uri.ToString())
                return (htmlDocument, uri, statusCode, isCloudflareDetected);
            Logger.Debug($"[TEST] Canonical URL detected: {canonicalUrl}");
            uri = new Uri(canonicalUrl);

            return (htmlDocument, uri, statusCode, isCloudflareDetected);
        }
        catch (HttpRequestException ex)
        {
            Logger.Error($"[TEST] HTTP error: {ex.Message}");
            return (null, uri, 0, false);
        }
        catch (Exception ex)
        {
            Logger.Error($"[TEST] Error during test: {ex.Message}");
            return (null, uri, 0, false);
        }
    }

    private static bool DetectCloudflare(HttpResponseMessage response, string content, int statusCode)
    {
        if (!response.Headers.Contains("CF-RAY") &&
            !response.Headers.Contains("cf-ray") &&
            !response.Headers.Contains("CF-Cache-Status")) return false;
        if (statusCode is 403 or 503)
        {
            return true;
        }

        if (string.IsNullOrEmpty(content)) return false;
        return content.Contains("cf-browser-verification") ||
               content.Contains("cf_chl_opt") ||
               content.Contains("Checking your browser") ||
               content.Contains("Just a moment") ||
               content.Contains("ray ID") && statusCode != 200;
    }

    private static string GetNextUserAgent()
    {
        var userAgents = new List<string>
            {
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
            };
        return userAgents[new Random().Next(userAgents.Count)];
    }

    public async Task ValidateConfigAsync(SiteConfiguration siteConfig, Uri testUri)
    {
        Console.WriteLine($"\n{new string('=', 70)}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Validating Configuration: {siteConfig.Name}");
        Console.ResetColor();
        Console.WriteLine($"{new string('=', 70)}\n");

        Console.WriteLine($"Test URL: {testUri}\n");

        var (htmlDocument, updatedUri, statusCode, cloudflareDetected) = await TestLoadHtmlAsync(testUri);

        if (htmlDocument == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Failed to load page (Status: {statusCode})");
            if (cloudflareDetected)
            {
                Console.WriteLine("✗ Cloudflare protection detected");
            }
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Page loaded successfully (Status: {statusCode})\n");
        Console.ResetColor();

        var scraperData = new ScraperData
        {
            SiteConfig = siteConfig,
            SiteTableOfContents = updatedUri,
            BaseUri = new Uri(updatedUri.GetLeftPart(UriPartial.Authority)),
            HttpClientFactory = _httpClientFactory
        };

        Console.WriteLine("Validating Selectors.TableOfContents...\n");

        var validationResults = new List<(string fieldName, bool success)>();

        Console.WriteLine($"Title:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelTitle))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelTitle}");
            Console.ResetColor();
        }
        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Title",
            NovelDataInitializer.Attr.Title,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelTitle)));

        Console.WriteLine($"Author:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelAuthor))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelAuthor}");
            Console.ResetColor();
        }
        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Author",
            NovelDataInitializer.Attr.Author,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelAuthor)));

        Console.WriteLine($"Description:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelDescription))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelDescription}");
            Console.ResetColor();
        }
        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Description",
            NovelDataInitializer.Attr.Description,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelDescription)));

        Console.WriteLine($"Genres:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelGenres))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelGenres}");
            Console.ResetColor();
        }
        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Genres",
            NovelDataInitializer.Attr.Genres,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelGenres)));

        Console.WriteLine($"Status:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelStatus))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelStatus}");
            Console.ResetColor();
        }
        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Status",
            NovelDataInitializer.Attr.NovelStatus,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelStatus)));

        Console.WriteLine($"Alternative Names:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelAlternativeNames))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelAlternativeNames}");
            Console.ResetColor();
        }
        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Alternative Names",
            NovelDataInitializer.Attr.AlternativeNames,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelAlternativeNames)));

        Console.WriteLine($"Thumbnail:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelThumbnailUrl))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelThumbnailUrl}");
            Console.ResetColor();
        }
        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Thumbnail",
            NovelDataInitializer.Attr.ThumbnailUrl,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelThumbnailUrl)));

        // Validate Chapter Links (REQUIRED)
        Console.WriteLine($"Chapter Links:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.ChapterLinks))
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.ChapterLinks}");
            Console.ResetColor();
        }
        validationResults.Add(ValidateChapterLinks("Chapter Links", htmlDocument, siteConfig.Selectors.TableOfContents.ChapterLinks));

        Console.WriteLine($"\n{new string('=', 70)}");
        Console.WriteLine("Validation Summary:");
        Console.WriteLine($"{new string('=', 70)}\n");

        var successCount = validationResults.Count(r => r.success);
        var totalCount = validationResults.Count;

        if (successCount == totalCount)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ All selectors validated successfully ({successCount}/{totalCount})");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Some selectors failed validation ({successCount}/{totalCount} passed)");
        }
        Console.ResetColor();
        Console.WriteLine($"\n{new string('=', 70)}\n");
    }

    public override Task<NovelDataBuffer> ScrapeAsync()
    {
        throw new NotImplementedException("TestStrategy does not implement ScrapeAsync");
    }

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
    {
        throw new NotImplementedException("TestStrategy does not implement FetchNovelDataFromTableOfContents");
    }
}
