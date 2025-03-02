using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using Microsoft.Extensions.Options;
using NLog;

namespace Benny_Scraper.BusinessLogic;
/// <summary>
/// A implementation of the INovelScraper interface. The need for a site that requires a browser will be determined by the constructor of the relevant strategy..
/// </summary>
public class NovelScraper : INovelScraper
{
    private readonly Dictionary<string, ScraperStrategy> _strategies = new Dictionary<string, ScraperStrategy>();
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private readonly IPuppeteerDriverService _puppeteerDriverService;

    public NovelScraper(IPuppeteerDriverService puppeteerDriverService, IOptions<NovelScraperSettings> settings)
    {
        _puppeteerDriverService = puppeteerDriverService; // need to either handle the class or create another for non http constructor
        RegisterStrategy();
    }

    /// <summary>
    /// Returns the scraper strategy for the given site. If no strategy is found, null is returned. Classes are added to the map in the constructor.
    /// </summary>
    /// <exception cref="NotSupportedException">"No scraper strategy found for </exception>
    /// <param name="novelTableOfContentsUri"></param>
    /// <param name="siteConfig"></param>
    /// <returns></returns>
    public ScraperStrategy GetScraperStrategy(Uri novelTableOfContentsUri, SiteConfiguration siteConfig)
    {
        string baseUrl = novelTableOfContentsUri.GetLeftPart(UriPartial.Authority);

        return _strategies.TryGetValue(baseUrl, out var strategy) ? strategy :
            throw new NotSupportedException($"No scraper strategy found for {baseUrl}");
    }

    #region setup maps
    private void AddStrategy(string siteName, ScraperStrategy scraperStrategy)
    {
        _strategies.Add(siteName, scraperStrategy);
    }

    private void RegisterStrategy()
    {
        AddStrategy("https://www.lightnovelworld.com", new LightNovelWorldStrategy(_puppeteerDriverService));
        AddStrategy("https://novelfull.com", new NovelFullStrategy());
        AddStrategy("https://mangakakalot.to", new MangaKakalotStrategy());
        AddStrategy("https://mangareader.to", new MangaReaderStrategy());
        AddStrategy("https://mangakatana.com", new MangaKatanaStrategy(_puppeteerDriverService));
        AddStrategy("https://noveldrama.com", new NovelDramaStrategy());
    }
    #endregion
}
