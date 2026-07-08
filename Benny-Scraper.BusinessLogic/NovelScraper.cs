using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Interfaces;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using NLog;

namespace BennyScraper.BusinessLogic;

/// <summary>
/// Resolves the scraping strategy for a given site URL. Individual strategies decide whether
/// they use HttpClient or Selenium internally (e.g. WuxiaWorld uses Selenium for premium login).
/// </summary>
public class NovelScraper : INovelScraper
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private ScraperStrategy _scraperStrategy;
    private readonly Dictionary<string, ScraperStrategy> _websiteMap = new();

    public NovelScraper()
    {
        AddSupportForWebsite();
    }

    /// <summary>
    /// Returns the scraper strategy for the given site. If no strategy is found, null is returned. Classes are added to the map in the constructor.
    /// </summary>
    public ScraperStrategy? GetScraperStrategy(Uri novelTableOfContentsUri, SiteConfiguration siteConfig)
    {
        var baseUrl = novelTableOfContentsUri.GetLeftPart(UriPartial.Authority);

        if (_websiteMap.TryGetValue(baseUrl, out _scraperStrategy))
        {
            return _scraperStrategy;
        }

        Logger.Error($"No scraper strategy found for {baseUrl}");
        return null;
    }

    public IReadOnlyList<string> GetSupportedSites()
    {
        return _websiteMap.Select(website => website.Key).ToList();
    }

    private void AddSiteToMap(string siteName, ScraperStrategy scraperStrategy)
    {
        _websiteMap.Add(siteName, scraperStrategy);
    }

    private void AddSupportForWebsite()
    {
        AddSiteToMap("https://www.lightnovelworld.com", new LightNovelWorldStrategy());
        AddSiteToMap("https://mangakakalot.to", new MangaKakalotStrategy());
        AddSiteToMap("https://mangareader.to", new MangaReaderStrategy());
        AddSiteToMap("https://mangakatana.com", new MangaKatanaStrategy());
        AddSiteToMap("https://novelbin.me", new NovelBinStrategy());
        AddSiteToMap("https://novelbin.com", new NovelBinStrategy());
        AddSiteToMap("https://noveldrama.com", new NovelDramaStrategy());
        AddSiteToMap("https://novelfull.com", new NovelFullStrategy());
        AddSiteToMap("https://novlove.com", new NovelBinStrategy());
        AddSiteToMap("https://wanderinginn.com", new WanderingInnStrategy());
        AddSiteToMap("https://www.wuxiaworld.com", new WuxiaWorldStrategy());
        AddSiteToMap("https://www.royalroad.com", new RoyalRoadStrategy());
    }
}