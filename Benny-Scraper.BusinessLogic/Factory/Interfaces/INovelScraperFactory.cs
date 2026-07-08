using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Interfaces;

namespace BennyScraper.BusinessLogic.Factory.Interfaces;

public interface INovelScraperFactory
{
    INovelScraper CreateScraper(Uri novelTableOfContentsUri, SiteConfiguration siteConfig);
}