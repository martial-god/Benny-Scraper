using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Scrapers.Strategy;

namespace BennyScraper.BusinessLogic.Interfaces;

internal interface INovelScraper
{
    public ScraperStrategy? GetScraperStrategy(Uri novelTableOfContentsUri, SiteConfiguration siteConfig);

    public IReadOnlyList<string> GetSupportedSites();
}