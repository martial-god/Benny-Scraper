namespace BennyScraper.BusinessLogic.Config;

public class NovelScraperSettings
{
    public string UserAgent { get; set; }

    public int HttpTimeout { get; set; }

    public HttpResilienceSettings HttpResilience { get; set; } = new();

    public SeleniumSettings SeleniumSettings { get; set; }

    public FlareSolverrSettings FlareSolverrSettings { get; set; }

    public List<SiteConfiguration> SiteConfigurations { get; init; }
}

public class HttpResilienceSettings
{
    /// <summary>
    /// Base retry delay used for transient HTTP failures when no server-specific delay is provided.
    /// </summary>
    public int BaseDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Maximum retry attempts for transient HTTP failures.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 6;
}

/// <summary>
/// Configuration for FlareSolverr Cloudflare bypass service.
/// </summary>
public class FlareSolverrSettings
{
    /// <summary>
    /// Whether to enable FlareSolverr for Cloudflare bypass.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// FlareSolverr URL (default: http://localhost:8191).
    /// </summary>
    public string Url { get; set; } = "http://localhost:8191";

    /// <summary>
    /// Maximum timeout in milliseconds for FlareSolverr to solve a challenge.
    /// </summary>
    public int MaxTimeout { get; set; } = 60000;
}