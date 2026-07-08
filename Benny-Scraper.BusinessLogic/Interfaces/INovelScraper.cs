using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Scrapers.Strategy;

namespace BennyScraper.BusinessLogic.Interfaces;

public interface INovelScraper
{
    public ScraperStrategy? GetScraperStrategy(Uri novelTableOfContentsUri, SiteConfiguration siteConfig);
}