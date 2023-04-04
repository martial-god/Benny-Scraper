using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Benny_Scraper.BusinessLogic.Config;
using NLog;

namespace Benny_Scraper.BusinessLogic.Factory
{
    public class NovelScraperFactory : INovelScraperFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly NovelScraperSettings _novelScraperSettings; // IOptions will get an instnace of NovelScraperSettings

        public NovelScraperFactory(IServiceProvider serviceProvider, IOptions<NovelScraperSettings> novelScraperSettings)
        {
            _serviceProvider = serviceProvider;
            _novelScraperSettings = novelScraperSettings.Value;
        }

        public INovelScraper CreateSeleniumOrHttpScraper(Uri novelTableOfContentsUri)
        {
            bool isSeleniumUrl = _novelScraperSettings.SeleniumSites.Any(x => novelTableOfContentsUri.Host.Contains(x));
            
            if (isSeleniumUrl)
            {
                return _serviceProvider.GetRequiredService<SeleniumNovelScraper>();
            }

            try
            {
               return _serviceProvider.GetRequiredService<HttpNovelScraper>();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error when getting Scraper");
                throw;
            }
        }
    }
}
