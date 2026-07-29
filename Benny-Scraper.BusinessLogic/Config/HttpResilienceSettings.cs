namespace BennyScraper.BusinessLogic.Config;

public class HttpResilienceSettings
{
    /// <summary>
    /// Gets or sets the base retry delay used for transient HTTP failures when no server-specific delay is provided.
    /// </summary>
    public int BaseDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Gets or sets the maximum retry attempts for transient HTTP failures.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 6;
}