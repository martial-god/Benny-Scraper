using System.Collections.Concurrent;
using System.Net;
using HtmlAgilityPack;
using NLog;
using PuppeteerSharp;

namespace Benny_Scraper.BusinessLogic.Factory;

public interface IPuppeteerDriverService
{
    Task<IBrowser> GetOrCreateBrowserAsync(bool headless = true);
    Task<IPage> CreatePageAndGoToAsync(Uri uri, bool headless = true);
    Task CloseBrowserAsync();
    Task<HtmlDocument> GetPageContentAsync(IPage page);
    Task<HtmlDocument> GetSafeSanitizedPageContentAsync(IPage page);
    Task<IPage> GetStealthPageAsync(bool headless = true);
    Task GoToAndWaitForByXpath(IPage page, string url, string selector, WaitForSelectorOptions options);
    void ReturnPage(IPage page);
    void Dispose();
}

public class PuppeteerDriverService : IPuppeteerDriverService, IDisposable
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private IBrowser? _browser;
    // this is only for pages not in use
    private readonly ConcurrentBag<IPage> _availablePages = new();
    private readonly int _maxPoolSize = 1;
    private bool _isDisposed;


    public async Task<IBrowser> GetOrCreateBrowserAsync(bool headless = true)
    {
        if (!headless)
            _logger.Info("<=========== Browser window will open to bypass Cloudflare protection.\r\n\tThis is normal - don't interact with it! =========>");
        if (_browser is not null && _browser.IsConnected)
            return _browser;

        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = headless,
            Args =
            [
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-web-security",
                "--disable-features=IsolateOrigins,site-per-process",
                "--disable-blink-features=AutomationControlled",
                "--no-first-run",
                "--no-service-autorun",
                "--no-default-browser-check",
                "--disable-extensions",
                "--window-size=1,1"
                //"--headless=new" // this should handle headless detection for chrome 109+
            ]
        });

        return _browser;
    }

    /// <summary>
    /// Headless should be true for most cases. If you need to bypass Cloudflare, set it to false.
    /// </summary>
    public async Task<IPage> CreatePageAndGoToAsync(Uri uri, bool headless = true)
    {
        var page = await GetStealthPageAsync(headless);
        var response = await page.GoToAsync(uri.ToString(), new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
            Timeout = 30000
        });
        
        if (response.Status != HttpStatusCode.OK)
            throw new Exception($"Failed to navigate to {uri}. Status code: {response.Status}");

        return page;
    }

    /// <summary>
    /// Gets the content of a page and returns it as an HtmlDocument.
    /// </summary>
    /// <param name="page"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="Exception"></exception>
    public async Task<HtmlDocument> GetPageContentAsync(IPage page)
    {
        try
        {
            var htmlDocument = new HtmlDocument();
            var pageContent = await page.GetContentAsync();
            htmlDocument.LoadHtml(pageContent);
            if (htmlDocument.DocumentNode is null || htmlDocument is null)
                throw new NullReferenceException($"Document is null. Possibly a navigation or rate-limit -error");
            return htmlDocument;
        }
        catch (Exception ex)
        {
            _logger.Fatal($"Error while getting page content for {page.Url}: {ex}");
            throw new Exception($"Error while getting page content for {page.Url}", ex);
        }
    }

    public async Task GoToAndWaitForByXpath(IPage page, string url, string selector, WaitForSelectorOptions options)
    {
        await page.GoToAsync(url);
        await page.WaitForSelectorAsync($"xpath/{selector}", options);
    }

    /// <summary>
    /// Sanitizes html to avoid JSON exceptions that Puppeteer has ThrowInvalidOperationException_ReadIncompleteUTF16().
    /// </summary>
    /// <param name="page"></param>
    /// <returns></returns>
    public async Task<string> EvaluateOuterHtmlSafe(IPage page)
    {
        const string script = @"
            () => {
              function stripInvalidSurrogates(str) {
                return str.replace(
                  /[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/g,
                  ''
                );
              }
              let html = document.documentElement.outerHTML;
              html = stripInvalidSurrogates(html);
              return html;
            }
            ";

        return await page.EvaluateFunctionAsync<string>(script);
    }

    /// <summary>
    /// This method is used to get the page content in a safe way to avoid JSON exceptions.
    /// </summary>
    /// <param name="page"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<HtmlDocument> GetSafeSanitizedPageContentAsync(IPage page)
    {
        try
        {
            var safeHtml = await EvaluateOuterHtmlSafe(page);
            var doc = new HtmlDocument();
            doc.LoadHtml(safeHtml);
            return doc;
        }
        catch (Exception ex)
        {
            _logger.Fatal($"Error while getting page content for {page.Url}: {ex}");
            throw new Exception($"Error while getting page content for {page.Url}", ex);
        }
    }

    /// <summary>
    /// Gets a "stealth" page from the pool (or creates a new one if below maxPoolSize).
    /// Does NOT navigate to a URL. 
    /// </summary>
    public async Task<IPage> GetStealthPageAsync(bool headless = true)
    {
        var browser = await GetOrCreateBrowserAsync(headless);

        if (_availablePages.TryTake(out var page))
        {
            try
            {
                if (page.Url == null)
                    await page
                        .CloseAsync(); // found cases where page will have a null url which will then start failing all following calls. Already removed by the TryTake

                if (!page.IsClosed)
                {
                    _logger.Debug("Reusing page from the pool.");
                    return page;
                }
                _logger.Debug("Pooled page was closed, discarding it. Will create a new one.");
            }
            catch (NullReferenceException ex)
            {
                _logger.Debug($"Page Url was most likely null or the page can no longer be used. Need a new browser");
                await CloseBrowserAsync();
                browser = await GetOrCreateBrowserAsync(headless);
            }
        }

        // If no page is available, see if we are under the max pool size
        var countOfPages = CountOpenPages();
        var browserPages = await _browser?.PagesAsync();
        var browserPagesCount = _browser?.PagesAsync().Result.Count();
        if (countOfPages < _maxPoolSize)
        {
            var newPage = await browser.NewPageAsync();

            await newPage.EvaluateExpressionOnNewDocumentAsync("() => { delete navigator.__proto__.webdriver }");
            await newPage.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64)...");

            _logger.Debug("Created a brand-new page for the pool.");
            _availablePages.Add(newPage);
            return newPage;
        }

        if (browserPages is not null)
        {
            _logger.Debug("Reusing page from the pool.");
            return browser.PagesAsync().Result.First();
        }

        _logger.Warn("Pool is full. Waiting for a page to be returned...");
        while (true)
        {
            await Task.Delay(500);
            if (_availablePages.TryTake(out var waitingPage))
            {
                if (!waitingPage.IsClosed)
                    return waitingPage;
            }
        }
    }

    /// <summary>
    /// Returns the page to the pool for future reuse (unless it’s closed).
    /// </summary>
    public void ReturnPage(IPage page)
    {
        if (page.IsClosed)
        {
            _logger.Debug("Not returning a closed page to the pool.");
            return;
        }

        _logger.Debug("Returning page to the pool.");
        _availablePages.Add(page);
    }

    public void CloseAllPages()
    {
        _logger.Debug($"Total pages: {CountOpenPages()}...");
        while (_availablePages.TryTake(out var page))
        {
            if (!page.IsClosed)
            {
                page.CloseAsync().Wait();
                _availablePages.Clear();
                _logger.Debug("Closed a page from the pool.");
            }
        }
    }

    private int CountOpenPages()
    {
        return _availablePages.Count + (_browser?.PagesAsync().Result?.Count() ?? 0);
    }

    public async Task CloseBrowserAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _availablePages.Clear();
            _browser = null;
            _logger.Debug("Closed the browser.");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        CloseAllPages();
        CloseBrowserAsync().Wait();
    }
}