using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Interfaces;

namespace Benny_Scraper.BusinessLogic.Factory.Interfaces
{
    public interface INovelScraperFactory
    {
        INovelScraper CreateScraper(Uri novelTableOfContentsUri, SiteConfiguration siteConfig);
    }
}
