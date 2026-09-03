using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Utilities;
using BennyScraper.Models;
using HtmlAgilityPack;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Cookie = System.Net.Cookie;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy
{
    internal abstract class ScraperStrategy : IDisposable
    {
        protected const int TotalPossiblePaginationTabs = 6;

        protected static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        protected static readonly NovelScraperSettings Settings = new();

        protected ScraperData ScraperData { get; } = new();

        private const int _defaultMinimumParagraphThreshold = 5;

        private const int _maxGlobalBackoffMs = 30_000;

        private static readonly List<string> _userAgents = new List<string>
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64; rv:133.0) Gecko/20100101 Firefox/133.0",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0"
        };

        private static int _userAgentIndex = -1;

        private readonly IHttpClientFactory _httpClientFactory;

        private readonly IDriverFactory _driverFactory;

        private volatile int _globalBackoffMs;

        // FlareSolverr integration for Cloudflare bypass
        private FlareSolverrService? _flareSolverr;
        private bool _flareSolverrEnabled;
        private string? _flareSolverrUserAgent;
        private volatile bool _useFlareSolverrForPageLoads;

        private SemaphoreSlim _semaphoreSlim; // limit the number of concurrent requests, prevent posssible rate limiting

        public static bool IsValidHttpUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private static bool AreChapterImageUrlsLoaded(IEnumerable<string?> chapterImageUrls)
        {
            var imageUrls = chapterImageUrls.ToList();
            return imageUrls.Count > 0 && imageUrls.All(imageUrl =>
                !string.IsNullOrWhiteSpace(imageUrl) && IsValidHttpUrl(imageUrl));
        }

        private static ChapterDataBuffer CreateFailedChapterDataBuffer(
            ChapterLink chapterLink,
            string tempImageDirectory = "")
        {
            return new ChapterDataBuffer
            {
                Url = chapterLink.Url,
                Title = string.IsNullOrWhiteSpace(chapterLink.Title) ? chapterLink.Url : chapterLink.Title,
                Content = "No content found",
                SequenceNumber = chapterLink.ChapterNumber,
                DateLastModified = DateTime.Now,
                IsPartial = true,
                TempDirectory = tempImageDirectory
            };
        }

        private static async Task StopChapterLoadingSpinnerAsync(
            CancellationTokenSource spinnerCancellationTokenSource,
            Task? spinnerTask)
        {
            try
            {
                await spinnerCancellationTokenSource.CancelAsync().ConfigureAwait(false);
                if (spinnerTask == null)
                {
                    return;
                }

                await spinnerTask.ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception exception)
            {
                Logger.Debug(exception, "The chapter-loading spinner could not be stopped cleanly.");
            }
        }

        private static void ReportChapterProcessing(ChapterLink chapterLink)
        {
            var message = $"Processing chapter {chapterLink.ChapterNumber}: {chapterLink.Url}";
            Logger.Info(message);
            Console.WriteLine(message);
        }

        protected ScraperStrategy(IHttpClientFactory? httpClientFactory = null, IDriverFactory? driverFactory = null)
        {
            _httpClientFactory = httpClientFactory ?? new HttpClientFactory();
            _driverFactory = driverFactory ?? new DriverFactory();
            ScraperData.HttpClientFactory = _httpClientFactory;
            _semaphoreSlim = new SemaphoreSlim(ConcurrentRequestsLimit);
        }

        protected enum SeleniumPageStepType
        {
            Click,
            WaitForPresence,
            WaitForClickable,
            Custom
        }

        /// <summary>
        /// Gets a value indicating whether FlareSolverr is currently enabled.
        /// </summary>
        public bool IsFlareSolverrEnabled => _flareSolverrEnabled && _flareSolverr != null;

        protected bool RequiresLogin { get; private set; }

        private int ConcurrentRequestsLimit { get; set; } = 2;

        private static readonly char[] _function = new[] { '|', '/', '-', '\\' };

        /// <summary>
        /// Enable FlareSolverr for Cloudflare bypass.
        /// FlareSolverr must be running at the specified URL (default: http://localhost:8191).
        ///
        /// To run FlareSolverr with Docker:
        /// docker run -d --name=flaresolverr -p 8191:8191 -e LOG_LEVEL=info ghcr.io/flaresolverr/flaresolverr:latest.
        /// </summary>
        /// <param name="flareSolverrUrl">FlareSolverr URL (default: http://localhost:8191).</param>
        /// <param name="useForAllPageLoads">Whether every page load should use FlareSolverr immediately.</param>
        /// <returns>True if FlareSolverr is available and enabled.</returns>
        public async Task<bool> EnableFlareSolverrAsync(
            string flareSolverrUrl = "http://localhost:8191",
            bool useForAllPageLoads = false)
        {
            _useFlareSolverrForPageLoads = false;
            _flareSolverr = new FlareSolverrService(flareSolverrUrl);
            _flareSolverrEnabled = await _flareSolverr.CheckHealthAsync().ConfigureAwait(false);

            if (_flareSolverrEnabled)
            {
                _useFlareSolverrForPageLoads = useForAllPageLoads;
                Logger.Debug($"FlareSolverr enabled at {flareSolverrUrl}");
            }
            else
            {
                Logger.Warn($"FlareSolverr not available at {flareSolverrUrl}");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠ FlareSolverr not available at {flareSolverrUrl}");
                Console.WriteLine("  FlareSolverr is optional and is not included with Benny-Scraper.");
                Console.WriteLine("  Cloudflare-protected sites may fail to load.");
                Console.WriteLine(
                    "  To run FlareSolverr: docker run -d -p 8191:8191 ghcr.io/flaresolverr/flaresolverr:latest");
            }

            Console.ResetColor();

            return _flareSolverrEnabled;
        }

        /// <summary>
        /// Disable FlareSolverr and dispose the service.
        /// </summary>
        public void DisableFlareSolverr()
        {
            _flareSolverrEnabled = false;
            _flareSolverr?.Dispose();
            _flareSolverr = null;
            _flareSolverrUserAgent = null;
            _useFlareSolverrForPageLoads = false;
            Logger.Info("FlareSolverr disabled");
        }

        public abstract Task<NovelDataBuffer> ScrapeAsync();

        public void SetVariables(SiteConfiguration siteConfig, Uri siteTableOfContents, Configuration configuration)
        {
            SetSiteConfiguration(siteConfig);
            SetSiteTableOfContents(siteTableOfContents);
            SetConcurrentRequestLimit(configuration.ConcurrencyLimit);
            SetSemaphoreLimit(ConcurrentRequestsLimit);
        }

        public SiteConfiguration GetSiteConfiguration() =>
            ScraperData.SiteConfig ?? throw new InvalidOperationException("SiteConfiguration is null");

        public void SetChapterRange(SelectedChapterRange? chapterRange, string? volumeName = null)
        {
            ScraperData.ChapterRange = chapterRange;
            ScraperData.DetectedVolumeName = volumeName;
            if (chapterRange != null)
            {
                Logger.Info($"Chapter range set: {chapterRange}{(volumeName != null ? $" ({volumeName})" : string.Empty)}");
            }
        }

        public void SetLoginPreference(bool withLogin)
        {
            RequiresLogin = withLogin;

            // Show informative message for sites with premium chapters
            if (!withLogin || ScraperData.SiteConfig.HasPremiumChapters != true)
            {
                return;
            }

            var loginMessages = new[] { $"Login Enabled for {ScraperData.SiteConfig.SiteName}" };
            CommonHelper.DrawBox(loginMessages, ConsoleColor.Cyan);
            Console.WriteLine("A browser window will open for manual login.\n");
            Console.WriteLine("Benefits:");
            Console.WriteLine($"  • Access premium chapters you own");
            Console.WriteLine($"  • Premium content included in your download\n");
            Console.WriteLine("Privacy:");
            Console.WriteLine($"  • Your credentials are NEVER stored");
            Console.WriteLine($"  • Login session ends after scraping completes\n");

            Logger.Info($"Login enabled for {ScraperData.SiteConfig.SiteName}");
        }

        public void SetSessionAuthenticated(bool isAuthenticated) => ScraperData.IsSessionAuthenticated = isAuthenticated;

        public SelectedChapterRange? GetChapterRange() => ScraperData.ChapterRange;

        public string? GetDetectedVolumeName() => ScraperData.DetectedVolumeName;

        /// <summary>
        /// Filters a list of chapter URLs based on the current ChapterRange.
        /// Returns the filtered list and optionally chapter titles if provided.
        /// </summary>
        /// <param name="chapterUrls">The full list of chapter URLs to filter.</param>
        /// <param name="filteredTitles">When this method returns, contains the chapter titles filtered to the same range as <paramref name="chapterUrls"/>, or the original <paramref name="chapterTitles"/> if no filtering was applied.</param>
        /// <param name="chapterTitles">The full list of chapter titles corresponding to <paramref name="chapterUrls"/>, or null if titles are not available.</param>
        /// <returns>The chapter URLs filtered down to the configured chapter range, or the original list if no range is set or the range is invalid.</returns>
        public IReadOnlyList<string> FilterChaptersByRange(
            IReadOnlyList<string> chapterUrls,
            out IReadOnlyList<string>? filteredTitles,
            IReadOnlyList<string>? chapterTitles = null)
        {
            filteredTitles = null;

            if (ScraperData.ChapterRange == null)
            {
                Logger.Debug("No chapter range set, returning all chapter URLs");
                filteredTitles = chapterTitles;
                return chapterUrls;
            }

            var range = ScraperData.ChapterRange;

            if (!range.IsValid())
            {
                Logger.Error($"Invalid chapter range: {range}");
                filteredTitles = chapterTitles;
                return chapterUrls;
            }

            if (range.Begin > chapterUrls.Count || range.End > chapterUrls.Count)
            {
                Logger.Error($"Chapter range {range} exceeds available chapters ({chapterUrls.Count})");
                filteredTitles = chapterTitles;
                return chapterUrls;
            }

            Logger.Info($"Filtering chapters to range: {range}");

            var filteredUrls = chapterUrls
                .Skip(range.Begin - 1)
                .Take(range.Count)
                .ToList();

            if (chapterTitles != null && chapterTitles.Count >= range.End)
            {
                filteredTitles = chapterTitles
                    .Skip(range.Begin - 1)
                    .Take(range.Count)
                    .ToList();
            }

            Logger.Info($"Filtered {chapterUrls.Count} chapters down to {filteredUrls.Count} chapters");

            return filteredUrls;
        }

        /// <summary>
        /// Extracts both chapter URLs and titles from an already-loaded HTML document.
        /// This consolidates extraction to avoid redundant HTTP requests.
        /// Populates the NovelDataBuffer with both ChapterUrls and ChapterTitles.
        /// </summary>
        /// <param name="htmlDocument">The already-loaded table of contents HTML document to extract chapter links from.</param>
        /// <param name="novelDataBuffer">The buffer whose ChapterLinks and ChapterTitles are populated with the extracted data.</param>
        /// <param name="scraperData">The scraper context providing the site configuration and base URI used to resolve chapter links.</param>
        public virtual void ExtractChapterUrlsAndTitles(
            HtmlDocument htmlDocument,
            NovelDataBuffer novelDataBuffer,
            ScraperData scraperData)
        {
            try
            {
                Logger.Info("Extracting chapter URLs and titles from table of contents");

                var chapterLinkNodes =
                    htmlDocument.DocumentNode.SelectNodes(
                        scraperData.SiteConfig.Selectors.TableOfContents.ChapterLinks ?? string.Empty);

                if (chapterLinkNodes == null || chapterLinkNodes.Count == 0)
                {
                    Logger.Warn("No chapter link nodes found");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        $"Failed to get chapter urls for novel {novelDataBuffer.Title} at url {scraperData.BaseUri}");
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine(
                        $"Please check the site configuration for {scraperData.SiteConfig.SiteName}, specifically the chapterLinks property.");
                    Console.ResetColor();
                    return;
                }

                var chapterLinks = new List<ChapterLink>();
                var chapterTitles = new List<string>();

                foreach (var chapterLinkNode in chapterLinkNodes)
                {
                    var href = chapterLinkNode.Attributes["href"].Value;
                    if (string.IsNullOrEmpty(href))
                    {
                        continue;
                    }

                    var url = IsValidHttpUrl(href)
                        ? href
                        : new Uri(scraperData.BaseUri, href).ToString();

                    var title = HtmlEntity.DeEntitize(chapterLinkNode.InnerText?.Trim() ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = $"Chapter {chapterLinks.Count + 1}";
                    }

                    chapterTitles.Add(title);
                    chapterLinks.Add(new ChapterLink { Url = url, Title = title });
                }

                novelDataBuffer.ChapterLinks.ReplaceWith(chapterLinks);
                novelDataBuffer.ChapterTitles.ReplaceWith(chapterTitles);

                Console.WriteLine($"Extracted {chapterLinks.Count} chapter URLs and {chapterTitles.Count} titles");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error extracting chapter URLs and titles: {ex.Message}");
            }
        }

        /// <summary>
        /// Sorts chapters based on the site configuration's ChapterSortOrder setting.
        /// Applies to both ChapterUrls and ChapterTitles in the NovelDataBuffer.
        /// Also assigns proper ChapterNumber after sorting.
        /// </summary>
        /// <param name="novelDataBuffer">The buffer whose ChapterLinks and ChapterTitles are reordered and re-numbered in place.</param>
        public virtual void SortChapters(NovelDataBuffer novelDataBuffer)
        {
            switch (ScraperData.SiteConfig.ChapterSortOrder)
            {
                case ChapterSortOrder.None:
                    Logger.Debug("Chapter sort order set to None, keeping original order");
                    break;
                case ChapterSortOrder.Descending:
                    Logger.Debug("Reversing chapter order (Descending -> Ascending)");
                    novelDataBuffer.ChapterLinks.ReverseInPlace();
                    novelDataBuffer.ChapterTitles.ReverseInPlace();
                    break;
                case ChapterSortOrder.Ascending:
                default:
                    Logger.Debug("Chapter sort order is Ascending (default), no reversal needed");
                    break;
            }

            AssignChapterNumbers(novelDataBuffer);
            NovelChapterStateUpdater.UpdateAvailableChapterBoundaries(novelDataBuffer);
        }

        public async Task<(HtmlDocument Document, Uri UpdatedUri)> LoadHtmlPublicAsync(Uri uri)
        {
            return await LoadHtmlAsync(uri).ConfigureAwait(false);
        }

        /// <summary>
        /// Inject cookies from the browser to bypass Cloudflare protection.
        /// You can copy cookies from your browser's DevTools (F12 -> Application -> Cookies).
        /// Example: "cf_clearance=abc123; session=xyz789".
        /// </summary>
        /// <param name="uri">The URI for which to set cookies (usually the base site URL).</param>
        /// <param name="cookieHeader">Cookie string from browser (format: "name1=value1; name2=value2").</param>
        public void InjectCookiesFromBrowser(Uri uri, string cookieHeader)
        {
            _httpClientFactory.AddCookiesFromHeader(uri, cookieHeader);
            Logger.Info($"Injected {cookieHeader.Split(';').Length} cookie(s) for {uri.Host}");
        }

        /// <summary>
        /// Add a single cookie for a specific URI.
        /// </summary>
        /// <param name="uri">The URI the cookie applies to.</param>
        /// <param name="cookie">The cookie to add.</param>
        public void AddCookie(Uri uri, Cookie cookie)
        {
            _httpClientFactory.AddCookie(uri, cookie);
            Logger.Info($"Added cookie '{cookie.Name}' for {uri.Host}");
        }

        /// <summary>
        /// Gets the non-paginated chapter contents for each chapter url provided. This method uses either HttpClient or
        /// Selenium based on site requirements.
        /// </summary>
        /// <param name="chapterLinks">The chapter links to fetch content for.</param>
        /// <returns>Returns the list of Chapter Data Buffer which contains the contents.</returns>
        public async Task<List<ChapterDataBuffer>> GetChaptersDataAsync(IList<ChapterLink> chapterLinks)
        {
            if (chapterLinks.Count == 0)
            {
                return new List<ChapterDataBuffer>();
            }

            var tempImageDirectory = string.Empty;
            var chapterDataBuffers = new List<ChapterDataBuffer>();
            try
            {
                Logger.Info("Getting chapters data");

                // Selenium is not thread safe, so don't use tasks when the site requires a browser.
                if (ShouldUseSeleniumForChapters())
                {
                    if (ShouldUseSpaNavigation())
                    {
                        Logger.Info("Using SPA navigation for chapter processing (auth session active, next-chapter button configured)");

                        // Reuse existing driver if one was left alive from TOC scraping
                        IWebDriver driver;
                        if (!_driverFactory.GetAllDrivers().IsEmpty)
                        {
                            Logger.Debug("Reusing existing Selenium driver for SPA navigation");
                            driver = _driverFactory.GetAllDrivers().Values.First();
                        }
                        else
                        {
                            driver = await _driverFactory.CreateDriverAsync(chapterLinks.First().Url, isHeadless: false).ConfigureAwait(false);
                        }

                        if (ScraperData.SiteConfig.HasImagesForChapterContent)
                        {
                            tempImageDirectory = CommonHelper.CreateTempDirectory();
                        }

                        try
                        {
                            await ProcessChaptersWithSpaNavigation(driver, chapterLinks, chapterDataBuffers, tempImageDirectory).ConfigureAwait(false);
                        }
                        finally
                        {
                            _driverFactory.DisposeAllDrivers();
                        }
                    }
                    else
                    {
                        await ProcessChaptersWithSelenium(chapterLinks, chapterDataBuffers, tempImageDirectory).ConfigureAwait(false);
                    }
                }
                else
                {
                    if (ScraperData.SiteConfig.HasImagesForChapterContent)
                    {
                        tempImageDirectory = CommonHelper.CreateTempDirectory();
                    }

                    await ProcessChaptersWithHttpClient(
                        chapterLinks,
                        chapterDataBuffers,
                        tempImageDirectory).ConfigureAwait(false);
                }

                for (var i = 0; i < chapterDataBuffers.Count && i < chapterLinks.Count; i++)
                {
                    chapterDataBuffers[i].SequenceNumber = chapterLinks[i].ChapterNumber;
                }

                return chapterDataBuffers;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting chapters data. {ex}");
                if (chapterDataBuffers.Count != 0)
                {
                    Logger.Warn(
                        $"Returning {chapterDataBuffers.Count} chapters that completed before the batch-level failure.");
                    return chapterDataBuffers;
                }

                if (string.IsNullOrEmpty(tempImageDirectory))
                {
                    throw;
                }

                Directory.Delete(tempImageDirectory, true);
                Logger.Info("Finished deleting temp directory");
                throw;
            }
        }

        public void SetSemaphoreLimit(int concurrentRequestLimit)
        {
            _semaphoreSlim?.Dispose();
            _semaphoreSlim = new SemaphoreSlim(concurrentRequestLimit);
        }

        protected static int GetTableOfContentsPageNumber(string pageValue, Uri baseUri)
        {
            if (int.TryParse(pageValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageNumber))
            {
                return pageNumber;
            }

            bool result = Uri.TryCreate(baseUri, pageValue, out Uri? uriResult);
            if (!result || uriResult == null ||
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Invalid page value", nameof(pageValue));
            }

            var pageNumberFromQuery = HttpUtility.ParseQueryString(uriResult.Query);
            if (pageNumberFromQuery.AllKeys.Contains("page"))
            {
                var pageNumberFromUrl = pageNumberFromQuery["page"];
                if (int.TryParse(pageNumberFromUrl, NumberStyles.Integer, CultureInfo.InvariantCulture, out int page))
                {
                    return page;
                }
            }

            return -1;
        }

        protected static Uri GetPaginatedTableOfContentsUri(
            Uri tableOfContentsUri,
            string paginationType,
            int pageNumber)
        {
            var paginationValue = string.Format(CultureInfo.InvariantCulture, paginationType, pageNumber);
            if (Uri.TryCreate(paginationValue, UriKind.Absolute, out var absolutePaginationUri))
            {
                return absolutePaginationUri;
            }

            var uriBuilder = new UriBuilder(tableOfContentsUri);
            if (paginationValue.StartsWith('?'))
            {
                uriBuilder.Query = paginationValue[1..];
                return uriBuilder.Uri;
            }

            if (paginationValue.StartsWith('&'))
            {
                uriBuilder.Query = uriBuilder.Query.TrimStart('?') + paginationValue;
                return uriBuilder.Uri;
            }

            var tableOfContentsUrl = tableOfContentsUri.GetLeftPart(UriPartial.Query);
            return new Uri(tableOfContentsUrl + paginationValue + tableOfContentsUri.Fragment);
        }

        /// <summary>
        /// Decodes sites that use HTML encoded characters like class="&#x70;&#x61;&#x67;&#x69;&#x6E;&#x61;&#x74;&#x69;.
        /// </summary>
        /// <param name="htmlDocument">The HTML document whose encoded content should be decoded.</param>
        /// <returns>Decoded HtmlDocument.</returns>
        protected static HtmlDocument DecodeHtml(HtmlDocument htmlDocument)
        {
            Logger.Debug("Decoding HTML - Start");
            var decodedHtml = WebUtility.HtmlDecode(htmlDocument.DocumentNode.OuterHtml);
            if (string.IsNullOrEmpty(decodedHtml))
            {
                Logger.Error("Decoded HTML was null");
                return htmlDocument;
            }

            Logger.Debug("Decoding HTML - End");
            var decodedHtmlDocument = new HtmlDocument();
            decodedHtmlDocument.LoadHtml(decodedHtml);
            Logger.Debug("Decoded HTML loaded into HtmlDocument");

            return decodedHtmlDocument;
        }

        /// <summary>
        /// This method is what is used to get the novel data from the table of contents page. i.e. Description, Chapters, Title, Novel Status.
        /// </summary>
        /// <param name="htmlDocument">The already-loaded table of contents HTML document to extract novel data from.</param>
        /// <returns>A populated <see cref="NovelDataBuffer"/> containing the novel's metadata and chapter links.</returns>
        protected abstract NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument);

        /// <summary>
        /// This method is what is used to get the novel data from the table of contents page. i.e. Description, Chapters, Title, Novel Status.
        /// </summary>
        /// <param name="htmlDocument">The already-loaded table of contents HTML document to extract novel data from.</param>
        /// <returns>A task that resolves to a populated <see cref="NovelDataBuffer"/> containing the novel's metadata and chapter links.</returns>
        protected virtual Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
        {
            // this method should always be overridden, I just needed a default implementation so other Strategies would not require it
            return Task.Run(() => FetchNovelDataFromTableOfContents(htmlDocument));
        }

        protected async Task<(HtmlDocument Document, Uri UpdatedUri)> LoadHtmlAsync(Uri uri)
        {
            var flareSolverrWasAttempted = false;
            if (_useFlareSolverrForPageLoads && IsFlareSolverrEnabled)
            {
                flareSolverrWasAttempted = true;
                var (flareSolverrDocument, flareSolverrUri, _) =
                    await TrySolveCloudflareChallengeAsync(uri, false).ConfigureAwait(false);
                if (flareSolverrDocument != null)
                {
                    return (flareSolverrDocument, flareSolverrUri);
                }

                Logger.Warn($"FlareSolverr could not load {uri}. Falling back to HttpClient for this request.");
            }

            using var client = _httpClientFactory.CreateClient();
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

            // Use FlareSolverr's user-agent if we got one from solving a challenge
            string userAgent = _flareSolverrUserAgent ??
                               _userAgents[
                                   Interlocked.Increment(ref _userAgentIndex) % _userAgents.Count];

            if (ScraperData.BaseUri == new Uri("https://www.lightnovelworld.com/"))
            {
                userAgent = _userAgents[0];
            }

            // Add realistic browser headers to bypass Cloudflare
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

            requestMessage.Headers.Add("Referer", ScraperData.BaseUri.ToString());

            requestMessage.Options.Set(
                new HttpRequestOptionsKey<TimeSpan>("RequestTimeout"),
                TimeSpan.FromSeconds(10));

            // Add random delay between 100-500ms to avoid predictable request patterns
            // This helps bypass Cloudflare's bot detection which looks for mechanical timing
            int baseDelay = RandomNumberGenerator.GetInt32(100, 500);
            int totalDelay = baseDelay + _globalBackoffMs;
            await Task.Delay(totalDelay).ConfigureAwait(false);

            Logger.Debug($"Sending request to {uri}");

            using var response = await client.SendAsync(requestMessage).ConfigureAwait(false);

            Logger.Debug($"Response Status: {(int)response.StatusCode} {response.StatusCode}");
            Logger.Debug(
                $"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"))}");

            // Guard: Handle unsuccessful response
            if (!response.IsSuccessStatusCode)
            {
                string? errorContent = null;
                try
                {
                    errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Could not read error response body: {ex.Message}");
                }

                if (!flareSolverrWasAttempted &&
                    IsFlareSolverrEnabled &&
                    FlareSolverrService.IsCloudflareChallenge(response.StatusCode, errorContent))
                {
                    var (flareSolverrDocument, flareSolverrUri, _) =
                        await TrySolveCloudflareChallengeAsync(uri).ConfigureAwait(false);
                    if (flareSolverrDocument != null)
                    {
                        return (flareSolverrDocument, flareSolverrUri);
                    }
                }

                Logger.Error($"Request failed to {uri}");
                Logger.Error($"Status Code: {(int)response.StatusCode} {response.StatusCode}");
                Logger.Error(
                    $"Response Headers: {string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"))}");

                if (!string.IsNullOrEmpty(errorContent))
                {
                    Logger.Error(errorContent.Length < 1000
                        ? $"Response Body: {errorContent}"
                        : $"Response Body (truncated): {errorContent.Substring(0, 1000)}...");
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var currentBackoff = _globalBackoffMs;
                    var newBackoff = Math.Min(currentBackoff == 0 ? 3000 : currentBackoff * 2, _maxGlobalBackoffMs);
                    _globalBackoffMs = newBackoff;
                    Logger.Warn($"429 detected for {uri} — global backoff increased to {newBackoff}ms");
                }

                throw new HttpRequestException(
                    $"Failed to load HTML document from {uri}. Status code: {response.StatusCode}");
            }

            if (_globalBackoffMs > 0)
            {
                _globalBackoffMs = Math.Max(0, _globalBackoffMs / 2);
            }

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);

            var canonicalNode = htmlDocument.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
            if (canonicalNode != null)
            {
                var canonicalUrl = canonicalNode.Attributes["href"]?.Value;
                if (!string.IsNullOrEmpty(canonicalUrl) && canonicalUrl != uri.ToString())
                {
                    Logger.Debug($"Canonical URL detected. Old URL: {uri}, Canonical URL: {canonicalUrl}");
                    uri = new Uri(canonicalUrl);
                }
            }

            return (htmlDocument, uri);
        }

        protected async Task<(HtmlDocument? Document, Uri UpdatedUri, int StatusCode)>
            TrySolveCloudflareChallengeAsync(Uri uri, bool cloudflareChallengeDetected = true)
        {
            if (!IsFlareSolverrEnabled || _flareSolverr == null)
            {
                return (null, uri, 0);
            }

            Logger.Debug(cloudflareChallengeDetected
                ? $"Cloudflare challenge detected for {uri}. Switching to FlareSolverr."
                : $"Loading {uri} directly with FlareSolverr because it was required earlier in this scraping session.");

            var flareSolverrResult = await _flareSolverr.SolveAsync(uri.ToString()).ConfigureAwait(false);
            if (flareSolverrResult?.Status != "ok" || flareSolverrResult.Solution == null)
            {
                Logger.Error(
                    $"FlareSolverr failed to solve challenge: {flareSolverrResult?.Message ?? "Unknown error"}");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ FlareSolverr failed to solve challenge");
                Console.ResetColor();
                return (null, uri, 0);
            }

            var cookieHeader = FlareSolverrService.GetCookieHeader(flareSolverrResult);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                _httpClientFactory.AddCookiesFromHeader(uri, cookieHeader);
                Logger.Debug(
                    $"Injected {flareSolverrResult.Solution.Cookies?.Count ?? 0} cookies from FlareSolverr");
            }

            _flareSolverrUserAgent = flareSolverrResult.Solution.UserAgent;
            _useFlareSolverrForPageLoads = true;

            var solvedHtmlDocument = new HtmlDocument();
            solvedHtmlDocument.LoadHtml(flareSolverrResult.Solution.Response);

            var solvedCanonicalNode = solvedHtmlDocument.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
            if (solvedCanonicalNode != null)
            {
                var solvedCanonicalUrl = solvedCanonicalNode.Attributes["href"]?.Value;
                if (!string.IsNullOrEmpty(solvedCanonicalUrl) && solvedCanonicalUrl != uri.ToString())
                {
                    Logger.Debug($"Canonical URL detected. Old URL: {uri}, Canonical URL: {solvedCanonicalUrl}");
                    uri = new Uri(solvedCanonicalUrl);
                }
            }

            Logger.Debug($"FlareSolverr successfully loaded {uri}.");
            return (solvedHtmlDocument, uri, flareSolverrResult.Solution.Status);
        }

        protected async Task<string> DownloadImageAsync(Uri uri, string tempImageDirectory)
        {
            var uriString = uri.ToString();
            uriString = uriString.Replace("amp;", string.Empty, StringComparison.Ordinal);
            uri = new Uri(uriString);

            try
            {
                using var client = _httpClientFactory.CreateClient();

                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);
                var userAgent = _flareSolverrUserAgent ?? _userAgents[Interlocked.Increment(ref _userAgentIndex) % _userAgents.Count];

                // Add realistic browser headers for image downloads
                requestMessage.Headers.Add("User-Agent", userAgent);
                requestMessage.Headers.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                requestMessage.Headers.Add("Accept-Language", "en-US,en;q=0.9");
                requestMessage.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                requestMessage.Headers.Add("DNT", "1");
                requestMessage.Headers.Add("Connection", "keep-alive");
                requestMessage.Headers.Add("Sec-Fetch-Dest", "image");
                requestMessage.Headers.Add("Sec-Fetch-Mode", "no-cors");
                requestMessage.Headers.Add("Sec-Fetch-Site", "cross-site");

                requestMessage.Headers.Add("Referer", ScraperData.BaseUri.ToString());

                using var response =
                    await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var imageStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                await using (imageStream.ConfigureAwait(false))
                {
                    var imagePath = Path.Combine(tempImageDirectory, Path.GetRandomFileName() + ".jpg");
                    var fileStream = File.Create(imagePath);
                    await using (fileStream.ConfigureAwait(false))
                    {
                        await imageStream.CopyToAsync(fileStream).ConfigureAwait(false);
                        Logger.Debug($"Downloaded image to temp folder {imagePath}");
                    }

                    return imagePath;
                }
            }
            catch (HttpRequestException e)
            {
                Logger.Error($"Error occurred while navigating to {uri}. Error: {e}");
            }

            throw new HttpRequestException($"Failed to download image from {uri}.");
        }

        protected virtual Uri TrimLastUriSegment(Uri siteUri)
        {
            string allSegementsButLast = siteUri.Segments.Take(siteUri.Segments.Length - 1)
                .Aggregate((segment1, segment2) => segment1 + segment2);
            return new Uri(ScraperData.BaseUri, allSegementsButLast);
        }

        protected void SetBaseUri(Uri siteUri)
        {
            ScraperData.BaseUri = new Uri(siteUri.GetLeftPart(UriPartial.Authority));
        }

        /// <summary>
        /// Fetches chapter links across multiple paginated table of contents pages.
        /// </summary>
        /// <param name="tableOfContentUri">The base table of contents URI used to build each paginated page URL.</param>
        /// <param name="getAllChapters">If true, all pages from <paramref name="pageToStartAt"/> to <paramref name="pageToStopAt"/> are fetched regardless of whether new content is found.</param>
        /// <param name="pageToStopAt">The last table of contents page number to fetch, or null to stop when a page has no new chapter links.</param>
        /// <param name="pageToStartAt">The first table of contents page number to fetch (defaults to 1).</param>
        /// <returns>Returns a ValueTuple that contains all the chapter urls and the url for the last page of the table of contents.</returns>
        protected virtual async Task<(List<ChapterLink> ChapterLinks, string LastTableOfContentsUrl)>
            GetPaginatedChapterLinksAsync(
                Uri tableOfContentUri,
                bool getAllChapters,
                int? pageToStopAt,
                int pageToStartAt = 1)
        {
            var chapterLinks = new List<ChapterLink>();
            var chapterUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tocSelector = ScraperData.SiteConfig.Selectors.TableOfContents;
            var paginationType = ScraperData.SiteConfig.PaginationType ?? string.Empty;
            var lastTableOfContentsUrl = tableOfContentUri.ToString();
            if (pageToStopAt.HasValue)
            {
                lastTableOfContentsUrl = GetPaginatedTableOfContentsUri(
                    tableOfContentUri,
                    paginationType,
                    pageToStopAt.Value).ToString();
            }

            for (var i = pageToStartAt; !pageToStopAt.HasValue || i <= pageToStopAt.Value; i++)
            {
                var pageUri = GetPaginatedTableOfContentsUri(tableOfContentUri, paginationType, i);
                var isPageNew = i > pageToStartAt;
                try
                {
                    Logger.Info($"Navigating to {pageUri}");
                    var (htmlDocument, _) = await LoadHtmlAsync(pageUri).ConfigureAwait(false);

                    var linkNodes = htmlDocument.DocumentNode.SelectNodes(tocSelector?.ChapterLinks ?? string.Empty);
                    if (linkNodes == null)
                    {
                        if (!pageToStopAt.HasValue)
                        {
                            break;
                        }

                        continue;
                    }

                    var newChapterCount = 0;
                    foreach (var node in linkNodes)
                    {
                        var href = node.GetAttributeValue("href", string.Empty);
                        if (string.IsNullOrWhiteSpace(href))
                        {
                            continue;
                        }

                        var url = IsValidHttpUrl(href) ? href : new Uri(ScraperData.BaseUri, href).ToString();
                        if (!chapterUrls.Add(url))
                        {
                            continue;
                        }

                        var title = HtmlEntity.DeEntitize(node.InnerText).Trim();
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            title = $"Chapter {chapterLinks.Count + 1}";
                        }

                        var premiumSelectors = tocSelector?.PremiumChapterSelectors;
                        var isPremium = ScraperData.SiteConfig.HasPremiumChapters == true
                                        && premiumSelectors?.PremiumIndicator != null
                                        && node.SelectSingleNode(premiumSelectors.PremiumIndicator) != null;

                        var costNode = isPremium && premiumSelectors?.PremiumCost != null
                            ? node.SelectSingleNode(premiumSelectors.PremiumCost)
                            : null;
                        var cost = 0;
                        if (costNode != null && int.TryParse(costNode.InnerText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCost))
                        {
                            cost = parsedCost;
                        }

                        chapterLinks.Add(new ChapterLink
                        {
                            Url = url,
                            Title = title,
                            PremiumInfo = new PremiumChapterInfo
                            {
                                IsPremium = isPremium,
                                Cost = cost,
                                CurrencyName = ScraperData.SiteConfig.PremiumInfo?.CurrencyName
                            }
                        });
                        newChapterCount++;
                    }

                    if (!pageToStopAt.HasValue)
                    {
                        if (newChapterCount == 0)
                        {
                            break;
                        }

                        lastTableOfContentsUrl = pageUri.ToString();
                    }

                    if (!getAllChapters && !isPageNew)
                    {
                        break;
                    }
                }
                catch (HttpRequestException e)
                {
                    Logger.Error($"Error occurred while navigating to {pageUri}. Error: {e}");
                    if (!pageToStopAt.HasValue)
                    {
                        break;
                    }
                }
            }

            return (chapterLinks, lastTableOfContentsUrl);
        }

        protected async Task<(HtmlDocument? Document, string? PageSource)> GetHtmlDocumentUsingSeleniumAsync(
            string url,
            string requiredXPath,
            IEnumerable<SeleniumPageStep>? steps = null,
            string objectToLookFor = "Content",
            bool isAllowedToFail = true,
            int timeoutSeconds = 60,
            bool isHeadless = false,
            Func<IWebDriver, WebDriverWait, Task>? preWaitAction = null,
            bool reuseExistingDriver = false)
        {
            IWebDriver? driver = null;

            try
            {
                if (reuseExistingDriver && !_driverFactory.GetAllDrivers().IsEmpty)
                {
                    driver = _driverFactory.GetAllDrivers().Values.First();
                    Logger.Debug("Reusing existing Selenium driver");
                    await driver.Navigate().GoToUrlAsync(url).ConfigureAwait(false);
                }
                else
                {
                    driver = await _driverFactory.CreateDriverAsync(url, isHeadless: isHeadless).ConfigureAwait(false);
                    Logger.Debug("Created new Selenium driver");
                }

                var stopwatch = Stopwatch.StartNew();
                var uriLastSegment = new Uri(url).Segments.LastOrDefault() ?? url;

                try
                {
                    Logger.Debug($"Waiting for {objectToLookFor} on page {url} to load.");

                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));

                    if (preWaitAction != null)
                    {
                        Logger.Debug("[Selenium] Running pre-wait action");
                        await preWaitAction(driver, wait).ConfigureAwait(false);
                    }

                    if (steps != null)
                    {
                        foreach (var step in steps)
                        {
                            var stepTimeout = TimeSpan.FromSeconds(step.TimeoutSeconds ?? timeoutSeconds);
                            var stepWait = new WebDriverWait(driver, stepTimeout);

                            switch (step.Type)
                            {
                                case SeleniumPageStepType.Click:
                                    {
                                        var msg = "[Selenium] Click: " + (step.Description ?? step.XPath);
                                        Logger.Debug(msg);
                                        stepWait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(step.XPath)))
                                            .Click();
                                        break;
                                    }

                                case SeleniumPageStepType.WaitForPresence:
                                    {
                                        var msg = "[Selenium] WaitForPresence: " + (step.Description ?? step.XPath);
                                        Logger.Debug(msg);
                                        stepWait.Until(
                                            ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(step.XPath)));
                                        break;
                                    }

                                case SeleniumPageStepType.WaitForClickable:
                                    {
                                        var msg = "[Selenium] WaitForClickable: " + (step.Description ?? step.XPath);
                                        Logger.Debug(msg);
                                        stepWait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(step.XPath)));
                                        break;
                                    }

                                case SeleniumPageStepType.Custom:
                                    {
                                        if (step.CustomAction == null)
                                        {
                                            throw new InvalidOperationException("Custom step requires CustomAction");
                                        }

                                        var msg = "[Selenium] Custom: " + (step.Description ?? "(custom action)");
                                        Logger.Debug(msg);
                                        await step.CustomAction(driver, stepWait).ConfigureAwait(false);
                                        break;
                                    }

                                default:
                                    throw new ArgumentOutOfRangeException(step.Type.ToString());
                            }
                        }
                    }

                    wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(requiredXPath)));
                    Logger.Debug($"\n{objectToLookFor} loaded for {url}. Time: {stopwatch.ElapsedMilliseconds} ms");

                    var htmlDocument = new HtmlDocument();
                    var pageSource = driver.PageSource;
                    htmlDocument.LoadHtml(pageSource);
                    return (htmlDocument, pageSource);
                }
                catch (WebDriverTimeoutException ex)
                {
                    var message =
                        $"Unable to get {objectToLookFor} from {uriLastSegment} after waiting for {timeoutSeconds} seconds.\n" +
                        $"Required XPath: {requiredXPath}\n" +
                        $"URL: {url}";

                    if (isAllowedToFail)
                    {
                        Logger.Error($"{message}\nException: {ex}");
                        return (null, null);
                    }

                    Logger.Fatal($"{message}\nException: {ex}");
                    throw;
                }
            }
            finally
            {
                // Only dispose the driver if we explicitly don't want to reuse it.
                // When reuseExistingDriver=true, keep the driver alive for subsequent calls
                // (e.g., table of contents needs driver and so does chapter content scraping).
                if (!reuseExistingDriver && driver != null)
                {
                    _driverFactory.DisposeDriver(driver);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            _flareSolverr?.Dispose();
            _semaphoreSlim?.Dispose();
        }

        // Backwards-compatible wrapper (keeps existing callers stable)
        protected async Task<HtmlDocument?> GetHtmlDocumentUsingSeleniumAsync(
            string url,
            string xpath,
            string novelTitle,
            string objectToLookFor = "Chapter Urls",
            bool isAllowedToFail = true,
            int timeoutSeconds = 60)
        {
            var (htmlDocument, _) = await GetHtmlDocumentUsingSeleniumAsync(
                url: url,
                requiredXPath: xpath,
                steps: null,
                objectToLookFor: objectToLookFor,
                isAllowedToFail: isAllowedToFail,
                timeoutSeconds: timeoutSeconds,
                isHeadless: true,
                reuseExistingDriver: true).ConfigureAwait(false);

            return htmlDocument;
        }

        /// <summary>
        /// Determines whether Selenium should be used for scraping chapter content.
        /// Image chapters can use HTTP when their image elements are present in the returned HTML.
        /// </summary>
        /// <returns>True if Selenium should be used to fetch chapter content; otherwise, false.</returns>
        protected virtual bool ShouldUseSeleniumForChapters()
        {
            return ScraperData.SiteConfig.EntireSiteRequiresSelenium
                   || ScraperData.SiteConfig.ChapterContentRequiresSelenium;
        }

        protected virtual string NormalizeChapterTitle(string? rawTitle)
        {
            return rawTitle?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Extracts both chapter URLs and titles from an HTML document in a specified range.
        /// This is typically used for paginated sites where chapters span multiple pages.
        /// </summary>
        /// <param name="htmlDocument">The already-loaded table of contents HTML document to extract chapter links from.</param>
        /// <param name="baseSiteUri">The base site URI used to resolve relative chapter links; if null, no chapters are extracted.</param>
        /// <param name="startChapter">The 1-based index of the first chapter to include, or null to start from the first chapter.</param>
        /// <param name="endChapter">The 1-based index of the last chapter to include, or null to include through the last chapter.</param>
        /// <returns>A tuple containing the extracted chapter URLs and their corresponding titles.</returns>
        protected virtual (List<string> Urls, List<string> Titles) GetChapterUrlsAndTitlesInRange(
            HtmlDocument htmlDocument,
            Uri? baseSiteUri,
            int? startChapter = null,
            int? endChapter = null)
        {
            Logger.Info("Getting chapter URLs and titles from table of contents");

            if (baseSiteUri == null)
            {
                Logger.Error("Base site URI is null; cannot build absolute chapter URLs.");
                return (new List<string>(), new List<string>());
            }

            try
            {
                var chapterLinks =
                    htmlDocument.DocumentNode.SelectNodes(
                        ScraperData.SiteConfig.Selectors.TableOfContents.ChapterLinks ?? string.Empty);
                if (chapterLinks == null || chapterLinks.Count == 0)
                {
                    Logger.Info("Chapter links Node Collection on table of contents page was null/empty.");
                    return (new List<string>(), new List<string>());
                }

                var chapterUrls = new List<string>();
                var chapterTitles = new List<string>();

                var index = 0;
                foreach (var link in chapterLinks)
                {
                    index++;

                    if (index < startChapter)
                    {
                        continue;
                    }

                    if (index > endChapter)
                    {
                        break;
                    }

                    var href = link.Attributes["href"]?.Value;
                    if (string.IsNullOrWhiteSpace(href))
                    {
                        continue;
                    }

                    var absoluteUrl = IsValidHttpUrl(href)
                        ? href
                        : new Uri(baseSiteUri, href.TrimStart('/')).ToString();

                    chapterUrls.Add(absoluteUrl);

                    var title = HtmlEntity.DeEntitize(link.InnerText ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = $"Chapter {chapterUrls.Count}";
                    }

                    chapterTitles.Add(title);
                }

                return (chapterUrls, chapterTitles);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting chapter urls and titles from table of contents. {ex}");
                return (new List<string>(), new List<string>());
            }
        }

        private static void AssignChapterNumbers(NovelDataBuffer novelDataBuffer)
        {
            for (var i = 0; i < novelDataBuffer.ChapterLinks.Count; i++)
            {
                // Records support 'with' for non-destructive mutation - creates a new instance with modified properties
                // instead of mutating the existing immutable ChapterLink. So we are swapping based on ChapterNumber
                novelDataBuffer.ChapterLinks[i] = novelDataBuffer.ChapterLinks[i] with { ChapterNumber = i + 1 };
            }
        }

        private static string ExtractWuxiaWorldParagraphText(HtmlNode? pNode)
        {
            if (pNode == null)
            {
                return string.Empty;
            }

            var clone = pNode.CloneNode(true);

            // Remove the inline comment-count chip (and any other aria-hidden UI fragments).
            var ariaHiddenNodes = clone.SelectNodes(".//*[@aria-hidden='true']");
            if (ariaHiddenNodes != null)
            {
                foreach (var node in ariaHiddenNodes)
                {
                    node.Remove();
                }
            }

            // Walk text nodes in DOM order. This naturally merges punctuation-only spans correctly.
            var textNodes = clone.SelectNodes(".//text()");
            if (textNodes == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var textNode in textNodes)
            {
                var chunk = HtmlEntity.DeEntitize(textNode.InnerText);
                if (string.IsNullOrWhiteSpace(chunk))
                {
                    continue;
                }

                // Normalize internal whitespace.
                chunk = Regex.Replace(chunk, @"\s+", " ").Trim();
                if (chunk.Length == 0)
                {
                    continue;
                }

                // Avoid inserting spaces before punctuation.
                var startsWithPunctuation = chunk.Length > 0 && ".,;:!?)]}".Contains(chunk[0], StringComparison.InvariantCulture);

                if (sb.Length > 0 && !startsWithPunctuation && sb[^1] != ' ')
                {
                    sb.Append(' ');
                }

                sb.Append(chunk);
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Determines whether SPA (client-side) navigation should be used for chapter-to-chapter transitions.
        /// Returns true when the user is authenticated, the site requires login, and a next-chapter button XPath is configured.
        /// SPA navigation avoids full page reloads that destroy auth state in React/SPA sites.
        /// </summary>
        /// <returns>True if SPA navigation should be used between chapters; otherwise, false.</returns>
        private bool ShouldUseSpaNavigation()
        {
            return RequiresLogin
                && ScraperData.IsSessionAuthenticated
                && !string.IsNullOrEmpty(ScraperData.SiteConfig.Selectors.NextChapterButton);
        }

        private async Task ProcessChaptersWithSelenium(
            IList<ChapterLink> chapterLinks,
            List<ChapterDataBuffer> chapterDataBuffers,
            string tempImageDirectory)
        {
            Logger.Debug("Using Selenium to get chapters data");

            IWebDriver driver;
            if (!_driverFactory.GetAllDrivers().IsEmpty)
            {
                Logger.Debug("Reusing existing Selenium driver from table of contents scraping");
                driver = _driverFactory.GetAllDrivers().Values.First();
            }
            else
            {
                driver = await _driverFactory.CreateDriverAsync(chapterLinks[0].Url, isHeadless: false).ConfigureAwait(false);
            }

            if (ScraperData.SiteConfig.HasImagesForChapterContent)
            {
                tempImageDirectory = CommonHelper.CreateTempDirectory();
            }

            try
            {
                var totalChapters = chapterLinks.Count;
                var currentChapter = 0;
                foreach (var chapterLink in chapterLinks)
                {
                    currentChapter++;
                    ReportChapterProcessing(chapterLink);
#pragma warning disable CA2007
                    chapterDataBuffers.Add(await GetChapterDataWithSeleniumSafelyAsync(
                        driver,
                        chapterLink,
                        tempImageDirectory,
                        currentChapter,
                        totalChapters));
#pragma warning restore CA2007
                }

                Console.Write("\r" + new string(' ', 80) + "\r"); // Clear the progress line
                Logger.Info($"Finished getting chapters data. Total chapters: {chapterDataBuffers.Count}");
                Logger.Debug("Disposing all drivers");
                Console.WriteLine($"Total drivers: {_driverFactory.GetAllDrivers().Count}");
                _driverFactory.DisposeAllDrivers();
                Logger.Debug("Finished disposing all drivers");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting chapters data. {ex}");

                if (Directory.Exists(tempImageDirectory))
                {
                    Directory.Delete(tempImageDirectory, true);
                    Logger.Debug("Finished deleting temp image directory");
                }
                else
                {
                    Logger.Warn($"Was unable to find temp directory {tempImageDirectory}. Please verify it was deleted successfully");
                }

                throw;
            }
            finally
            {
                Logger.Debug("Closing driver");
                _driverFactory.DisposeAllDrivers();
                Logger.Debug("Finished closing driver");
            }
        }

        /// <summary>
        /// Processes chapters using SPA (client-side) navigation instead of full page reloads.
        /// Clicks the "next chapter" button for chapter-to-chapter transitions, preserving React auth state.
        /// After each navigation, waits for the auth verification element (e.g. VIP button) to reappear
        /// before extracting content — this confirms the SPA transition completed with auth intact.
        /// Falls back to full navigation if the next-chapter click fails or lands on the wrong URL.
        /// </summary>
        /// <param name="driver">The active Selenium web driver to navigate and extract content with.</param>
        /// <param name="chapterLinks">The chapter links to process, in navigation order.</param>
        /// <param name="chapterDataBuffers">The list that each chapter's extracted data is appended to.</param>
        /// <param name="tempImageDirectory">The temp directory used to store downloaded chapter images, if applicable.</param>
        private async Task ProcessChaptersWithSpaNavigation(
            IWebDriver driver,
            IList<ChapterLink> chapterLinks,
            List<ChapterDataBuffer> chapterDataBuffers,
            string tempImageDirectory)
        {
            var nextChapterXPath = ScraperData.SiteConfig.Selectors.NextChapterButton!;
            ScraperData.SiteConfig.Selectors.UserCurrencyBalances.TryGetValue(
                "buttonToOpenBalances",
                out var authElementXPath);
            var totalChapters = chapterLinks.Count;
            var currentChapter = 0;
            var previousChapterFailed = false;

            foreach (var chapterLink in chapterLinks)
            {
                currentChapter++;
                ReportChapterProcessing(chapterLink);
                try
                {
                    var skipNavigation = false;

                    if (currentChapter == 1 || previousChapterFailed)
                    {
                        await driver.Navigate().GoToUrlAsync(chapterLink.Url).ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(authElementXPath))
                        {
                            try
                            {
                                var authWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                                authWait.Until(ExpectedConditions.ElementExists(By.XPath(authElementXPath)));
                                Logger.Debug("Auth verification passed after full navigation");
                            }
                            catch (WebDriverTimeoutException)
                            {
                                Logger.Warn($"Auth verification timed out for chapter {chapterLink.Url}, proceeding anyway");
                            }
                        }

                        skipNavigation = true;
                    }
                    else
                    {
                        // Subsequent chapters: click next-chapter button (SPA navigation)
                        try
                        {
                            var navWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                            var nextChapterButton = navWait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(nextChapterXPath)));
                            {
                                IWebElement? previousContentElement = null;
                                try
                                {
                                    previousContentElement = driver.FindElement(By.XPath(ScraperData.SiteConfig.Selectors.ChapterContent!));
                                }
                                catch (NoSuchElementException)
                                {
                                    Logger.Debug("Could not find previous content element for staleness check");
                                }

                                nextChapterButton.Click();

                                if (previousContentElement != null)
                                {
                                    var staleWait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
                                    staleWait.Until(ExpectedConditions.StalenessOf(previousContentElement));
                                    Logger.Debug("Previous chapter content element became stale — SPA transition detected");
                                }
                            }

                            if (!string.IsNullOrEmpty(authElementXPath))
                            {
                                Logger.Debug($"\nWaiting for auth verification element to reappear after SPA navigation to chapter {currentChapter}/{totalChapters}");
                                navWait.Until(ExpectedConditions.ElementIsVisible(By.XPath(authElementXPath)));
                            }

                            skipNavigation = true;
                            Logger.Debug($"SPA navigation to {chapterLink.Url} succeeded (chapter {currentChapter}/{totalChapters})");
                        }
                        catch (WebDriverTimeoutException)
                        {
                            Logger.Warn($"SPA navigation failed for {chapterLink.Url}, falling back to full navigation");

                            await driver.Navigate().GoToUrlAsync(chapterLink.Url).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(authElementXPath))
                            {
                                try
                                {
                                    var authWait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                                    authWait.Until(ExpectedConditions.ElementExists(By.XPath(authElementXPath)));
                                    Logger.Debug("Auth verification passed after fallback full navigation");
                                }
                                catch (WebDriverTimeoutException)
                                {
                                    Logger.Warn($"Auth verification timed out after fallback for {chapterLink.Url}");
                                }
                            }

                            skipNavigation = true;
                        }
                    }

                    var chapterDataBuffer = await GetChapterDataWithSeleniumSafelyAsync(
                        driver,
                        chapterLink,
                        tempImageDirectory,
                        currentChapter,
                        totalChapters,
                        skipNavigation).ConfigureAwait(false);
                    chapterDataBuffers.Add(chapterDataBuffer);
                    previousChapterFailed = chapterDataBuffer.IsPartial;
                }
                catch (Exception exception)
                {
                    Logger.Error(
                        exception,
                        $"Failed to process chapter {chapterLink.Url} during SPA navigation. Recording the failed chapter and continuing.");
                    chapterDataBuffers.Add(CreateFailedChapterDataBuffer(chapterLink, tempImageDirectory));
                    previousChapterFailed = true;
                }
            }

            Console.Write("\r" + new string(' ', 80) + "\r");
            Logger.Info($"Finished getting chapters data via SPA navigation. Total chapters: {chapterDataBuffers.Count}");
        }

        private async Task ProcessChaptersWithHttpClient(
            IList<ChapterLink> chapterLinks,
            List<ChapterDataBuffer> chapterDataBuffers,
            string tempImageDirectory)
        {
            Logger.Debug("Using HttpClient to get chapters data");

            var chapterTasks = chapterLinks.Select(chapterLink =>
                GetChapterDataWithHttpClientSafelyAsync(chapterLink, tempImageDirectory));
            var chapterResults = await Task.WhenAll(chapterTasks).ConfigureAwait(false);
            chapterDataBuffers.AddRange(chapterResults);

            Logger.Info("Finished getting chapters data");
        }

        private async Task<ChapterDataBuffer> GetChapterDataWithHttpClientSafelyAsync(
            ChapterLink chapterLink,
            string tempImageDirectory)
        {
            var semaphoreWasEntered = false;
            try
            {
                await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
                semaphoreWasEntered = true;
                ReportChapterProcessing(chapterLink);

                var chapterDataBuffer = await GetChapterDataAsync(
                    chapterLink.Url,
                    tempImageDirectory).ConfigureAwait(false);
                chapterDataBuffer.SequenceNumber = chapterLink.ChapterNumber;
                if (string.IsNullOrWhiteSpace(chapterDataBuffer.Title))
                {
                    chapterDataBuffer.Title = chapterLink.Title ?? chapterLink.Url;
                }

                return chapterDataBuffer;
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    $"Failed to process chapter {chapterLink.Url}. Recording the failed chapter and continuing.");
                return CreateFailedChapterDataBuffer(chapterLink, tempImageDirectory);
            }
            finally
            {
                if (semaphoreWasEntered)
                {
                    _semaphoreSlim.Release();
                }
            }
        }

        private void SetSiteConfiguration(SiteConfiguration siteConfig) => ScraperData.SiteConfig = siteConfig;

        private void SetSiteTableOfContents(Uri siteTableOfContents) => ScraperData.SiteTableOfContents = siteTableOfContents;

        private void SetConcurrentRequestLimit(int concurrentRequestLimit)
        {
            if (concurrentRequestLimit < 1)
            {
                throw new ArgumentException("Concurrent request limit must be greater than 0");
            }

            if (concurrentRequestLimit > Environment.ProcessorCount)
            {
                concurrentRequestLimit = Environment.ProcessorCount;
            }

            this.ConcurrentRequestsLimit = concurrentRequestLimit;
        }

        private async Task<ChapterDataBuffer> GetChapterDataAsync(
            IWebDriver driver,
            ChapterLink chapterLink,
            string tempImageDirectory,
            int currentChapter = 0,
            int totalChapters = 0,
            bool skipNavigation = false)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var uriLastSegment = new Uri(chapterLink.Url).Segments.Last();
            var waitTarget = ScraperData.SiteConfig.HasImagesForChapterContent ? "Image content" : "Text content";

