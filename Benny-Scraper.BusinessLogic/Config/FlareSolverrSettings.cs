namespace BennyScraper.BusinessLogic.Config;

/// <summary>
/// Configuration for FlareSolverr Cloudflare bypass service.
/// </summary>
internal sealed class FlareSolverrSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable FlareSolverr for Cloudflare bypass.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the FlareSolverr URL (default: http://localhost:8191).
    /// </summary>
    public string Url { get; set; } = "http://localhost:8191";

    /// <summary>
    /// Gets or sets the maximum timeout in milliseconds for FlareSolverr to solve a challenge.
    /// </summary>
    public int MaxTimeout { get; set; } = 60000;
}