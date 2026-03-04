namespace Benny_Scraper.BusinessLogic.Config
{
    public class NovelScraperSettings
    {
        public string UserAgent { get; set; }
        public int HttpTimeout { get; set; }
        public SeleniumSettings SeleniumSettings { get; set; }
        public FlareSolverrSettings FlareSolverrSettings { get; set; }
        public List<SiteConfiguration> SiteConfigurations { get; init; }
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
}
