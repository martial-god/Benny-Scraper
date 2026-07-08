namespace BennyScraper.BusinessLogic.Config;

public class NovelScraperSettings
{
    public string UserAgent { get; set; }

    public int HttpTimeout { get; set; }

    public HttpResilienceSettings HttpResilience { get; set; } = new();

    public SeleniumSettings SeleniumSettings { get; set; }

    public FlareSolverrSettings FlareSolverrSettings { get; set; }

    public IList<SiteConfiguration> SiteConfigurations { get; init; }
}
