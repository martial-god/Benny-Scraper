using Autofac.Features.Indexed;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Interfaces;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using Microsoft.Extensions.Options;
using NLog;

namespace BennyScraper.BusinessLogic;

/// <summary>
/// Resolves the scraping strategy for a given site URL. Individual strategies decide whether
/// they use HttpClient or Selenium internally (e.g. WuxiaWorld uses Selenium for premium login).
/// </summary>
public class NovelScraper(
    IIndex<string, ScraperStrategy> scraperStrategies,
    IOptions<NovelScraperSettings> novelScraperSettings) : INovelScraper
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly NovelScraperSettings _novelScraperSettings = novelScraperSettings.Value;

    /// <summary>
    /// Returns the configured scraper strategy for the given site, or null when its strategy name is not registered.
    /// </summary>
    /// <param name="novelTableOfContentsUri">The table of contents URI whose host is used to look up the matching strategy.</param>
    /// <param name="siteConfig">The site configuration associated with the novel's host.</param>
    /// <returns>The matching <see cref="ScraperStrategy"/>, or null if no strategy is registered for the host.</returns>
    public ScraperStrategy? GetScraperStrategy(Uri novelTableOfContentsUri, SiteConfiguration siteConfig)
    {
        ArgumentNullException.ThrowIfNull(novelTableOfContentsUri);
        ArgumentNullException.ThrowIfNull(siteConfig);

        if (!siteConfig.IsActive)
        {
            _logger.Warn($"The site configuration for {siteConfig.SiteName} is inactive.");
            return null;
        }

        var strategyName = string.IsNullOrWhiteSpace(siteConfig.StrategyName) ? "common" : siteConfig.StrategyName;
        if (scraperStrategies.TryGetValue(strategyName, out var scraperStrategy))
        {
            return scraperStrategy;
        }

        _logger.Error($"No scraper strategy named {strategyName} for site {novelTableOfContentsUri.Host} found.");
        return null;
    }

    /// <summary>
    /// Gets the active sites declared by the files in the sites directory.
    /// </summary>
    /// <returns>The supported site addresses, ordered by site name.</returns>
    public IReadOnlyList<string> GetSupportedSites() => _novelScraperSettings.SiteConfigurations
        .Where(siteConfiguration => siteConfiguration.IsActive)
        .OrderBy(siteConfiguration => siteConfiguration.SiteName, StringComparer.OrdinalIgnoreCase)
        .Select(siteConfiguration => $"https://{siteConfiguration.UrlPattern}")
        .ToList()
        .AsReadOnly();
}