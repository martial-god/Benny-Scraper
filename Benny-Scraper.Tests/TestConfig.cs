using BennyScraper.BusinessLogic;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BennyScraper.Tests;

/// <summary>
/// Shared test plumbing. Site configurations are loaded from the same sites directory used by the application.
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

    /// <summary>All site selector configurations from the sites directory.</summary>
    private static NovelScraperSettings LoadSettings()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();

        var novelScraperSettings = configuration.GetSection("NovelScraperSettings").Get<NovelScraperSettings>();
        Assert.NotNull(novelScraperSettings);

        novelScraperSettings.SiteConfigurations.Clear();
        var siteConfigurationsDirectory = Path.Combine(AppContext.BaseDirectory, "sites");
        foreach (var siteConfiguration in SiteConfigurationLoader.LoadFromDirectory(siteConfigurationsDirectory))
        {
            novelScraperSettings.SiteConfigurations.Add(siteConfiguration);
        }

        return novelScraperSettings;
    }

    /// <summary>
    /// Builds the site's base URI from the URL pattern stored in its site configuration.
    /// </summary>
    private static Uri ResolveBaseUri(string urlPattern)
    {
        var siteConfiguration = LoadSiteConfig(urlPattern);
        return new Uri($"https://{siteConfiguration.UrlPattern}");
    }
}