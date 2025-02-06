using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using NLog;

namespace Benny_Scraper.BusinessLogic.Scrapers;

/// <summary>
/// This scraper is for sites that require execution of javascript, or has anti-bot protections
/// </summary>
public class PuppeteerNovelScraper : INovelScraper
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private ScraperStrategy _scraperStrategy;
    private readonly IPuppeteerDriverService _puppeteerService;
    private Dictionary<string, ScraperStrategy> _websiteMap = new();

    public PuppeteerNovelScraper()
    {
        _puppeteerService = new PuppeteerDriverService();
        AddSupportForWebsite();
    }

    #region Setup Maps
    public void AddSiteToMap(string siteName, ScraperStrategy scraperStrategy)
    {
        _websiteMap.Add(siteName, scraperStrategy);
    }

    void AddSupportForWebsite()
    {
        AddSiteToMap("https://www.lightnovelworld.com", new PuppeteerScraperStrategy(_puppeteerService));
        AddSiteToMap("https://mangadex.org", new PuppeteerScraperStrategy(_puppeteerService));
    }
    #endregion

    public ScraperStrategy GetScraperStrategy(Uri novelTableOfContentsUri, SiteConfiguration siteConfig)
    {
        string baseUrl = novelTableOfContentsUri.GetLeftPart(UriPartial.Authority);

        if (_websiteMap.TryGetValue(baseUrl, out _scraperStrategy))
        {
            return _scraperStrategy;
        }

        // Fallback to generic Puppeteer strategy
        return new PuppeteerScraperStrategy(_puppeteerService);
    }

    public List<string> GetSupportedSites() => _websiteMap.Keys.ToList();
}