#pragma warning disable CA2000 // Dispose objects before losing scope. I don't dispose the chapterDataBuffer yet
#pragma warning disable CA2000 // Dispose objects before losing scope. I don't dispose the chapterDataBuffer yet
            var chapterDataBuffer = new ChapterDataBuffer
            {
                TempDirectory = tempImageDirectory,
                Url = chapterLink.Url,
                SequenceNumber = chapterLink.ChapterNumber
            };
#pragma warning restore CA2000
            using var spinnerCancellationTokenSource = new CancellationTokenSource();
            Task? spinnerTask = null;

            try
            {
                if (totalChapters > 0)
                {
                    spinnerTask = Task.Run(
                        async () =>
                        {
                            var spinner = _function;
                            var spinnerIndex = 0;
                            while (!spinnerCancellationTokenSource.Token.IsCancellationRequested)
                            {
                                Console.Write($"\rLoading chapter {currentChapter}/{totalChapters}, waiting for {waitTarget} {spinner[spinnerIndex]} ");
                                spinnerIndex = (spinnerIndex + 1) % spinner.Length;
                                try
                                {
                                    await Task.Delay(150, spinnerCancellationTokenSource.Token).ConfigureAwait(false);
                                }
                                catch (TaskCanceledException)
                                {
                                    break;
                                }
                            }
                        },
                        spinnerCancellationTokenSource.Token);
                }

                if (!skipNavigation)
                {
                    await driver.Navigate().GoToUrlAsync(chapterLink.Url).ConfigureAwait(false);
                }

                try
                {
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
                    var chapterContentSelector = By.XPath(ScraperData.SiteConfig.Selectors.ChapterContent!);

                    if (ScraperData.SiteConfig.HasImagesForChapterContent)
                    {
                        var imageUrlAttribute =
                            ScraperData.SiteConfig.Selectors.ChapterContentImageUrlAttribute ?? string.Empty;
                        wait.Until(currentDriver =>
                        {
                            try
                            {
                                var chapterImageElements = currentDriver.FindElements(chapterContentSelector);
                                return AreChapterImageUrlsLoaded(chapterImageElements.Select(imageElement =>
                                    imageElement.GetAttribute(imageUrlAttribute)));
                            }
                            catch (StaleElementReferenceException)
                            {
                                return false;
                            }
                        });
                    }
                    else
                    {
                        wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(chapterContentSelector));
                    }
                }
                catch (WebDriverTimeoutException ex)
                {
                    Logger.Error($"Timeout while waiting for elements on page {chapterLink.Url}: {ex.Message}");
                    chapterDataBuffer.Title = uriLastSegment;
                    chapterDataBuffer.Content = "No content found";
                    chapterDataBuffer.DateLastModified = DateTime.Now;
                    chapterDataBuffer.IsPartial = true;

                    return chapterDataBuffer;
                }

                if (totalChapters > 0)
                {
                    Console.Write(
                        $"\r{new string(' ', 100)}\rLoading chapter {currentChapter}/{totalChapters}, waiting for {waitTarget} - ({stopwatch.ElapsedMilliseconds} ms)");
                }

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(driver.PageSource);

                var titleNode = htmlDocument.DocumentNode.SelectSingleNode(ScraperData.SiteConfig.Selectors.ChapterTitle ?? string.Empty);
                chapterDataBuffer.Title = NormalizeChapterTitle(titleNode?.InnerText);
                Logger.Debug($"Chapter title: {chapterDataBuffer.Title}");

                var contentNodes = htmlDocument.DocumentNode.SelectNodes(ScraperData.SiteConfig.Selectors.ChapterContent ?? string.Empty);

                if (ScraperData.SiteConfig.HasImagesForChapterContent)
                {
                    chapterDataBuffer = await AddImagePagesContentToChapterDataBuffer(
                        chapterDataBuffer,
                        contentNodes,
                        stopwatch,
                        tempImageDirectory).ConfigureAwait(false);
                }
                else
                {
                    chapterDataBuffer =
                        AddTextContentToChapterDataBuffer(htmlDocument, chapterDataBuffer, contentNodes, chapterLink.Url);
                }

                chapterDataBuffer.DateLastModified = DateTime.Now;
                return chapterDataBuffer;
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    $"Failed to process chapter {chapterLink.Url}. Recording the failed chapter and continuing.");
                chapterDataBuffer.Title = string.IsNullOrWhiteSpace(chapterDataBuffer.Title)
                    ? chapterLink.Title ?? uriLastSegment
                    : chapterDataBuffer.Title;
                chapterDataBuffer.Content ??= "No content found";
                chapterDataBuffer.DateLastModified = DateTime.Now;
                chapterDataBuffer.IsPartial = true;
                return chapterDataBuffer;
            }
            finally
            {
                await StopChapterLoadingSpinnerAsync(
                    spinnerCancellationTokenSource,
                    spinnerTask).ConfigureAwait(false);
            }
        }

        private async Task<ChapterDataBuffer> GetChapterDataWithSeleniumSafelyAsync(
            IWebDriver driver,
            ChapterLink chapterLink,
            string tempImageDirectory,
            int currentChapter,
            int totalChapters,
            bool skipNavigation = false)
        {
            try
            {
#pragma warning disable CA2007
                return await GetChapterDataAsync(
                    driver,
                    chapterLink,
                    tempImageDirectory,
                    currentChapter,
                    totalChapters,
                    skipNavigation);
#pragma warning restore CA2007
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    $"Failed to process chapter {chapterLink.Url}. Recording the failed chapter and continuing.");
                return CreateFailedChapterDataBuffer(chapterLink, tempImageDirectory);
            }
        }

        private async Task<ChapterDataBuffer> AddImagePagesContentToChapterDataBuffer(
            ChapterDataBuffer chapterDataBuffer,
            HtmlNodeCollection contentNodes,
            Stopwatch stopwatch,
            string tempImageDirectory)
        {
            var pageUrls = contentNodes.Select(pageUrl =>
                pageUrl.Attributes[ScraperData.SiteConfig.Selectors.ChapterContentImageUrlAttribute ?? string.Empty].Value);

            var urls = pageUrls as string[] ?? pageUrls.ToArray();
            var isValidHttpUrls = urls.Select(url =>
                    Uri.TryCreate(url, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp ||
                        uriResult.Scheme == Uri.UriSchemeHttps))
                .All(value => value);

            if (!isValidHttpUrls)
            {
                Logger.Error("Invalid page urls");
                chapterDataBuffer.IsPartial = true;
                return chapterDataBuffer;
            }

            chapterDataBuffer.SetPages(new List<PageData>());
            var counter = 0;
            Console.ForegroundColor = ConsoleColor.Cyan;
            foreach (var url in urls)
            {
                Logger.Debug($"Getting page image from {url}");
                stopwatch.Restart();
                var imagePath = await DownloadImageAsync(new Uri(url), tempImageDirectory).ConfigureAwait(false);
                Logger.Debug($"Finished getting page image from {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
                Console.Write($"\r Downloaded page {++counter}/{contentNodes.Count} - Time taken: {stopwatch.ElapsedMilliseconds} ms");
                chapterDataBuffer.Pages?.Add(new PageData
                {
                    Url = url,
                    ImagePath = imagePath
                });
            }

            Console.WriteLine();
            Console.ResetColor();

            return chapterDataBuffer;
        }

        private async Task<ChapterDataBuffer> GetChapterDataAsync(string url, string tempImageDirectory)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

#pragma warning disable CA2000
            var chapterDataBuffer = new ChapterDataBuffer
            {
                TempDirectory = tempImageDirectory
            };
#pragma warning restore CA2000

            try
            {
                Logger.Debug($"Navigating to {url}");
                var (htmlDocument, uri) = await LoadHtmlAsync(new Uri(url)).ConfigureAwait(false);
                Logger.Info($"Finished navigating to {url} Time taken: {stopwatch.ElapsedMilliseconds} ms");
                stopwatch.Restart();

                var titleNode = htmlDocument.DocumentNode.SelectSingleNode(ScraperData.SiteConfig.Selectors.ChapterTitle ?? string.Empty);
                chapterDataBuffer.Title =
                    titleNode != null ? NormalizeChapterTitle(titleNode.InnerText) : "Unknown Title";
                Logger.Debug($"Chapter title: {chapterDataBuffer.Title}");

                var contentNodes = htmlDocument.DocumentNode.SelectNodes(ScraperData.SiteConfig.Selectors.ChapterContent ?? string.Empty);
                if (ScraperData.SiteConfig.HasImagesForChapterContent)
                {
                    chapterDataBuffer = await AddImagePagesContentToChapterDataBuffer(
                        chapterDataBuffer,
                        contentNodes,
                        stopwatch,
                        tempImageDirectory).ConfigureAwait(false);
                }
                else
                {
                    chapterDataBuffer = AddTextContentToChapterDataBuffer(
                        htmlDocument,
                        chapterDataBuffer,
                        contentNodes,
                        url);
                }

                Logger.Info($"Finished processing chapter data. Time taken: {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to process chapter {url}. Recording the failed chapter and continuing.");
                chapterDataBuffer.Content = "No content found";
                chapterDataBuffer.IsPartial = true;
            }
            finally
            {
                chapterDataBuffer.DateLastModified = DateTime.Now;
                chapterDataBuffer.Url = url;
            }

            return chapterDataBuffer;
        }

        private ChapterDataBuffer AddTextContentToChapterDataBuffer(
            HtmlDocument htmlDocument,
            ChapterDataBuffer chapterDataBuffer,
            HtmlNodeCollection? paragraphNodes,
            string url)
        {
            var isWuxiaWorld =
                string.Equals(ScraperData.SiteConfig.SiteName, "Wuxiaworld", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ScraperData.SiteConfig.UrlPattern, "wuxiaworld.com", StringComparison.OrdinalIgnoreCase);

            var paragraphs = paragraphNodes != null
                ? (isWuxiaWorld
                    ? paragraphNodes.Select(p => ExtractWuxiaWorldParagraphText(p)).ToList()
                    : paragraphNodes.Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList())
                : new List<string>();

            var minParagraphThreshold = ScraperData.SiteConfig.MinimumChapterParagraphThreshold ??
                                        _defaultMinimumParagraphThreshold;

            // Guard: Try alternative selector if paragraph count is too low
            if (paragraphs.Count < minParagraphThreshold)
            {
                Logger.Warn(
                    $"Chapter content node count ({paragraphs.Count}) is below threshold ({minParagraphThreshold}) for {url}. Trying AlternativeChapterContent selector.");

                var altSelector = ScraperData.SiteConfig.Selectors.AlternativeChapterContent;
                if (!string.IsNullOrWhiteSpace(altSelector))
                {
                    var alternateParagraphNodes = htmlDocument.DocumentNode.SelectNodes(altSelector);
                    var alternateParagraphs = alternateParagraphNodes != null
                        ? (isWuxiaWorld
                            ? alternateParagraphNodes.Select(p => ExtractWuxiaWorldParagraphText(p)).ToList()
                            : alternateParagraphNodes
                                .Select(paragraph => HtmlEntity.DeEntitize(paragraph.InnerText.Trim())).ToList())
                        : new List<string>();

                    Logger.Debug($"AlternativeChapterContent node count: {alternateParagraphs.Count}");

                    // Use alternate paragraphs if they provide more content
                    if (alternateParagraphs.Count > paragraphs.Count)
                    {
                        Logger.Debug("AlternativeChapterContent yielded more nodes; using it.");
                        paragraphs = alternateParagraphs;
                    }
                }
            }

            chapterDataBuffer.Content = string.Join("\n", paragraphs);

            // More robust than counting newlines: check actual non-whitespace character count.
            var nonWhitespaceCharCount = chapterDataBuffer.Content.Count(c => !char.IsWhiteSpace(c));

            if (nonWhitespaceCharCount < 20)
            {
                Logger.Debug(
                    $"No/insufficient text content found for {url} (non-whitespace chars: {nonWhitespaceCharCount}).");
                chapterDataBuffer.Content = "No content found";
                chapterDataBuffer.IsPartial = true;
            }

            return chapterDataBuffer;
        }

        protected sealed class SeleniumPageStep
        {
            public SeleniumPageStepType Type { get; }

            public string XPath { get; }

            public string? Description { get; }

            public int? TimeoutSeconds { get; }

            public Func<IWebDriver, WebDriverWait, Task>? CustomAction { get; }

            private SeleniumPageStep(
                SeleniumPageStepType type,
                string xPath,
                string? description,
                int? timeoutSeconds,
                Func<IWebDriver, WebDriverWait, Task>? customAction)
            {
                Type = type;
                XPath = xPath;
                Description = description;
                TimeoutSeconds = timeoutSeconds;
                CustomAction = customAction;
            }

            public static SeleniumPageStep Click(
                string xPath,
                string? description = null,
                int? timeoutSeconds = null)
            {
                return new SeleniumPageStep(SeleniumPageStepType.Click, xPath, description, timeoutSeconds, null);
            }

            public static SeleniumPageStep WaitForPresence(
                string xPath,
                string? description = null,
                int? timeoutSeconds = null)
            {
                return new SeleniumPageStep(SeleniumPageStepType.WaitForPresence, xPath, description, timeoutSeconds, null);
            }

            public static SeleniumPageStep WaitForClickable(
                string xPath,
                string? description = null,
                int? timeoutSeconds = null)
            {
                return new SeleniumPageStep(SeleniumPageStepType.WaitForClickable, xPath, description, timeoutSeconds, null);
            }

            public static SeleniumPageStep Custom(
                Func<IWebDriver, WebDriverWait, Task> customAction,
                string? description = null,
                int? timeoutSeconds = null)
            {
                return new SeleniumPageStep(SeleniumPageStepType.Custom, "(custom)", description, timeoutSeconds, customAction);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    namespace Impl
    {
        // The NovelDataInitializer represents an abstraction around populating a NovelDataBuffer object with its required content.
        // Each strategy implements a corresponding child of the NovelDataInitializer class which is used to neatly
        // encapsulate the strategy-specific content fetching functions in the Impl namespace.
        //
        // Note that the data & methods used by the Initializer are static so that the strategies do not have to
        // contain a data member of the class in order to call the given FetchNovelContent method (implemented on each
        // child class).
#pragma warning disable CA1052
#pragma warning disable RCS1102
        internal abstract class NovelDataInitializer
#pragma warning restore RCS1102
#pragma warning restore CA1052
        {
            public enum Attr
            {
                Title,
                Author,
                Category,
                NovelRating,
                TotalRatings,
                Description,
                Genres,
                AlternativeNames,
                NovelStatus,
                ThumbnailUrl,
                LastTableOfContentsPage,
                ChapterUrls,
                FirstChapterUrl,
                CurrentChapterUrl,
                AlternateLastTableOfContentsPage
            }

            public static async Task FetchContentByAttributeAsync(
                Attr attr,
                NovelDataBuffer novelDataBuffer,
                HtmlDocument htmlDocument,
                ScraperData scraperData)
            {
                switch (attr)
                {
                    case Attr.Title:
                        var titleNodes =
                            htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig.Selectors.TableOfContents
                                .NovelTitle ?? string.Empty);
                        if (titleNodes != null && titleNodes.Count != 0)
                        {
                            novelDataBuffer.Title = HtmlEntity.DeEntitize(titleNodes.First().InnerText.Trim());
                        }

                        Console.WriteLine($"Title: {novelDataBuffer.Title}");
                        break;

                    case Attr.Author:
                        var authorNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig.Selectors.TableOfContents
                                .NovelAuthor ?? string.Empty);
                        novelDataBuffer.Author = authorNode != null
                            ? HtmlEntity.DeEntitize(authorNode.InnerText.Trim())
                            : string.Empty;
                        Console.WriteLine($"Author: {novelDataBuffer.Author}");
                        break;

                    case Attr.Category:
                        // TODO: Implement
                        break;

                    case Attr.NovelRating:
                        var novelRatingNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig.Selectors.TableOfContents
                                .NovelRating ?? string.Empty);
                        if (novelRatingNode != null &&
                            double.TryParse(novelRatingNode.InnerText.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double rating))
                        {
                            novelDataBuffer.Rating = rating;
                            Console.WriteLine($"Rating: {novelDataBuffer.Rating}");
                        }

                        break;

                    case Attr.TotalRatings:
                        var totalRatingsNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig.Selectors.TableOfContents
                                .TotalRatings ?? string.Empty);
                        if (totalRatingsNode != null &&
                            int.TryParse(totalRatingsNode.InnerText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int totalRatings))
                        {
                            novelDataBuffer.TotalRatings = totalRatings;
                            Console.WriteLine($"Total Ratings: {novelDataBuffer.TotalRatings}");
                        }

                        break;

                    case Attr.Description:
                        var descriptionNodes =
                            htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig.Selectors.TableOfContents
                                .NovelDescription ?? string.Empty);
                        if (descriptionNodes != null)
                        {
                            novelDataBuffer.Description.ReplaceWith(descriptionNodes
                                .Select(description => HtmlEntity.DeEntitize(description.InnerText.Trim())));
                            Console.WriteLine($"Description line count: {novelDataBuffer.Description.Count}");
                        }

                        break;

                    case Attr.Genres:
                        var genreNodes =
                            htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig.Selectors.TableOfContents
                                .NovelGenres ?? string.Empty);
                        if (genreNodes != null && genreNodes.Count != 0)
                        {
                            novelDataBuffer.Genres.ReplaceWith(genreNodes
                                .Select(genre => HtmlEntity.DeEntitize(genre.InnerText.Trim())));
                            Console.WriteLine($"Total Genres: {novelDataBuffer.Genres.Count}");
                            Console.WriteLine($"Genres: {string.Join(", ", novelDataBuffer.Genres)}");
                        }

                        break;

                    case Attr.AlternativeNames:
                        var alternateNameNodes = htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig.Selectors
                            .TableOfContents.NovelAlternativeNames ?? string.Empty);
                        if (alternateNameNodes != null && alternateNameNodes.Count != 0)
                        {
                            List<string> alternateNames = alternateNameNodes.Select(alternateName =>
                                HtmlEntity.DeEntitize(alternateName.InnerText.Trim())).ToList();
                            if (alternateNames.Count != 0)
                            {
                                // SelectMany flattens a list of lists into a single list.
                                novelDataBuffer.AlternativeNames.ReplaceWith(alternateNames.SelectMany(SplitByLanguage)
                                    .Where(altName => !string.IsNullOrWhiteSpace(altName))
                                    .Select(altName => altName.Trim())
                                    .Distinct());
                            }
                        }

                        Console.WriteLine($"Alternate Names: {string.Join(", ", novelDataBuffer.AlternativeNames)}");
                        break;

                    case Attr.NovelStatus:
                        var statusNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig.Selectors.TableOfContents
                                .NovelStatus ?? string.Empty);
                        if (statusNode != null && scraperData?.SiteConfig.CompletedStatus != null)
                        {
                            novelDataBuffer.NovelStatus = statusNode.InnerText.Trim();
                            novelDataBuffer.IsNovelCompleted = novelDataBuffer.NovelStatus.ToLowerInvariant()
                                .Contains(scraperData.SiteConfig.CompletedStatus, StringComparison.OrdinalIgnoreCase);
                            Console.WriteLine($"Novel Status: {novelDataBuffer.NovelStatus}");
                        }

                        break;

                    case Attr.ThumbnailUrl:
                        var urlNode = htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig.Selectors
                            .TableOfContents.NovelThumbnailUrl ?? string.Empty);
                        var thumbnailUrlAttribute = scraperData.SiteConfig.Selectors.TableOfContents.ThumbnailUrlAttribute ?? string.Empty;

                        // Guard: Check if URL node exists and has the required attribute
                        if (urlNode == null || urlNode.Attributes == null || urlNode.Attributes[thumbnailUrlAttribute] == null)
                        {
                            break;
                        }

                        var url = urlNode
                            .Attributes[thumbnailUrlAttribute].Value;
                        bool isValidHttpUrl = Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                                              (uriResult.Scheme == Uri.UriSchemeHttp ||
                                               uriResult.Scheme == Uri.UriSchemeHttps);
                        Uri absoluteUri = isValidHttpUrl ? new Uri(url) : new Uri(scraperData.BaseUri, url);

                        using (var client = scraperData.HttpClientFactory?.CreateClient() ?? new HttpClient())
                        {
                            try
                            {
                                var thumbnailBytes = await client.GetByteArrayAsync(absoluteUri).ConfigureAwait(false);
                                novelDataBuffer.SetThumbnailImage(thumbnailBytes);
                            }
                            catch (Exception e)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(
                                    $"Failed to download thumbnail image for novel {novelDataBuffer.Title} at url {absoluteUri}. Exception: {e}");
                                Console.ResetColor();
                            }
                        }

                        novelDataBuffer.ThumbnailUrl = absoluteUri.ToString();
                        Console.WriteLine($"ThumbnailUrl: {novelDataBuffer.ThumbnailUrl}");
                        break;

                    case Attr.LastTableOfContentsPage:
                        try
                        {
                            var lastTableOfContentsPageAttribute = scraperData.SiteConfig.Selectors.TableOfContents
                                .LastTableOfContentPageNumberAttribute;
                            lastTableOfContentsPageAttribute = string.IsNullOrWhiteSpace(lastTableOfContentsPageAttribute)
                                ? "href"
                                : lastTableOfContentsPageAttribute;
                            var lastTableOfContentsPageNode =
                                htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig.Selectors
                                    .TableOfContents.LastTableOfContentsPage ?? string.Empty);

                            if (lastTableOfContentsPageNode == null)
                            {
                                break;
                            }

                            var lastTableOfContentsPageValue = lastTableOfContentsPageNode
                                .GetAttributeValue(lastTableOfContentsPageAttribute, string.Empty);
                            if (string.IsNullOrWhiteSpace(lastTableOfContentsPageValue) &&
                                !string.Equals(lastTableOfContentsPageAttribute, "href", StringComparison.OrdinalIgnoreCase))
                            {
                                lastTableOfContentsPageValue = lastTableOfContentsPageNode
                                    .GetAttributeValue("href", string.Empty);
                            }

                            novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsPageValue;
                            Console.WriteLine(
                                $"Last Table of Contents Page: {novelDataBuffer.LastTableOfContentsPageUrl}");
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(
                                $"Failed to get last table of contents page for novel {novelDataBuffer.Title} at url {scraperData.SiteTableOfContents}. Exception: {e}");
                            Console.ResetColor();
                            throw;
                        }

                    case Attr.ChapterUrls:
                        var chapterLinkNodes =
                            htmlDocument.DocumentNode.SelectNodes(scraperData.SiteConfig.Selectors.TableOfContents
                                .ChapterLinks ?? string.Empty);

                        if (chapterLinkNodes == null)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(
                                $"No chapter link nodes found for novel {novelDataBuffer.Title} at url {scraperData.BaseUri}");
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine(
                                $"Please check the site configuration for {scraperData.SiteConfig.SiteName}, specifically the chapterLinks property.");
                            Console.ResetColor();
                            break;
                        }

                        foreach (var chapterLinkNode in chapterLinkNodes)
                        {
                            var isPremium = !string.IsNullOrEmpty(scraperData.SiteConfig.Selectors.TableOfContents
                                .PremiumChapterSelectors.PremiumIndicator)
                                ? chapterLinkNode.SelectSingleNode(
                                    scraperData.SiteConfig.Selectors.TableOfContents.PremiumChapterSelectors
                                        .PremiumIndicator!) != null
                                : false;
                            var premiumCost = isPremium && !string.IsNullOrEmpty(scraperData.SiteConfig.Selectors
                                .TableOfContents.PremiumChapterSelectors.PremiumCost)
                                ? chapterLinkNode.SelectSingleNode(scraperData.SiteConfig.Selectors.TableOfContents
                                    .PremiumChapterSelectors.PremiumCost!)?.InnerText
                                : "0";
                            var chapterUrl = chapterLinkNode.Attributes["href"].Value;
                            var chapterTitle =
                                !string.IsNullOrEmpty(
                                    scraperData.SiteConfig.Selectors.TableOfContents.ChapterTitleInToc)
                                    ? HtmlEntity.DeEntitize(chapterLinkNode
                                        .SelectSingleNode(scraperData.SiteConfig.Selectors.TableOfContents
                                            .ChapterTitleInToc!)?.InnerText?.Trim() ?? string.Empty)
                                    : HtmlEntity.DeEntitize(chapterLinkNode.InnerText?.Trim() ?? string.Empty);
                            chapterUrl = chapterUrl != null && !IsValidHttpUrl(chapterUrl)
                                ? new Uri(scraperData.BaseUri, chapterUrl).ToString()
                                : chapterUrl;
                            var chapterLink = new ChapterLink()
                            {
                                Url = chapterUrl!,
                                Title = chapterTitle,
                                PremiumInfo = isPremium
                                    ? new PremiumChapterInfo()
                                    {
                                        IsPremium = isPremium,
                                        Cost = int.TryParse(premiumCost, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPremiumCost) ? parsedPremiumCost : 0,
                                        CurrencyName = scraperData.SiteConfig.PremiumInfo?.CurrencyName ?? "Credits"
                                    }
                                    : new PremiumChapterInfo()
                            };
                            novelDataBuffer.ChapterLinks.Add(chapterLink);
                        }

                        Console.WriteLine($"Got chapter urls, total: {novelDataBuffer.ChapterLinks.Count}");
                        break;

                    case Attr.FirstChapterUrl:
                        // TODO: Implement
                        break;

                    case Attr.CurrentChapterUrl:
                        var latestChapterNode =
                            htmlDocument.DocumentNode.SelectSingleNode(scraperData.SiteConfig.Selectors.TableOfContents.LatestChapterLink ?? string.Empty);

                        var currentChapterUrl = latestChapterNode.Attributes["href"].Value;

                        if (!IsValidHttpUrl(currentChapterUrl))
                        {
                            currentChapterUrl = new Uri(scraperData.BaseUri, currentChapterUrl).ToString();
                        }

                        novelDataBuffer.CurrentChapterUrl = currentChapterUrl;

                        novelDataBuffer.MostRecentChapterTitle =
                            HtmlEntity.DeEntitize(latestChapterNode.InnerText).Trim();
                        Console.WriteLine($"Latest Chapter: {novelDataBuffer.MostRecentChapterTitle}");
                        break;
                    case Attr.AlternateLastTableOfContentsPage:
                    default:
                        Console.WriteLine($"Case: {attr} is not implemented");
                        break;
                }
            }

            /// <summary>
            /// Splits a string into a list of strings, each containing only characters from either the Asian or Latin alphabet.
            /// </summary>
            /// <param name="input">The string to split into language-homogeneous segments.</param>
            /// <returns>A list of substrings, each containing characters from a single alphabet (Asian or Latin).</returns>
            public static IList<string> SplitByLanguage(string input)
            {
                List<string> result = new List<string>();
                StringBuilder currentString = new StringBuilder();
                bool? isLastCharAsian = null;

                foreach (char c in input)
                {
                    // http://www.rikai.com/library/kanjitables/kanji_codes.unicode.shtml
                    bool isCurrentCharAsian =
                        (c >= 0x3000 && c <= 0x9FFF) || (c >= 0x4E00 && c <= 0x9FFF); // range of Asian characters

                    if (isLastCharAsian.HasValue && isCurrentCharAsian != isLastCharAsian)
                    {
                        result.Add(currentString.ToString().Trim());
                        currentString.Clear();
                    }

                    currentString.Append(c);
                    isLastCharAsian = isCurrentCharAsian;
                }

                result.Add(currentString.ToString().Trim());

                return result;
            }

            public static bool IsValidHttpUrl(string url)
            {
                return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) &&
                       (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            }
        }
    }

    internal sealed class ScraperData
    {
        public SiteConfiguration SiteConfig { get; set; } = null!;

        public Uri SiteTableOfContents { get; set; } = null!;

        public Uri BaseUri { get; set; } = null!;

        public IHttpClientFactory? HttpClientFactory { get; set; }

        public SelectedChapterRange? ChapterRange { get; set; }

        public string? DetectedVolumeName { get; set; }

        public IList<OpenQA.Selenium.Cookie>? LoginCookies { get; init; }

        public bool IsSessionAuthenticated { get; set; }
    }
}