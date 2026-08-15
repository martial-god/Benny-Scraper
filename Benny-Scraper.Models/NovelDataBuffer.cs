using System.Collections.ObjectModel;

namespace BennyScraper.Models;

/// <summary>
/// Class for storing pertinent data about a novel, usually things found on table of centents page like title description, genres, etc.
/// </summary>
public sealed class NovelDataBuffer : IDisposable
{
    public NovelDataBuffer()
    {
        NovelUrl = string.Empty;
    }

    public string Title { get; set => field = value?.Trim() ?? string.Empty; } = string.Empty;

    public IList<ChapterLink> ChapterLinks { get; } = new List<ChapterLink>();

    public IList<UserPremiumCurrency> UserPremiumCurrencies { get; } = new List<UserPremiumCurrency>();

    public IList<string> ChapterTitles { get; } = new List<string>();

    public string NovelStatus { get; set => field = value?.Trim() ?? string.Empty; } = string.Empty;

    public string LastTableOfContentsPageUrl { get; set; } = string.Empty;

    public bool IsNovelCompleted { get; set; }

    public string ThumbnailUrl { get; set; } = string.Empty;

    public double Rating { get; set; }

    public int TotalRatings { get; set; }

    public IList<string> Description { get; } = new List<string>();

    public string Author { get; set => field = value?.Trim() ?? string.Empty; } = string.Empty;

    public IList<string> Genres { get; } = new List<string>();

    public IList<string> AlternativeNames { get; } = new List<string>();

    public string MostRecentChapterTitle { get; set => field = value?.Trim() ?? string.Empty; } = string.Empty;

    public string CurrentChapterUrl { get; set; } = string.Empty;

    public string FirstChapter { get; set => field = value?.Trim() ?? string.Empty; } = string.Empty;

    public ReadOnlyCollection<byte>? ThumbnailImage { get; private set; }

    public string NovelUrl { get; set; }

    public bool IsLoggedIn { get; set; }

    public void SetThumbnailImage(byte[]? bytes) => ThumbnailImage = bytes is null ? null : new ReadOnlyCollection<byte>(bytes);

    public void Dispose()
    {
        ChapterLinks.Clear();
        ChapterTitles.Clear();
        Description.Clear();
        Genres.Clear();
        AlternativeNames.Clear();
        UserPremiumCurrencies.Clear();
        ThumbnailImage = null;
        GC.SuppressFinalize(this);
    }
}