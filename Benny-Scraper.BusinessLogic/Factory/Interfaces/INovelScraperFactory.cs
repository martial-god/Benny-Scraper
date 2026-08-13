using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Interfaces;

namespace BennyScraper.BusinessLogic.Factory.Interfaces;

internal interface INovelScraperFactory
{
    INovelScraper CreateScraper(Uri novelTableOfContentsUri, SiteConfiguration siteConfig);
}