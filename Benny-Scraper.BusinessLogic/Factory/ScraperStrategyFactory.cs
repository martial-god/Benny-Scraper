using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using NLog;

namespace BennyScraper.BusinessLogic.Factory;

internal sealed class ScraperStrategyFactory(
    IHttpClientFactory httpClientFactory,
    IDriverFactory driverFactory) : IScraperStrategyFactory
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public ScraperStrategy? Create(Uri novelTableOfContentsUri, SiteConfiguration siteConfig)
    {
        if (!siteConfig.IsActive)
        {
            _logger.Warn($"The site configuration for {siteConfig.SiteName} is inactive.");
            return null;
        }

        var strategyName = string.IsNullOrWhiteSpace(siteConfig.StrategyName) ? "common" : siteConfig.StrategyName;
        switch (strategyName)
        {
            case "common":
                return new CommonStrategy(httpClientFactory, driverFactory);
            case "lightnovelworld":
                return new LightNovelWorldStrategy(httpClientFactory, driverFactory);
            case "mangakakalot":
                return new MangaKakalotStrategy(httpClientFactory, driverFactory);
            case "mangakatana":
                return new MangaKatanaStrategy(httpClientFactory, driverFactory);
            case "mangareader":
                return new MangaReaderStrategy(httpClientFactory, driverFactory);
            case "novelbin":
                return new NovelBinStrategy(httpClientFactory, driverFactory);
            case "noveldrama":
                return new NovelDramaStrategy(httpClientFactory, driverFactory);
            case "novelfull":
                return new NovelFullStrategy(httpClientFactory, driverFactory);
            case "novelbuddy":
                return new NovelBuddyStrategy(httpClientFactory, driverFactory);
            case "royalroad":
                return new RoyalRoadStrategy(httpClientFactory, driverFactory);
            case "wanderinginn":
                return new WanderingInnStrategy(httpClientFactory, driverFactory);
            case "wuxiaworld":
                return new WuxiaWorldStrategy(httpClientFactory, driverFactory);
            default:
                _logger.Error($"No scraper strategy named {strategyName} for site {novelTableOfContentsUri.Host} found.");
                return null;
        }
    }
}