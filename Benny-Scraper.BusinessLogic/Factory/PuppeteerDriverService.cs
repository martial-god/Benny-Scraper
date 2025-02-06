using System.Collections.Concurrent;
using HtmlAgilityPack;
using NLog;
using PuppeteerSharp;

namespace Benny_Scraper.BusinessLogic.Factory;

public interface IPuppeteerDriverService
{
    Task<IBrowser> GetBrowserAsync(bool headless = true);
    Task<IPage> CreatePageAsync(Uri uri, bool headless = true);
    Task CloseBrowserAsync();
    Task<HtmlDocument> GetPageContentAsync(IPage page);
    // Getting cookies for puppeteersharp https://webscraping.ai/faq/puppeteer-sharp/how-can-i-manage-cookies-in-puppeteer-sharp
}

public class PuppeteerDriverService : IPuppeteerDriverService, IDisposable
{
    private IBrowser? _browser;
    private readonly ConcurrentDictionary<string, IPage> _pages = new();
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    public async Task<IBrowser> GetBrowserAsync(bool headless = true)
    {
        if (_browser is not null && _browser.IsConnected)
            return _browser;
        var browserFetcher = new BrowserFetcher();

        var installedBrowsers = browserFetcher.GetInstalledBrowsers();
        await browserFetcher.DownloadAsync();
        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = headless, Args =
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
                "--headless=new" // this should handle headless detection for chrome 109+
            ]
        });

        return _browser;
    }

    public async Task<IPage> CreatePageAsync(Uri uri, bool headless = true)
    {
        var browser = await GetBrowserAsync(headless);
        var page = await browser.NewPageAsync();

        // Configure stealth
        await page.EvaluateExpressionOnNewDocumentAsync("() => { delete navigator.__proto__.webdriver }");

        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64)...");
        await page.GoToAsync(uri.ToString());
        _pages.TryAdd(uri.Host, page);
        return page;
    }
    
    public async Task<HtmlDocument> GetPageContentAsync(IPage page)
    {
        try
        {
            var htmlDocument = new HtmlDocument();
            var pageContent = await page.GetContentAsync();
            htmlDocument.LoadHtml(pageContent);
            return htmlDocument;
        }
        catch (Exception ex)
        {
            _logger.Fatal($"Error while getting page content for {page.Url}: {ex}");
            Dispose();
            throw new Exception($"Error while getting page content for {page.Url}", ex);
        }
    }
    
    public async Task CloseBrowserAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }
    }

    public void Dispose() => CloseBrowserAsync().Wait();
}