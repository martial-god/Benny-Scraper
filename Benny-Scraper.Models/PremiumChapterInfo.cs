namespace BennyScraper.Models;

public sealed class PremiumChapterInfo
{
    public bool IsPremium { get; init; }

    public int Cost { get; init; }

    public string? CurrencyName { get; init; }
}