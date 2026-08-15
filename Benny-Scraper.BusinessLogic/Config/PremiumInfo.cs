namespace BennyScraper.BusinessLogic.Config;

internal sealed class PremiumInfo
{
    /// <summary>
    /// Gets or sets the primary currency name displayed on table of contents for premium chapters.
    /// Default: "Credits".
    /// </summary>
    public string CurrencyName { get; set; } = "Credits";
}