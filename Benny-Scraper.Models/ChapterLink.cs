namespace BennyScraper.Models;

public sealed record ChapterLink
{
    public string Url { get; init; } = string.Empty;

    public string? Title { get; init => field = value?.Trim(); }

    public PremiumChapterInfo PremiumInfo { get; init; } = new PremiumChapterInfo();

    public int ChapterNumber { get; init; }
}