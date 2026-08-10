using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.Interfaces;
using Microsoft.Extensions.Options;
using NLog;

namespace BennyScraper.BusinessLogic.Factory;

public class NovelScraperFactory : INovelScraperFactory
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly Func<INovelScraper> _novelScraperResolver;
    private readonly NovelScraperSettings _novelScraperSettings;

    public NovelScraperFactory(Func<INovelScraper> novelScraperResolver, IOptions<NovelScraperSettings> novelScraperSettings)
    {
        ArgumentNullException.ThrowIfNull(novelScraperSettings);

        _novelScraperResolver = novelScraperResolver;
        _novelScraperSettings = novelScraperSettings.Value;
    }

    public INovelScraper CreateScraper(Uri novelTableOfContentsUri, SiteConfiguration siteConfig)
    {
        ArgumentNullException.ThrowIfNull(novelTableOfContentsUri);
        ArgumentNullException.ThrowIfNull(siteConfig);

        if (siteConfig.CloudflareProtection == CloudflareProtectionLevel.Detected)
        {
            _logger.Info($"Site {siteConfig.SiteName} has Cloudflare protection detected. Using HttpClient with enhanced headers.");
        }

        try
        {
            return _novelScraperResolver();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error when getting NovelScraper for {novelTableOfContentsUri.Host}. {ex}");
            throw;
        }
    }
}