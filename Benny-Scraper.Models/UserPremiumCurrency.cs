namespace BennyScraper.Models;

public sealed class UserPremiumCurrency
{
    public int Balance { get; init; }

    public string CurrencyName { get; init; } = string.Empty;
}