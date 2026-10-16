using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Scrapers.Strategy;

namespace BennyScraper.BusinessLogic.Factory.Interfaces;

internal interface IScraperStrategyFactory
{
    ScraperStrategy? Create(Uri novelTableOfContentsUri, SiteConfiguration siteConfig);
}