using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BennyScraper.Models;

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

public class Novel
{
    [Key]
    public Guid Id { get; init; }

    [Column("novel_id")]
    public ICollection<Chapter> Chapters { get; } = new List<Chapter>();

    public ICollection<ChapterRange> ChapterRanges { get; } = new List<ChapterRange>();

    [Required]
    public string Title { get; init; } = string.Empty;

    public string? Author { get; init; }

    [StringLength(50)]
    public string SiteName { get; init; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? Genre { get; set; }

    public string? Description { get; set; }

    public string FirstChapter { get; set; } = string.Empty;

    public string CurrentChapter { get; set; } = string.Empty;

    public string CurrentChapterUrl { get; set; } = string.Empty;

    public int? TotalChapters { get; set; }

    /// <summary>
    /// Gets a value indicating whether this novel has chapter ranges (partial download).
    /// </summary>
    public bool IsPartialDownload => ChapterRanges.Count > 0;

    public DateTime DateCreated { get; init; }

    public DateTime DateLastModified { get; set; }

    // [DatabaseGenerated(DatabaseGeneratedOption.Computed)] // will need to create a constraint to default the value to 0
    public bool LastChapter { get; set; }

    public string? LastTableOfContentsUrl { get; set; }

    public string? Status { get; set; }

    public string? SaveLocation { get; set; }

    public bool SavedFileIsSplit { get; set; }

    public NovelFileType FileType { get; set; }
}