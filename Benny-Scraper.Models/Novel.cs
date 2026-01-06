using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Benny_Scraper.Models
{
    public class Novel
    {
        [Key]
        public Guid Id { get; set; }
        [Column("novel_id")]
        public ICollection<Chapter> Chapters { get; set; }

        [Required]
        public string Title { get; set; }

        public string? Author { get; set; }

        [StringLength(50)] public string SiteName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Genre { get; set; }
        public string? Description { get; set; }

        public string FirstChapter { get; set; } = string.Empty;
        public string CurrentChapter { get; set; } = string.Empty;
        public string CurrentChapterUrl { get; set; } = string.Empty;
        public int? TotalChapters { get; set; }
        public int? ChapterRangeBegin { get; set; }
        public int? ChapterRangeEnd { get; set; }
        public bool IsPartialDownload { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastModified { get; set; }

        //[DatabaseGenerated(DatabaseGeneratedOption.Computed)] // will need to create a constraint to default the value to 0
        public bool LastChapter { get; set; }
        public string? LastTableOfContentsUrl { get; set; }
        public string? Status { get; set; }

        public string? SaveLocation { get; set; }
        public bool SavedFileIsSplit { get; set; }
        public NovelFileType FileType { get; set; }
    }

    public enum NovelFileType
    {
        Epub,
        Pdf,
        Cbz,
        Cbr,
        Cb7,
        Cbt,
        Cba
    }

    /// <summary>
    /// Class for storing pertinent data about a novel, usually things found on table of centents page like title description, genres, etc.
    /// </summary>
    public class NovelDataBuffer : IDisposable
    {
        public NovelDataBuffer()
        {
            ChapterLinks = new List<ChapterLink>();
            ChapterTitles = new List<string>();
            Description = new List<string>();
            Genres = new List<string>();
            AlternativeNames = new List<string>();
            NovelUrl = string.Empty;
        }

        public string Title
        {
            get;
            set => field = value?.Trim() ?? string.Empty;
        }

        public List<ChapterLink> ChapterLinks { get; set; }
        public List<UserPremiumCurrency>? UserPremiumCurrencies { get; set; }
        public List<string> ChapterTitles { get; set; }
        public string NovelStatus { get; set => field = value?.Trim() ?? string.Empty; }
        public string LastTableOfContentsPageUrl { get; set; }
        public bool IsNovelCompleted { get; set; }
        public string ThumbnailUrl { get; set; }
        public double Rating { get; set; }
        public int TotalRatings { get; set; }
        public List<string>? Description { get; set; }
        public string Author { get; set => field = value?.Trim() ?? string.Empty; }
        public List<string> Genres { get; set; }
        public List<string> AlternativeNames { get; set; }
        public string MostRecentChapterTitle { get; set => field = value?.Trim() ?? string.Empty; }
        public string CurrentChapterUrl { get; set; }
        public string FirstChapter { get; set => field = value?.Trim() ?? string.Empty; }
        public byte[]? ThumbnailImage { get; set; }
        public string NovelUrl { get; set; }

        public void Dispose()
        {
            ChapterLinks.Clear();
            ChapterTitles.Clear();
            Description?.Clear();
            Genres.Clear();
            AlternativeNames.Clear();
            ThumbnailImage = null;
        }
    }

    public sealed class ChapterLink
    {
        public string Url { get; init; } = string.Empty;
        public string? Title { get; init => field = value?.Trim(); }
        public PremiumChapterInfo PremiumInfo { get; init; } = new PremiumChapterInfo();
    }

    public sealed class PremiumChapterInfo
    {
        public bool IsPremium { get; init; }
        public int Cost { get; init; }
        public string? CurrencyName { get; init; }
    }

    public sealed class UserPremiumCurrency
    {
        public int Balance { get; init; }
        public string CurrencyName { get; init; } = string.Empty;
    }
}
