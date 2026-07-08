using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.Interfaces;
using Microsoft.Extensions.Options;
using NLog;

namespace BennyScraper.BusinessLogic.Factory;

public class NovelScraperFactory : INovelScraperFactory
{
    private readonly Func<INovelScraper> _novelScraperResolver;
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private readonly NovelScraperSettings _novelScraperSettings;

    public NovelScraperFactory(Func<INovelScraper> novelScraperResolver, IOptions<NovelScraperSettings> novelScraperSettings)
    {
        _novelScraperResolver = novelScraperResolver;
        _novelScraperSettings = novelScraperSettings.Value;
    }

    public INovelScraper CreateScraper(Uri novelTableOfContentsUri, SiteConfiguration siteConfig)
    {
        if (siteConfig.CloudflareProtection == CloudflareProtectionLevel.Detected)
        {
            Logger.Info($"Site {siteConfig.Name} has Cloudflare protection detected. Using HttpClient with enhanced headers.");
        }

        try
        {
            return _novelScraperResolver();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error when getting NovelScraper for {novelTableOfContentsUri.Host}. {ex}");
            throw;
        }
    }
}