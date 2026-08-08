using BennyScraper.BusinessLogic;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BennyScraper.Tests;

/// <summary>
/// Shared test plumbing. Two sources of truth:
///   * appsettings.json  -> the FULL set of per-site selector configs.
///   * NovelScraper       -> the sites that actually have a strategy implementation (and their real base URL).
/// Tests feed hand-written (synthetic) HTML through the real extraction code, so no copyrighted page
/// content is stored in the repo. Live-site checks are the CLI's job (--test-site / --validate-config).
/// </summary>
internal static class TestConfig
{
    public static ScraperData ScraperDataFor(string urlPattern)
    {
        var baseUri = ResolveBaseUri(urlPattern);
        return new ScraperData
        {
            SiteConfig = LoadSiteConfig(urlPattern),
            BaseUri = baseUri,
            SiteTableOfContents = baseUri,
        };
    }

    public static HtmlDocument Parse(string html)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);
        return htmlDocument;
    }

    public static SiteConfiguration LoadSiteConfig(string urlPattern) =>
        LoadSettings().SiteConfigurations.First(siteConfiguration => siteConfiguration.UrlPattern == urlPattern);

    /// <summary>All site selector configs from appsettings.json.</summary>
    private static NovelScraperSettings LoadSettings()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        var novelScraperSettings = configuration.GetSection("NovelScraperSettings").Get<NovelScraperSettings>();
        Assert.NotNull(novelScraperSettings);
        return novelScraperSettings;
    }

    /// <summary>
    /// The real base URL for a site, taken from <see cref="NovelScraper"/> — the source of truth for
    /// which sites have an implementation and their exact host (some use www, some don't). Fails loudly
    /// if the site is no longer implemented there (url changed or removed).
    /// </summary>
    private static Uri ResolveBaseUri(string urlPattern)
    {
        var baseUrl = new NovelScraper().GetSupportedSites()
            .FirstOrDefault(supportedSite => supportedSite.Contains(urlPattern, StringComparison.OrdinalIgnoreCase));

        Assert.True(
            baseUrl is not null,
            $"'{urlPattern}' has no implementation in NovelScraper (url changed or site removed).");
        return new Uri(baseUrl!);
    }
}