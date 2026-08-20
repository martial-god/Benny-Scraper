using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Attr = BennyScraper.BusinessLogic.Scrapers.Strategy.Impl.NovelDataInitializer.Attr;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

#pragma warning disable SA1202 // Public test entry points are kept next to their corresponding workflow helpers.
#pragma warning disable SA1204 // Static field probes are grouped together near the end of the workflow implementation.

/// <summary>
/// Simple test strategy for testing site connectivity without implementing a full scraper.
/// Provides single-attempt testing without retry logic for faster testing.
/// </summary>
internal sealed class TestStrategy(IHttpClientFactory httpClientFactory, IDriverFactory? driverFactory = null)
    : ScraperStrategy(httpClientFactory, driverFactory)
{
    private static readonly char[] _progressCharacters = ['|', '/', '-', '\\'];

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    public bool LastRequestUsedFlareSolverr { get; private set; }

    private readonly IDriverFactory _driverFactory = driverFactory ?? new DriverFactory();
    private HtmlDocument _htmlDocument = new HtmlDocument();
    private Uri _testUri = new Uri("about:blank");
    private SiteConfiguration _config = new SiteConfiguration();
    private ScraperData _scraperData = new ScraperData();
    private HtmlDocument? _chapterHtmlDocument;
    private Uri? _chapterTestUri;
    private bool _requiredFieldsFailed;
    private bool _titleFailed;
    private bool _chapterLinksFailed;

    /// <summary>
    /// Describes a simple table-of-contents metadata field so the guided flow, the modify menu, and the
    /// single-field probe can all be driven from one source of truth instead of three parallel hardcoded lists.
    /// </summary>
    private sealed record TocFieldSpec(
        string Name,
        string ExampleXPath,
        Attr Attribute,
        Action<TableOfContentsSelectors, string> Set,
        bool Required);

    private static readonly IReadOnlyList<TocFieldSpec> _tocMetadataFields = new List<TocFieldSpec>
    {
        new("Title", "//h1[@class='heading']/text()", Attr.Title, (s, v) => s.NovelTitle = v, true),
        new("Author", "//a[@class='author']/text()", Attr.Author, (s, v) => s.NovelAuthor = v, false),
        new("Description", "//div[@class='description']/p/text()", Attr.Description, (s, v) => s.NovelDescription = v, false),
        new("Genres", "//div[@class='genres']/a/text()", Attr.Genres, (s, v) => s.NovelGenres = v, false),
        new("Status", "//span[@class='status']/text()", Attr.NovelStatus, (s, v) => s.NovelStatus = v, false),
        new("Alternative Names", "//div[@class='alt-names']/text()", Attr.AlternativeNames, (s, v) => s.NovelAlternativeNames = v, false),
        new("Novel Rating", "//span[@class='rating']/text()", Attr.NovelRating, (s, v) => s.NovelRating = v, false),
        new("Total Ratings", "//span[@class='rating-count']/text()", Attr.TotalRatings, (s, v) => s.TotalRatings = v, false),
    };

    /// <summary>
    /// Looks up a <see cref="TocFieldSpec"/> by display name, ignoring case and interior spaces.
    /// </summary>
    private static TocFieldSpec RequireTocField(string name)
    {
        var normalizedName = name.Replace(" ", string.Empty, StringComparison.Ordinal);
        var spec = _tocMetadataFields.FirstOrDefault(field => string.Equals(
            field.Name.Replace(" ", string.Empty, StringComparison.Ordinal),
            normalizedName,
            StringComparison.OrdinalIgnoreCase));
        return spec ?? throw new ArgumentException($"No TOC field registered for '{name}'", nameof(name));
    }

    public async Task RunInteractiveTestAsync(Uri testUri)
    {
        try
        {
            await RunInteractiveTestCoreAsync(testUri).ConfigureAwait(false);
        }
        finally
        {
            // Any Selenium drivers spun up during interactive reloads are cleaned up here.
            _driverFactory.DisposeAllDrivers();
        }
    }

    private async Task RunInteractiveTestCoreAsync(Uri testUri)
    {
        _testUri = testUri;

        Console.WriteLine($"\n{new string('=', 70)}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Interactive Site Testing Mode");
        Console.ResetColor();
        Console.WriteLine($"{new string('=', 70)}\n");

        Console.WriteLine($"Testing URL: {testUri}\n");

        var initialLoadUsedSelenium = false;
        var (htmlDocument, updatedUri, statusCode, cloudflareDetected) = await TestLoadHtmlAsync(testUri).ConfigureAwait(false);

        if (htmlDocument == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Failed to load page over HTTP (Status: {statusCode})");
            if (cloudflareDetected)
            {
                Console.WriteLine("✗ Cloudflare protection detected");
            }

            Console.ResetColor();

            // The most common reason an HTTP fetch fails outright is exactly what this tool exists to onboard:
            // a JavaScript/Cloudflare-gated site. Offer to fall back to a real browser instead of bailing.
            Console.Write("\nTry loading the page with Selenium instead? (y/n): ");
            var seleniumResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (seleniumResponse != "y" && seleniumResponse != "yes")
            {
                return;
            }

            htmlDocument = await LoadPageWithSeleniumAsync(testUri, headless: false).ConfigureAwait(false);
            if (htmlDocument == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Selenium could not load the page either - aborting.");
                Console.ResetColor();
                return;
            }

            initialLoadUsedSelenium = true;
            updatedUri = testUri;
        }

        _htmlDocument = htmlDocument;
        _testUri = updatedUri;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Page loaded successfully\n");
        Console.ResetColor();

        InitializeConfiguration();

        _config.RequiresFlareSolverr = LastRequestUsedFlareSolverr;

        if (cloudflareDetected)
        {
            _config.CloudflareProtection = CloudflareProtectionLevel.Detected;
        }

        // InitializeConfiguration rebuilds _config, so apply the Selenium requirement afterwards.
        if (initialLoadUsedSelenium)
        {
            _config.TableOfContentsRequiresSelenium = true;
        }

        await TestFieldsInteractivelyAsync().ConfigureAwait(false);

        await TestChapterContentInteractivelyAsync().ConfigureAwait(false);

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
                        await RetryFailedRequiredFieldsAsync().ConfigureAwait(false);
                        break;
                    case "2":
                        await ModifyFieldsInteractivelyAsync().ConfigureAwait(false);
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
                        await ModifyFieldsInteractivelyAsync().ConfigureAwait(false);
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

        HtmlDocument? htmlDocument;
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

                var (seleniumDoc, _) = await LoadHtmlWithSeleniumAsync(testUri, xpath, fieldName, headless).ConfigureAwait(false);

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
                var (httpDoc, updatedUri, status, cloudflare) = await TestLoadHtmlAsync(testUri).ConfigureAwait(false);
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

            // Simple TOC metadata fields are driven from the shared registry so this probe, the guided flow,
            // and the modify menu never fall out of sync. Chapter-level and special fields stay explicit.
            var spec = _tocMetadataFields.FirstOrDefault(field =>
                string.Equals(field.Name.Replace(" ", string.Empty, StringComparison.Ordinal), fieldUpper, StringComparison.OrdinalIgnoreCase));

            bool success;
            if (spec != null)
            {
                success = await TestSpecificField(
                    spec.Attribute,
                    xpath,
                    htmlDocument,
                    scraperData,
                    config,
                    value => spec.Set(config.Selectors.TableOfContents, value)).ConfigureAwait(false);
            }
            else
            {
                success = fieldUpper switch
                {
                    "THUMBNAIL" => await TestSpecificField(
                        Attr.ThumbnailUrl,
                        xpath,
                        htmlDocument,
                        scraperData,
                        config,
                        value => config.Selectors.TableOfContents.NovelThumbnailUrl = value).ConfigureAwait(false),
                    "CHAPTERLINKS" => CheckChapterLinksField(xpath, htmlDocument),
                    "CHAPTERTITLE" => TestChapterTitleField(xpath, htmlDocument),
                    "CHAPTERCONTENT" => TestChapterContentField(xpath, htmlDocument),
                    "CHAPTERTITLEINTOC" => TestChapterTitleInTocField(xpath, htmlDocument),
                    "NEXTCHAPTERBUTTON" => TestNextChapterButtonSingleField(xpath, htmlDocument),
                    _ => HandleUnknownField(fieldName)
                };
            }

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

    /// <summary>
    /// Tests loading HTML with a single attempt (no retries) for faster testing.
    /// </summary>
    /// <param name="uri">The URI of the page to load.</param>
    /// <returns>A tuple containing the loaded HTML document (or null on failure), the possibly-redirected URI, the HTTP status code, and whether Cloudflare protection was detected.</returns>
    public async Task<(HtmlDocument? Document, Uri UpdatedUri, int StatusCode, bool CloudflareDetected)> TestLoadHtmlAsync(Uri uri)
    {
        LastRequestUsedFlareSolverr = false;

        try
        {
            using var client = _httpClientFactory.CreateClient();
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            // Enforce a real 10s ceiling for fast testing. Setting a custom HttpRequestOptions key does
            // nothing unless a handler reads it (ours does not), so use a CancellationToken instead.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var userAgent = GetNextUserAgent();

            requestMessage.Headers.Add("User-Agent", userAgent);
            requestMessage.Headers.Add(
                "Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
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

            using var response = await client.SendAsync(requestMessage, timeoutCts.Token).ConfigureAwait(false);
            var statusCode = (int)response.StatusCode;

            var content = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            var isCloudflareDetected = DetectCloudflare(response, content, statusCode);

            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn($"[TEST] Request failed to {uri} - Status: {statusCode}");
                if (isCloudflareDetected)
                {
                    Logger.Warn($"[TEST] Cloudflare protection detected");

                    var (flareSolverrDocument, flareSolverrUri, flareSolverrStatusCode) = await TrySolveCloudflareChallengeAsync(uri).ConfigureAwait(false);
                    if (flareSolverrDocument != null)
                    {
                        LastRequestUsedFlareSolverr = true;
                        return (flareSolverrDocument, flareSolverrUri, flareSolverrStatusCode, true);
                    }
                }

                return (null, uri, statusCode, isCloudflareDetected);
            }

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);

            var canonicalNode = htmlDocument.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
            if (canonicalNode == null)
            {
                return (htmlDocument, uri, statusCode, isCloudflareDetected);
            }

            var canonicalUrl = canonicalNode.Attributes["href"]?.Value;
            if (string.IsNullOrEmpty(canonicalUrl) || canonicalUrl == uri.ToString())
            {
                return (htmlDocument, uri, statusCode, isCloudflareDetected);
            }

            Logger.Debug($"[TEST] Canonical URL detected: {canonicalUrl}");
            uri = new Uri(canonicalUrl);

            return (htmlDocument, uri, statusCode, isCloudflareDetected);
        }
        catch (HttpRequestException ex)
        {
            Logger.Error($"[TEST] HTTP error: {ex.Message}");
            return (null, uri, 0, false);
        }
        catch (OperationCanceledException)
        {
            Logger.Error($"[TEST] Request to {uri} timed out after 10 seconds");
            return (null, uri, 0, false);
        }
        catch (Exception ex)
        {
            Logger.Error($"[TEST] Error during test: {ex.Message}");
            return (null, uri, 0, false);
        }
    }

    public async Task ValidateConfigAsync(SiteConfiguration siteConfig, Uri testUri)
    {
        try
        {
            await ValidateConfigCoreAsync(siteConfig, testUri).ConfigureAwait(false);
        }
        finally
        {
            // Dispose any Selenium driver created for TOC-requires-Selenium configs.
            _driverFactory.DisposeAllDrivers();
        }
    }

    private async Task ValidateConfigCoreAsync(SiteConfiguration siteConfig, Uri testUri)
    {
        Console.WriteLine($"\n{new string('=', 70)}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Validating Configuration: {siteConfig.SiteName}");
        Console.ResetColor();
        Console.WriteLine($"{new string('=', 70)}\n");

        Console.WriteLine($"Test URL: {testUri}\n");

        HtmlDocument? htmlDocument;
        var updatedUri = testUri;

        if (siteConfig.TableOfContentsRequiresSelenium)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Config marks the table of contents as requiring Selenium - loading with a real browser.");
            Console.ResetColor();

            htmlDocument = await LoadPageWithSeleniumAsync(
                testUri,
                headless: true,
                siteConfig.Selectors.TableOfContents.ChapterLinks).ConfigureAwait(false);

            if (htmlDocument == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Failed to load page with Selenium");
                Console.ResetColor();
                return;
            }
        }
        else
        {
            var (httpDocument, resolvedUri, statusCode, cloudflareDetected) =
                await TestLoadHtmlAsync(testUri).ConfigureAwait(false);

            if (httpDocument == null)
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

            htmlDocument = httpDocument;
            updatedUri = resolvedUri;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Page loaded successfully\n");
        Console.ResetColor();

        var scraperData = new ScraperData
        {
            SiteConfig = siteConfig,
            SiteTableOfContents = updatedUri,
            BaseUri = new Uri(updatedUri.GetLeftPart(UriPartial.Authority)),
            HttpClientFactory = _httpClientFactory
        };

        Console.WriteLine("Validating Selectors.TableOfContents...\n");

        var validationResults = new List<(string FieldName, bool Success)>();

        Console.WriteLine($"Title:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelTitle))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelTitle}");
            Console.ResetColor();
        }

        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Title",
            Attr.Title,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelTitle)).ConfigureAwait(false));

        Console.WriteLine($"Author:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelAuthor))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelAuthor}");
            Console.ResetColor();
        }

        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Author",
            Attr.Author,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelAuthor)).ConfigureAwait(false));

        Console.WriteLine($"Description:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelDescription))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelDescription}");
            Console.ResetColor();
        }

        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Description",
            Attr.Description,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelDescription)).ConfigureAwait(false));

        Console.WriteLine($"Genres:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelGenres))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelGenres}");
            Console.ResetColor();
        }

        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Genres",
            Attr.Genres,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelGenres)).ConfigureAwait(false));

        Console.WriteLine($"Status:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelStatus))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelStatus}");
            Console.ResetColor();
        }

        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Status",
            Attr.NovelStatus,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelStatus)).ConfigureAwait(false));

        Console.WriteLine($"Alternative Names:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelAlternativeNames))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelAlternativeNames}");
            Console.ResetColor();
        }

        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Alternative Names",
            Attr.AlternativeNames,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelAlternativeNames)).ConfigureAwait(false));

        Console.WriteLine($"Thumbnail:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelThumbnailUrl))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.NovelThumbnailUrl}");
            Console.ResetColor();
        }

        validationResults.Add(await TestStrategyInitializer.ValidateFieldAsync(
            "Thumbnail",
            Attr.ThumbnailUrl,
            htmlDocument,
            scraperData,
            !string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.NovelThumbnailUrl)).ConfigureAwait(false));

        // Validate Chapter Links (REQUIRED)
        Console.WriteLine($"Chapter Links:");
        if (!string.IsNullOrEmpty(siteConfig.Selectors.TableOfContents.ChapterLinks))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  XPath: {siteConfig.Selectors.TableOfContents.ChapterLinks}");
            Console.ResetColor();
        }

        validationResults.Add(ValidateChapterLinks("Chapter Links", htmlDocument, siteConfig.Selectors.TableOfContents.ChapterLinks));

        Console.WriteLine($"\n{new string('=', 70)}");
        Console.WriteLine("Validation Summary:");
        Console.WriteLine($"{new string('=', 70)}\n");

        var successCount = validationResults.Count(result => result.Success);
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

    private void InitializeConfiguration()
    {
        var host = _testUri.Host.Replace("www.", string.Empty, StringComparison.Ordinal);
        var siteName = host.Split('.')[0];
        siteName = char.ToUpper(siteName[0], CultureInfo.InvariantCulture) + siteName.Substring(1);

        _config = new SiteConfiguration
        {
            SiteName = siteName,
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

        Console.WriteLine($"Site Name: {_config.SiteName}");
        Console.WriteLine($"URL Pattern: {_config.UrlPattern}\n");
    }

    private async Task TestFieldsInteractivelyAsync()
    {
        Console.WriteLine($"{new string('-', 70)}");
        Console.WriteLine("Field Testing - Press Enter to skip optional fields");
        Console.WriteLine($"{new string('-', 70)}\n");

        await TestFieldAsync(RequireTocField("Title")).ConfigureAwait(false);
        await TestFieldAsync(RequireTocField("Author")).ConfigureAwait(false);
        await TestFieldAsync(RequireTocField("Description")).ConfigureAwait(false);
        await TestFieldAsync(
            "Current Chapter Link",
            "//*[@id='en-chapters']/li[1]/a",
            Attr.CurrentChapterUrl,
            xpath => _config.Selectors.TableOfContents.LatestChapterLink = xpath,
            false).ConfigureAwait(false);
        await TestFieldAsync(RequireTocField("Genres")).ConfigureAwait(false);

        TestCompletedStatusSetting();

        await TestFieldAsync(RequireTocField("Status")).ConfigureAwait(false);
        await TestFieldAsync(RequireTocField("Alternative Names")).ConfigureAwait(false);

        await TestNovelRatingFieldAsync().ConfigureAwait(false);

        await TestThumbnailFieldAsync().ConfigureAwait(false);
        await TestChapterLinksFieldAsync().ConfigureAwait(false);

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
            await TestFieldAsync(RequireTocField("Title")).ConfigureAwait(false);
        }

        if (_chapterLinksFailed)
        {
            _chapterLinksFailed = false;
            await TestChapterLinksFieldAsync().ConfigureAwait(false);
        }

        _requiredFieldsFailed = _titleFailed || _chapterLinksFailed;
    }

    private async Task ModifyFieldsInteractivelyAsync()
    {
        while (true)
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
            if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var choice))
            {
                choice = -1;
            }

            switch (choice)
            {
                case 0:
                    return;
                case 1:
                    _titleFailed = false;
                    await TestFieldAsync(RequireTocField("Title")).ConfigureAwait(false);
                    _requiredFieldsFailed = _titleFailed || _chapterLinksFailed;
                    break;
                case 2:
                    await TestFieldAsync(RequireTocField("Author")).ConfigureAwait(false);
                    break;
                case 3:
                    await TestFieldAsync(RequireTocField("Description")).ConfigureAwait(false);
                    break;
                case 4:
                    await TestFieldAsync(RequireTocField("Genres")).ConfigureAwait(false);
                    break;
                case 5:
                    await TestFieldAsync(RequireTocField("Status")).ConfigureAwait(false);
                    break;
                case 6:
                    await TestFieldAsync(RequireTocField("Alternative Names")).ConfigureAwait(false);
                    break;
                case 7:
                    await TestThumbnailFieldAsync().ConfigureAwait(false);
                    break;
                case 8:
                    _chapterLinksFailed = false;
                    await TestChapterLinksFieldAsync().ConfigureAwait(false);
                    _requiredFieldsFailed = _titleFailed || _chapterLinksFailed;
                    break;
                case 9:
                    TestPaginationSettings();
                    break;
                case 10:
                    TestContentTypeSetting();
                    break;
                case 11:
                    TestCompletedStatusSetting();
                    break;
                case 12:
                    TestChapterSortOrderSetting();
                    break;
                case 13:
                    await TestFieldAsync(RequireTocField("Novel Rating")).ConfigureAwait(false);
                    break;
                case 14:
                    await TestFieldAsync(RequireTocField("Total Ratings")).ConfigureAwait(false);
                    break;
                case 15 when _chapterHtmlDocument != null:
                    await TestChapterTitleAsync().ConfigureAwait(false);
                    break;
                case 16 when _chapterHtmlDocument != null:
                    await TestChapterContentAsync().ConfigureAwait(false);
                    break;
                case 17 when _chapterHtmlDocument != null:
                    await TestNextChapterButtonFieldAsync().ConfigureAwait(false);
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Invalid choice");
                    Console.ResetColor();
                    break;
            }
        }
    }

    private Task TestFieldAsync(TocFieldSpec spec) =>
        TestFieldAsync(
            spec.Name,
            spec.ExampleXPath,
            spec.Attribute,
            xpath => spec.Set(_config.Selectors.TableOfContents, xpath),
            spec.Required);

    private async Task TestFieldAsync(
        string fieldName,
        string exampleXPath,
        Attr attribute,
        Action<string> setSelectorAction,
        bool isRequired)
    {
        var fieldVerified = false;

        while (!fieldVerified)
        {
            Console.WriteLine($"\n[{fieldName}]");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
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

                    await PromptSeleniumForTocIfNeededAsync(fieldName).ConfigureAwait(false);

                    _requiredFieldsFailed = true;

                    if (fieldName.Equals("Title", StringComparison.OrdinalIgnoreCase))
                    {
                        _titleFailed = true;
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("⊘ Skipped");
                    Console.ResetColor();
                }

                return;
            }

            setSelectorAction(xpath);
            var success = await ValidateFieldAsync(attribute).ConfigureAwait(false);

            Console.Write("\nAre you happy with this result? (y/n/retry, default: y): ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();

            switch (response)
            {
                case "y" or "yes":
                    {
                        fieldVerified = true;

                        if (!isRequired || success)
                        {
                            continue;
                        }

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
                            await PromptSeleniumForTocIfNeededAsync(fieldName).ConfigureAwait(false);
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

    private async Task PromptSeleniumForTocIfNeededAsync(string fieldName)
    {
        Console.WriteLine($"\n⚠ {fieldName} field could not be extracted.");
        Console.WriteLine("If the content is loaded via JavaScript or requires interaction, Selenium may be needed.");
        Console.Write("\nDoes the table of contents require Selenium to load this field? (y/n): ");
        var seleniumResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (seleniumResponse != "y" && seleniumResponse != "yes")
        {
            return;
        }

        _config.TableOfContentsRequiresSelenium = true;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("✓ Marked site as requiring Selenium for table of contents");
        Console.ResetColor();

        await ReloadTableOfContentsWithSeleniumAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Re-fetches the table of contents page through a real browser and swaps it in as the active document,
    /// so subsequent XPath attempts are tested against the JavaScript-rendered DOM rather than the raw HTTP HTML.
    /// </summary>
    private async Task<bool> ReloadTableOfContentsWithSeleniumAsync()
    {
        var seleniumDoc = await LoadPageWithSeleniumAsync(_testUri, headless: false).ConfigureAwait(false);
        if (seleniumDoc == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Could not reload the page with Selenium - continuing against the original HTML.");
            Console.ResetColor();
            return false;
        }

        _htmlDocument = seleniumDoc;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Reloaded the table of contents with Selenium. Retry your XPath against the rendered page.");
        Console.ResetColor();
        return true;
    }

    private async Task<bool> ValidateFieldAsync(Attr attribute)
    {
        try
        {
            using var novelDataBuffer = new NovelDataBuffer();
            await TestStrategyInitializer.TestSingleFieldAsync(attribute, novelDataBuffer, _htmlDocument, _scraperData)
                .ConfigureAwait(false);

            var hasData = TestStrategyInitializer.HasFieldData(attribute, novelDataBuffer);

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
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Example: //img[@class='cover']");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("(Optional) ");
            Console.ResetColor();
            Console.Write("XPath: ");

            var xpath = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(xpath))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("⊘ Skipped");
                Console.ResetColor();
                return;
            }

            _config.Selectors.TableOfContents.NovelThumbnailUrl = xpath;

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Common attributes: src, data-src, data-lazy");
            Console.ResetColor();
            Console.Write("Attribute name (default: src): ");

            var attribute = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(attribute))
            {
                attribute = "src";
            }

            _config.Selectors.TableOfContents.ThumbnailUrlAttribute = attribute;
            var success = await ValidateFieldAsync(Attr.ThumbnailUrl).ConfigureAwait(false);

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
                            await PromptSeleniumForTocIfNeededAsync("Thumbnail").ConfigureAwait(false);
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

    private async Task TestChapterLinksFieldAsync()
    {
        var linksVerified = false;

        while (!linksVerified)
        {
            Console.WriteLine($"\n[Chapter Links] (Required)");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
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

                    if (await ReloadTableOfContentsWithSeleniumAsync().ConfigureAwait(false))
                    {
                        continue;
                    }
                }

                _requiredFieldsFailed = true;
                _chapterLinksFailed = true;
                return;
            }

            _config.Selectors.TableOfContents.ChapterLinks = xpath;

            try
            {
                var nodes = _htmlDocument.DocumentNode.SelectNodes(xpath);

                if (nodes == null || nodes.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ No chapter links found");
                    Console.ResetColor();

                    Console.Write("\nDo you want to retry with a different XPath? (y/n, default: y): ");
                    var retryResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

                    if (retryResponse == "y" || retryResponse == "yes" || string.IsNullOrEmpty(retryResponse))
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
                    var value = nodes[i].GetAttributeValue("href", nodes[i].InnerText?.Trim() ?? string.Empty);
                    Console.WriteLine($"  [{i + 1}] {value}");
                }

                if (nodes.Count > 3)
                {
                    Console.WriteLine($"\nLast 3 chapters:");
                    for (var i = Math.Max(0, nodes.Count - 3); i < nodes.Count; i++)
                    {
                        var value = nodes[i].GetAttributeValue("href", nodes[i].InnerText?.Trim() ?? string.Empty);
                        Console.WriteLine($"  [{i + 1}] {value}");
                    }
                }

                Console.Write("\nDo the chapter links look correct? (y/n, default: y): ");
                var verifyResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

                if (verifyResponse == "y" || verifyResponse == "yes" || string.IsNullOrEmpty(verifyResponse))
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

                    if (await ReloadTableOfContentsWithSeleniumAsync().ConfigureAwait(false))
                    {
                        continue;
                    }
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

                if (retryResponse == "y" || retryResponse == "yes" || string.IsNullOrEmpty(retryResponse))
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
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Example: ?page={0}  or  /page-{0}");
            Console.ResetColor();
            Console.Write("Pagination format: ");
            _config.PaginationType = Console.ReadLine()?.Trim();

            Console.Write("Pagination query partial (e.g., '?page='): ");
            _config.PaginationQueryPartial = Console.ReadLine()?.Trim();

            Console.Write("Chapters per page (default: 50): ");
            var chaptersPerPage = Console.ReadLine()?.Trim();
            _config.ChaptersPerPage = int.TryParse(chaptersPerPage, NumberStyles.Integer, CultureInfo.InvariantCulture, out int cpp)
                ? cpp
                : 50;

            Console.Write("Page offset (0 or 1, i.e. Is the first page on page 1 or page 2? if 1 chose 0, otherwise add the value will be added default: 1): ");
            var offset = Console.ReadLine()?.Trim();
            _config.PageOffSet = int.TryParse(offset, NumberStyles.Integer, CultureInfo.InvariantCulture, out int off) ? off : 1;

            TestLastTableOfContentsPageField();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ No pagination");
            Console.ResetColor();
        }
    }

    private void TestLastTableOfContentsPageField()
    {
        while (true)
        {
            Console.WriteLine("\n[Last Table of Contents Page] (Required for pagination)");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Select the link for the final chapter-list page. Both //a and //a/@href are supported.");
            Console.WriteLine("Example: //a[@aria-label='Last']");
            Console.ResetColor();
            Console.Write("XPath: ");

            var lastTableOfContentsPageXPath = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(lastTableOfContentsPageXPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Required when pagination is enabled");
                Console.ResetColor();
                continue;
            }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Use 'href' for a link such as '?page=25', or 'data-page' when the number is stored separately.");
            Console.ResetColor();
            Console.Write("Attribute containing the last page number or URL (default: href): ");
            var lastTableOfContentsPageAttribute = Console.ReadLine()?.Trim();
            lastTableOfContentsPageAttribute = string.IsNullOrWhiteSpace(lastTableOfContentsPageAttribute)
                ? "href"
                : lastTableOfContentsPageAttribute;

            try
            {
                var lastTableOfContentsPageNode = _htmlDocument.DocumentNode.SelectSingleNode(lastTableOfContentsPageXPath);
                if (lastTableOfContentsPageNode == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ No last page link found");
                    Console.ResetColor();
                }
                else
                {
                    var lastTableOfContentsPageUrl = lastTableOfContentsPageNode
                        .GetAttributeValue(lastTableOfContentsPageAttribute, string.Empty)
                        .Trim();

                    if (!string.IsNullOrWhiteSpace(lastTableOfContentsPageUrl))
                    {
                        var lastTableOfContentsPageNumber = GetTableOfContentsPageNumber(lastTableOfContentsPageUrl, _testUri);
                        if (lastTableOfContentsPageNumber >= 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✓ Last chapter-list page: {lastTableOfContentsPageNumber}");
                            Console.ResetColor();
                            Console.WriteLine($"  URL: {lastTableOfContentsPageUrl}");
                            Console.Write("\nDoes the last page look correct? (y/n, default: y): ");

                            var verificationResponse = Console.ReadLine()?.Trim().ToLowerInvariant();
                            if (verificationResponse == "y" || verificationResponse == "yes" || string.IsNullOrEmpty(verificationResponse))
                            {
                                _config.Selectors.TableOfContents.LastTableOfContentsPage = lastTableOfContentsPageXPath;
                                _config.Selectors.TableOfContents.LastTableOfContentPageNumberAttribute = lastTableOfContentsPageAttribute;
                                return;
                            }

                            continue;
                        }

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("✗ The selected link does not contain a valid page number");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"✗ The selected link does not have a '{lastTableOfContentsPageAttribute}' attribute");
                        Console.ResetColor();
                    }
                }
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Could not test the last page link: {exception.Message}");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Please enter another XPath.");
            Console.ResetColor();
        }
    }

    private void TestCompletedStatusSetting()
    {
        Console.WriteLine($"\n[Completed Status]");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("What text indicates a completed novel? (e.g., 'Completed', 'Finished')");
        Console.ResetColor();
        Console.Write("Completed status text (or press Enter for null): ");

        var status = Console.ReadLine()?.Trim();
        _config.CompletedStatus = string.IsNullOrEmpty(status) ? null : status.ToLowerInvariant();

        if (string.IsNullOrEmpty(_config.CompletedStatus))
        {
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ Completed status: '{_config.CompletedStatus}'");
        Console.ResetColor();
    }

    private void TestChapterSortOrderSetting()
    {
        Console.WriteLine($"\n[Chapter Sort Order]");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
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
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("XPath for the novel's rating score (e.g., 4.5 out of 5)");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("(Optional) ");
        Console.ResetColor();
        Console.Write("XPath: ");

        var xpath = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(xpath))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("⊘ Skipped");
            Console.ResetColor();
            return;
        }

        _config.Selectors.TableOfContents.NovelRating = xpath;
        await ValidateFieldAsync(Attr.NovelRating).ConfigureAwait(false);

        await TestTotalRatingsFieldAsync().ConfigureAwait(false);
    }

    private async Task TestTotalRatingsFieldAsync()
    {
        Console.WriteLine($"\n[Total Ratings]");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("XPath for the total number of ratings/votes");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("(Optional) ");
        Console.ResetColor();
        Console.Write("XPath: ");

        var xpath = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(xpath))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("⊘ Skipped");
            Console.ResetColor();
            return;
        }

        _config.Selectors.TableOfContents.TotalRatings = xpath;
        await ValidateFieldAsync(Attr.TotalRatings).ConfigureAwait(false);
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
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("⊘ Chapter content testing skipped");
            Console.ResetColor();
            return;
        }

        Console.WriteLine("\nProvide a chapter page URL to test on:");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
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
        var chapterPageLoadedWithSelenium = false;
        var (chapterHtml, updatedUri, statusCode, cloudflareDetected) = await TestLoadHtmlAsync(chapterUri).ConfigureAwait(false);

        if (cloudflareDetected)
        {
            _config.CloudflareProtection = CloudflareProtectionLevel.Detected;
        }

        if (chapterHtml == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Failed to load chapter page (Status: {statusCode})");
            if (cloudflareDetected)
            {
                Console.WriteLine("✗ Cloudflare protection detected");
                Console.WriteLine(IsFlareSolverrEnabled
                    ? "✗ FlareSolverr could not load this chapter page"
                    : "✗ FlareSolverr is not available");
            }

            Console.ResetColor();

            Console.Write("\nTry loading the chapter page with Selenium instead? (y/n): ");
            var seleniumResponse = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (seleniumResponse != "y" && seleniumResponse != "yes")
            {
                return;
            }

            chapterHtml = await LoadPageWithSeleniumAsync(chapterUri, headless: false).ConfigureAwait(false);
            if (chapterHtml == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Selenium could not load the chapter page either");
                Console.ResetColor();
                return;
            }

            _config.ChapterContentRequiresSelenium = true;
            updatedUri = chapterUri;
            chapterPageLoadedWithSelenium = true;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("✓ Marked site as requiring Selenium for chapter content");
            Console.ResetColor();
        }
        else if (LastRequestUsedFlareSolverr)
        {
            _config.RequiresFlareSolverr = true;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("FlareSolverr was needed to load this chapter page.");
            Console.WriteLine("Users must have FlareSolverr running to scrape chapter content from this site.");
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(chapterPageLoadedWithSelenium
            ? "✓ Chapter page loaded successfully with Selenium\n"
            : $"✓ Chapter page loaded successfully (Status: {statusCode})\n");
        Console.ResetColor();

        _chapterHtmlDocument = chapterHtml;
        _chapterTestUri = updatedUri;

        await TestChapterTitleAsync().ConfigureAwait(false);
        await TestChapterContentAsync().ConfigureAwait(false);
        await TestNextChapterButtonFieldAsync().ConfigureAwait(false);
    }

    private async Task TestChapterTitleAsync()
    {
        var fieldVerified = false;

        while (!fieldVerified)
        {
            Console.WriteLine($"\n[Chapter Title]");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Example: //h1[@class='chapter-title']/text()");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write("(Optional) ");
            Console.ResetColor();
            Console.Write("XPath: ");

            var xpath = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(xpath))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("⊘ Skipped");
                Console.ResetColor();
                return;
            }

            _config.Selectors.ChapterTitle = xpath;
            var success = false;

            try
            {
                var titleNode = _chapterHtmlDocument!.DocumentNode.SelectSingleNode(xpath);

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
                            await PromptSeleniumForChapterContentAsync("Chapter Title").ConfigureAwait(false);
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

    private async Task PromptSeleniumForChapterContentAsync(string fieldName)
    {
        Console.WriteLine($"\n⚠ {fieldName} field could not be extracted.");
        Console.WriteLine("If the content is loaded via JavaScript or requires interaction, Selenium may be needed.");
        Console.Write("\nDoes the chapter content require Selenium to load this field? (y/n): ");
        var seleniumResponse = Console.ReadLine()?.Trim().ToLowerInvariant();

        if (seleniumResponse != "y" && seleniumResponse != "yes")
        {
            return;
        }

        _config.ChapterContentRequiresSelenium = true;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("✓ Marked site as requiring Selenium for chapter content");
        Console.ResetColor();

        if (_chapterTestUri == null)
        {
            return;
        }

        var seleniumDoc = await LoadPageWithSeleniumAsync(_chapterTestUri, headless: false).ConfigureAwait(false);
        if (seleniumDoc == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ Could not reload the chapter page with Selenium - continuing against the original HTML.");
            Console.ResetColor();
            return;
        }

        _chapterHtmlDocument = seleniumDoc;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Reloaded the chapter page with Selenium. Retry your XPath against the rendered page.");
        Console.ResetColor();
    }

    private async Task TestChapterContentAsync()
    {
        var fieldVerified = false;

        while (!fieldVerified)
        {
            Console.WriteLine($"\n[Chapter Content]");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
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
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("⊘ Skipped");
                    Console.ResetColor();
                    return;
                }

                _config.Selectors.ChapterContent = xpath;

                try
                {
                    var imageNodes = _chapterHtmlDocument!.DocumentNode.SelectNodes(xpath);

                    if (imageNodes == null || imageNodes.Count == 0)
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

                        Console.ForegroundColor = ConsoleColor.DarkYellow;
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
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("⊘ Skipped");
                    Console.ResetColor();
                    return;
                }

                _config.Selectors.ChapterContent = xpath;

                try
                {
                    var contentNodes = _chapterHtmlDocument!.DocumentNode.SelectNodes(xpath);

                    if (contentNodes == null || contentNodes.Count == 0)
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
                            var preview = text is { Length: > 60 }
                                ? string.Concat(text.AsSpan(0, 60), "...")
                                : text;
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
                            await PromptSeleniumForChapterContentAsync("Chapter Content").ConfigureAwait(false);
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

        if (!_config.HasImagesForChapterContent &&
            !string.IsNullOrWhiteSpace(_config.Selectors.ChapterContent))
        {
            TestAlternativeChapterContentField();
        }
    }

    private void TestAlternativeChapterContentField()
    {
        _config.Selectors.AlternativeChapterContent = null;

        while (true)
        {
            Console.WriteLine("\n[Alternative Chapter Content]");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Optional fallback used when the main selector finds too little chapter content.");
            Console.WriteLine("Example: //div[@id='chapter-content']//p");
            Console.ResetColor();
            Console.Write("XPath (or press Enter to skip): ");

            var alternativeChapterContentXPath = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(alternativeChapterContentXPath))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("⊘ Skipped");
                Console.ResetColor();
                return;
            }

            try
            {
                var alternativeChapterContentNodes = _chapterHtmlDocument!.DocumentNode
                    .SelectNodes(alternativeChapterContentXPath);

                if (alternativeChapterContentNodes == null || alternativeChapterContentNodes.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ No alternative chapter content found");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Found {alternativeChapterContentNodes.Count} paragraphs");
                    Console.ResetColor();

                    Console.WriteLine("\nFirst 3 paragraphs:");
                    for (var index = 0; index < Math.Min(3, alternativeChapterContentNodes.Count); index++)
                    {
                        var text = alternativeChapterContentNodes[index].InnerText?.Trim();
                        var preview = text is { Length: > 60 }
                            ? string.Concat(text.AsSpan(0, 60), "...")
                            : text;
                        Console.WriteLine($"  [{index + 1}] {preview}");
                    }

                    Console.Write("\nAre you happy with this result? (y/n, default: y): ");
                    var verificationResponse = Console.ReadLine()?.Trim().ToLowerInvariant();
                    if (verificationResponse == "y" || verificationResponse == "yes" || string.IsNullOrEmpty(verificationResponse))
                    {
                        _config.Selectors.AlternativeChapterContent = alternativeChapterContentXPath;
                        return;
                    }

                    continue;
                }
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Invalid XPath: {exception.Message}");
                Console.ResetColor();
            }

            Console.Write("Do you want to retry? (y/n, default: y): ");
            var retryResponse = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (retryResponse != "n" && retryResponse != "no")
            {
                continue;
            }

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("⊘ Alternative chapter content skipped");
            Console.ResetColor();
            return;
        }
    }

    private async Task TestNextChapterButtonFieldAsync()
    {
        var fieldVerified = false;

        while (!fieldVerified)
        {
            Console.WriteLine($"\n[Next Chapter Button]");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
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
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("⊘ Skipped");
                Console.ResetColor();
                return;
            }

            _config.Selectors.NextChapterButton = xpath;
            var success = false;

            try
            {
                var node = _chapterHtmlDocument!.DocumentNode.SelectSingleNode(xpath);

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
                    success = true;
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
                    if (!success)
                    {
                        await PromptSeleniumForChapterContentAsync("Next Chapter Button").ConfigureAwait(false);
                    }

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

        var missingRequiredSelectors = new List<string>();
        if (string.IsNullOrWhiteSpace(_config.Selectors.TableOfContents.NovelTitle))
        {
            missingRequiredSelectors.Add("Novel Title");
        }

        if (string.IsNullOrWhiteSpace(_config.Selectors.TableOfContents.ChapterLinks))
        {
            missingRequiredSelectors.Add("Chapter Links");
        }

        if (string.IsNullOrWhiteSpace(_config.Selectors.ChapterContent))
        {
            missingRequiredSelectors.Add("Chapter Content");
        }

        if (missingRequiredSelectors.Count > 0)
        {
            _config.IsActive = false;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Required selector not set for: {string.Join(", ", missingRequiredSelectors)}.");
            Console.WriteLine("  The configuration will be saved as inactive until these selectors are provided.");
            Console.ResetColor();
            Console.WriteLine();
        }

        if (string.IsNullOrWhiteSpace(_config.Selectors.ChapterTitle))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚠ No Chapter Title selector is set. This is optional, but should be verified with a real chapter.");
            Console.ResetColor();
            Console.WriteLine();
        }

        var json = JsonSerializer.Serialize(_config, _jsonSerializerOptions);

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
        Console.WriteLine("2. The configuration has been saved in the sites directory");
        Console.WriteLine("3. Use the common strategy unless the site requires custom behavior");
        Console.WriteLine("4. Validate the saved configuration, then test with a real download");
        Console.WriteLine($"{new string('=', 70)}\n");
    }

    private void SaveConfigToFile(string json)
    {
        try
        {
            var configurationDirectory = GetSiteConfigurationDirectory();
            Directory.CreateDirectory(configurationDirectory);

            var fileName = CreateSiteConfigurationFileName(_config.UrlPattern);
            var filePath = Path.Combine(configurationDirectory, fileName);

            if (File.Exists(filePath))
            {
                Console.Write($"A configuration for '{_config.UrlPattern}' already exists. Replace it? (y/n): ");
                var shouldReplace = string.Equals(
                    Console.ReadLine()?.Trim(),
                    "y",
                    StringComparison.OrdinalIgnoreCase);

                if (!shouldReplace)
                {
                    var draftsDirectory = Path.Combine(configurationDirectory, "drafts");
                    Directory.CreateDirectory(draftsDirectory);

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture);
                    filePath = Path.Combine(
                        draftsDirectory,
                        $"{Path.GetFileNameWithoutExtension(fileName)}-{timestamp}.json");
                }
            }

            File.WriteAllText(filePath, json);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Saved to: {filePath}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ Could not save configuration to file: {ex.Message}");
            Console.ResetColor();
            Logger.Warn($"Failed to save config to file: {ex}");
        }
    }

    private static string CreateSiteConfigurationFileName(string urlPattern)
    {
        var normalizedCharacters = urlPattern
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' ? character : '-')
            .ToArray();

        return $"{new string(normalizedCharacters)}.json";
    }

    private static string GetSiteConfigurationDirectory()
    {
        var workingDirectorySiteConfigurations = Path.Combine(Directory.GetCurrentDirectory(), "sites");
        if (Directory.Exists(workingDirectorySiteConfigurations))
        {
            return workingDirectorySiteConfigurations;
        }

        var repositorySiteConfigurations = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Benny-Scraper",
            "sites");
        if (Directory.Exists(repositorySiteConfigurations))
        {
            return repositorySiteConfigurations;
        }

        return Path.Combine(AppContext.BaseDirectory, "sites");
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
            driver = await _driverFactory.CreateDriverAsync(testUri.ToString(), isHeadless: headless).ConfigureAwait(false);

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));

            await TryClickNovelBinChapterTabAsync(driver, wait, testUri).ConfigureAwait(false);

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
    }

    /// <summary>
    /// Loads a page through a real browser and returns the rendered DOM. When <paramref name="waitForXpath"/> is
    /// provided the loader waits for that XPath to appear; otherwise it waits for document.readyState to reach
    /// 'complete'. Existing drivers are disposed first so repeated interactive reloads don't pile up browser windows.
    /// </summary>
    private async Task<HtmlDocument?> LoadPageWithSeleniumAsync(Uri uri, bool headless, string? waitForXpath = null)
    {
        try
        {
            _driverFactory.DisposeAllDrivers();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"🌐 Loading page with Selenium (headless: {headless})...");
            Console.ResetColor();

            var driver = await _driverFactory.CreateDriverAsync(uri.ToString(), isHeadless: headless).ConfigureAwait(false);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));

            await TryClickNovelBinChapterTabAsync(driver, wait, uri).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(waitForXpath))
            {
                var waitTimedOut = false;
                using var progressCancellationTokenSource = new CancellationTokenSource();
                var progressTask = DisplaySeleniumWaitProgressAsync(progressCancellationTokenSource.Token);
                try
                {
                    wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(waitForXpath)));
                }
                catch (WebDriverTimeoutException)
                {
                    waitTimedOut = true;
                }
                finally
                {
                    await progressCancellationTokenSource.CancelAsync().ConfigureAwait(false);
                    await progressTask.ConfigureAwait(false);
                }

                if (waitTimedOut)
                {
                    Logger.Warn($"Timed out waiting for XPath during Selenium load: {waitForXpath}");
                }
            }
            else
            {
                try
                {
                    wait.Until(d => string.Equals(
                        ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState")?.ToString(),
                        "complete",
                        StringComparison.Ordinal));
                }
                catch (WebDriverTimeoutException)
                {
                    Logger.Warn("Timed out waiting for document.readyState to reach 'complete'");
                }
            }

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(driver.PageSource);
            return htmlDocument;
        }
        catch (Exception ex)
        {
            Logger.Error($"Selenium load error: {ex}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ Failed to load page with Selenium: {ex.Message}");
            Console.ResetColor();
            return null;
        }
    }

    private static async Task DisplaySeleniumWaitProgressAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var progressCharacterIndex = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Console.Write(
                    $"\rWaiting for configured content... {stopwatch.Elapsed:mm\\:ss} {_progressCharacters[progressCharacterIndex]} ");
                progressCharacterIndex = (progressCharacterIndex + 1) % _progressCharacters.Length;
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            Console.Write("\r                                                        \r");
        }
    }

    /// <summary>
    /// NovelBin/NovLove render their chapter list behind a "Chapter List" tab and lazy-load it. When the target
    /// host is one of those, click the tab and wait for the chapter count to stabilize. No-op for other sites.
    /// </summary>
    private static async Task TryClickNovelBinChapterTabAsync(IWebDriver driver, WebDriverWait wait, Uri uri)
    {
        if (!uri.Host.Contains("novelbin", StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.Contains("novlove", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            Logger.Info("NovelBin/NovLove site detected - attempting to click 'Chapter List' tab...");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("🔍 Detected NovelBin/NovLove site - clicking 'Chapter List' tab...");
            Console.ResetColor();

            var chapterTab = wait.Until(
                ExpectedConditions.ElementToBeClickable(
                    By.XPath("//a[@id='tab-chapters-title'][@role='tab']")));
            chapterTab.Click();

            Logger.Info("Successfully clicked 'Chapter List' tab");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Chapter tab clicked");
            Console.ResetColor();

            // Wait for initial chapters to appear
            try
            {
                wait.Until(
                    ExpectedConditions.PresenceOfAllElementsLocatedBy(
                        By.XPath("//ul[@class='list-chapter']/li/a")));
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
                await Task.Delay(1000).ConfigureAwait(false); // Wait 1 second between checks

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

    private static async Task<bool> TestSpecificField(
        Attr attribute,
        string xpath,
        HtmlDocument htmlDocument,
        ScraperData scraperData,
        SiteConfiguration config,
        Action<string> setSelector)
    {
        setSelector(xpath);

        try
        {
            using var novelDataBuffer = new NovelDataBuffer();
            await TestStrategyInitializer.TestSingleFieldAsync(attribute, novelDataBuffer, htmlDocument, scraperData).ConfigureAwait(false);

            var hasData = TestStrategyInitializer.HasFieldData(attribute, novelDataBuffer);

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

    private static bool CheckChapterLinksField(string xpath, HtmlDocument htmlDocument)
    {
        try
        {
            var nodes = htmlDocument.DocumentNode.SelectNodes(xpath);

            if (nodes == null || nodes.Count == 0)
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
                var value = nodes[i].GetAttributeValue("href", nodes[i].InnerText?.Trim() ?? string.Empty);
                Console.WriteLine($"  [{i + 1}] {value}");
            }

            if (nodes.Count <= 5)
            {
                return true;
            }

            Console.WriteLine($"\nLast 5 chapters:");
            for (int i = Math.Max(0, nodes.Count - 5); i < nodes.Count; i++)
            {
                var value = nodes[i].GetAttributeValue("href", nodes[i].InnerText?.Trim() ?? string.Empty);
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

    private static bool TestChapterTitleField(string xpath, HtmlDocument htmlDocument)
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

            if (nodes == null || nodes.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ No content found");
                Console.ResetColor();
                return false;
            }

            var isImageContent = string.Equals(nodes.First().Name, "img", StringComparison.OrdinalIgnoreCase);

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
                var preview = text is { Length: > 80 }
                    ? string.Concat(text.AsSpan(0, 80), "...")
                    : text;
                Console.WriteLine($"  [{i + 1}] {preview}");
            }

            Console.WriteLine("\nLast content element:");
            var lastText = nodes.Last().InnerText?.Trim();
            var lastPreview = lastText is { Length: > 80 }
                ? string.Concat(lastText.AsSpan(0, 80), "...")
                : lastText;
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
            // First, find chapter link nodes to test against. Only match 'chapter' — a bare 'ch'
            // substring also matches "search", "cache", etc. and produces noisy false positives.
            var chapterLinkNodes = htmlDocument.DocumentNode.SelectNodes("//a[contains(@href, 'chapter')]");

            if (chapterLinkNodes == null || chapterLinkNodes.Count == 0)
            {
                // Fallback: try all anchor tags
                chapterLinkNodes = htmlDocument.DocumentNode.SelectNodes("//a[@href]");
            }

            if (chapterLinkNodes == null || chapterLinkNodes.Count == 0)
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
                if (titleNode == null)
                {
                    continue;
                }

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
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("\nTrying as absolute XPath for reference:");
                Console.ResetColor();
                var absoluteNodes = htmlDocument.DocumentNode.SelectNodes(xpath);
                if (absoluteNodes != null && absoluteNodes.Count != 0)
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

            if (sampleResults.Count != 0)
            {
                Console.WriteLine("\nSample titles found:");
                for (var i = 0; i < sampleResults.Count; i++)
                {
                    var preview = sampleResults[i].Length > 80
                        ? string.Concat(sampleResults[i].AsSpan(0, 80), "...")
                        : sampleResults[i];
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

    private static (string FieldName, bool Success) ValidateChapterLinks(string fieldName, HtmlDocument htmlDocument, string? xpath)
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

            if (nodes == null || nodes.Count == 0)
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

    private static bool DetectCloudflare(HttpResponseMessage response, string content, int statusCode)
    {
        if (!response.Headers.Contains("CF-RAY") &&
            !response.Headers.Contains("cf-ray") &&
            !response.Headers.Contains("CF-Cache-Status"))
        {
            return false;
        }

        if (statusCode is 403 or 503)
        {
            return true;
        }

        if (string.IsNullOrEmpty(content))
        {
            return false;
        }

        return content.Contains("cf-browser-verification", StringComparison.Ordinal) ||
               content.Contains("cf_chl_opt", StringComparison.Ordinal) ||
               content.Contains("Checking your browser", StringComparison.Ordinal) ||
               content.Contains("Just a moment", StringComparison.Ordinal) ||
               (content.Contains("ray ID", StringComparison.Ordinal) && statusCode != 200);
    }

    private static string GetNextUserAgent()
    {
        var userAgents = new List<string>
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
        };
        return userAgents[RandomNumberGenerator.GetInt32(userAgents.Count)];
    }
}

internal abstract class TestStrategyInitializer : NovelDataInitializer
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Single source of truth for deciding whether an extraction actually produced data for a given attribute.
    /// Shared by every field tester/validator so the per-attribute rules cannot drift apart.
    /// Attributes without a dedicated buffer field (e.g. <see cref="Attr.CurrentChapterUrl"/>) default to true.
    /// </summary>
    /// <param name="attribute">The extracted attribute whose corresponding buffer value is checked.</param>
    /// <param name="novelDataBuffer">The buffer containing the extraction result.</param>
    /// <returns><see langword="true"/> when the requested attribute contains usable data; otherwise, <see langword="false"/>.</returns>
    public static bool HasFieldData(Attr attribute, NovelDataBuffer novelDataBuffer)
    {
        return attribute switch
        {
            Attr.Title => !string.IsNullOrEmpty(novelDataBuffer.Title),
            Attr.Author => !string.IsNullOrEmpty(novelDataBuffer.Author),
            Attr.Description => novelDataBuffer.Description?.Count > 0,
            Attr.Genres => novelDataBuffer.Genres?.Count > 0,
            Attr.NovelStatus => !string.IsNullOrEmpty(novelDataBuffer.NovelStatus),
            Attr.AlternativeNames => novelDataBuffer.AlternativeNames?.Count > 0,
            Attr.ThumbnailUrl => !string.IsNullOrEmpty(novelDataBuffer.ThumbnailUrl),
            Attr.NovelRating => novelDataBuffer.Rating > 0,
            Attr.TotalRatings => novelDataBuffer.TotalRatings > 0,
            Attr.LastTableOfContentsPage => !string.IsNullOrEmpty(novelDataBuffer.LastTableOfContentsPageUrl),
            Attr.ChapterUrls => novelDataBuffer.ChapterLinks.Count > 0,
            Attr.FirstChapterUrl => !string.IsNullOrEmpty(novelDataBuffer.FirstChapter),
            Attr.CurrentChapterUrl => !string.IsNullOrEmpty(novelDataBuffer.CurrentChapterUrl),
            Attr.Category => false,
            Attr.AlternateLastTableOfContentsPage => false,
            _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, "Unknown novel-data attribute.")
        };
    }

    /// <summary>
    /// Tests a single attribute field for the interactive testing mode.
    /// </summary>
    /// <param name="attribute">The attribute to extract and test.</param>
    /// <param name="novelDataBuffer">The buffer that receives the extracted value.</param>
    /// <param name="htmlDocument">The HTML document to extract the attribute from.</param>
    /// <param name="scraperData">The scraper context (site configuration, base URI, HTTP client factory) used during extraction.</param>
    /// <returns>A task that represents the asynchronous extraction operation.</returns>
    public static async Task TestSingleFieldAsync(
        Attr attribute,
        NovelDataBuffer novelDataBuffer,
        HtmlDocument htmlDocument,
        ScraperData scraperData)
    {
        await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates all configured fields for an existing site configuration.
    /// </summary>
    /// <param name="fieldName">The display name of the field being validated.</param>
    /// <param name="attribute">The attribute to extract and validate.</param>
    /// <param name="htmlDocument">The HTML document to extract the attribute from.</param>
    /// <param name="scraperData">The scraper context (site configuration, base URI, HTTP client factory) used during extraction.</param>
    /// <param name="hasSelector">true if a selector is configured for this field; false to skip validation.</param>
    /// <returns>A tuple containing the field name and whether validation succeeded.</returns>
    public static async Task<(string FieldName, bool Success)> ValidateFieldAsync(
        string fieldName,
        Attr attribute,
        HtmlDocument htmlDocument,
        ScraperData scraperData,
        bool hasSelector)
    {
        if (!hasSelector)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"  ⊘ {fieldName}: Not configured (skipped)");
            Console.ResetColor();
            return (fieldName, true);
        }

        try
        {
            using var novelDataBuffer = new NovelDataBuffer();
            await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData).ConfigureAwait(false);

            var hasData = HasFieldData(attribute, novelDataBuffer);

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
                Attr.Genres =>
                    $"  ✓ {fieldName}: {string.Join(", ", novelDataBuffer.Genres?.Take(3) ?? new List<string>())}{(novelDataBuffer.Genres?.Count > 3 ? "..." : string.Empty)}",
                Attr.NovelStatus => $"  ✓ {fieldName}: {novelDataBuffer.NovelStatus}",
                Attr.AlternativeNames => $"  ✓ {fieldName}: {novelDataBuffer.AlternativeNames?.Count ?? 0} name(s)",
                Attr.ThumbnailUrl => $"  ✓ {fieldName}: {novelDataBuffer.ThumbnailUrl}",
                Attr.NovelRating => $"  ✓ {fieldName}: {novelDataBuffer.Rating}",
                Attr.TotalRatings => $"  ✓ {fieldName}: {novelDataBuffer.TotalRatings}",
                Attr.LastTableOfContentsPage => $"  ✓ {fieldName}: {novelDataBuffer.LastTableOfContentsPageUrl}",
                Attr.ChapterUrls => $"  ✓ {fieldName}: {novelDataBuffer.ChapterLinks.Count} chapter(s)",
                Attr.FirstChapterUrl => $"  ✓ {fieldName}: {novelDataBuffer.FirstChapter}",
                Attr.CurrentChapterUrl => $"  ✓ {fieldName}: {novelDataBuffer.CurrentChapterUrl}",
                Attr.Category => $"  ✓ {fieldName}: Found",
                Attr.AlternateLastTableOfContentsPage => $"  ✓ {fieldName}: Found",
                _ => throw new ArgumentOutOfRangeException(nameof(attribute), attribute, "Unknown novel-data attribute.")
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
            _logger.Error($"Validation error for {fieldName}: {ex}");
            return (fieldName, false);
        }
    }
}