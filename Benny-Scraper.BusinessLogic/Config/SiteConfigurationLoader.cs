using System.Text.Json;
using System.Text.Json.Serialization;

namespace BennyScraper.BusinessLogic.Config;

/// <summary>
/// Loads the individual site configuration files shipped in the application's sites directory.
/// </summary>
internal static class SiteConfigurationLoader
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    /// <summary>
    /// Loads and validates every JSON file directly inside the supplied directory.
    /// </summary>
    /// <param name="siteConfigurationsDirectory">Directory containing one <see cref="SiteConfiguration"/> per JSON file.</param>
    /// <returns>The configurations, ordered by site name.</returns>
    public static IReadOnlyList<SiteConfiguration> LoadFromDirectory(string siteConfigurationsDirectory)
    {
        if (!Directory.Exists(siteConfigurationsDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The site configuration directory was not found: {siteConfigurationsDirectory}");
        }

        var siteConfigurationFiles = Directory
            .EnumerateFiles(siteConfigurationsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (siteConfigurationFiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"No site configuration files were found in: {siteConfigurationsDirectory}");
        }

        var siteConfigurations = new List<SiteConfiguration>(siteConfigurationFiles.Count);
        foreach (var siteConfigurationFile in siteConfigurationFiles)
        {
            SiteConfiguration siteConfiguration;
            try
            {
                siteConfiguration = JsonSerializer.Deserialize<SiteConfiguration>(
                    File.ReadAllText(siteConfigurationFile),
                    _jsonSerializerOptions) ?? throw new JsonException("The file did not contain a site configuration.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    $"The site configuration file is invalid: {siteConfigurationFile}",
                    exception);
            }

            ValidateSiteConfiguration(siteConfiguration, siteConfigurationFile);
            siteConfigurations.Add(siteConfiguration);
        }

        var duplicateUrlPattern = siteConfigurations
            .GroupBy(siteConfiguration => siteConfiguration.UrlPattern, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateUrlPattern != null)
        {
            throw new InvalidOperationException(
                $"More than one site configuration uses the URL pattern '{duplicateUrlPattern.Key}'.");
        }

        var duplicateSiteName = siteConfigurations
            .GroupBy(siteConfiguration => siteConfiguration.SiteName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateSiteName != null)
        {
            throw new InvalidOperationException(
                $"More than one site configuration uses the site name '{duplicateSiteName.Key}'.");
        }

        return siteConfigurations
            .OrderBy(siteConfiguration => siteConfiguration.SiteName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    private static void ValidateSiteConfiguration(
        SiteConfiguration siteConfiguration,
        string siteConfigurationFile)
    {
        if (string.IsNullOrWhiteSpace(siteConfiguration.SiteName))
        {
            throw new InvalidOperationException(
                $"The siteName property is required in: {siteConfigurationFile}");
        }

        if (string.IsNullOrWhiteSpace(siteConfiguration.UrlPattern))
        {
            throw new InvalidOperationException(
                $"The urlPattern property is required in: {siteConfigurationFile}");
        }

        if (string.IsNullOrWhiteSpace(siteConfiguration.StrategyName))
        {
            throw new InvalidOperationException(
                $"The strategyName property is required in: {siteConfigurationFile}");
        }

        if (!siteConfiguration.IsActive ||
            !string.Equals(siteConfiguration.StrategyName, "common", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var tableOfContentsSelectors = siteConfiguration.Selectors.TableOfContents;
        if (string.IsNullOrWhiteSpace(tableOfContentsSelectors.NovelTitle))
        {
            throw new InvalidOperationException(
                $"An active common strategy requires selectors.tableOfContents.novelTitle in: {siteConfigurationFile}");
        }

        if (string.IsNullOrWhiteSpace(tableOfContentsSelectors.ChapterLinks))
        {
            throw new InvalidOperationException(
                $"An active common strategy requires selectors.tableOfContents.chapterLinks in: {siteConfigurationFile}");
        }

        if (string.IsNullOrWhiteSpace(siteConfiguration.Selectors.ChapterContent))
        {
            throw new InvalidOperationException(
                $"An active common strategy requires selectors.chapterContent in: {siteConfigurationFile}");
        }
    }
}