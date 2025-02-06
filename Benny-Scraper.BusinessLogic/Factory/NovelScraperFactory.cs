using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.Interfaces;
using NLog;

namespace Benny_Scraper.BusinessLogic.Factory
{
    public class NovelScraperFactory(Func<INovelScraper> novelScraperResolver) : INovelScraperFactory
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Creates an instance of either a SeleniumNovelScraper or NovelScraper depending on the url.
        /// </summary>
        /// <param name="novelTableOfContentsUri"></param>
        /// <returns>Scraper instance that implements INovelService </returns>
        public INovelScraper CreateScraper(Uri novelTableOfContentsUri)
        {
            try
            {
                return novelScraperResolver();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error when getting {nameof(NovelScraper)} for {novelTableOfContentsUri.Host}. {ex}");
                throw;
            }
        }

    }

}
