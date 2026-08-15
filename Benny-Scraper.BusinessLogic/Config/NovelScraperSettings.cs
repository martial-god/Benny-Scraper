namespace BennyScraper.BusinessLogic.Config;

internal sealed class NovelScraperSettings
{
    public string UserAgent { get; set; } = string.Empty;

    public int HttpTimeout { get; set; }

    public HttpResilienceSettings HttpResilience { get; set; } = new();

    public SeleniumSettings SeleniumSettings { get; set; } = new();

    public FlareSolverrSettings FlareSolverrSettings { get; set; } = new();

    public IList<SiteConfiguration> SiteConfigurations { get; init; } = new List<SiteConfiguration>();
}