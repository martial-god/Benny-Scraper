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
        [StringLength(255)]
        public string Title { get; set; }

        [StringLength(50)] public string? Author { get; set; }

        [StringLength(50)] public string SiteName { get; set; }
        public string Url { get; set; }
        public string? Genre { get; set; }
        public string? Description { get; set; }

        [StringLength(255)] public string FirstChapter { get; set; }
        [StringLength(255)] public string CurrentChapter { get; set; }
        public string CurrentChapterUrl { get; set; }
        public int? TotalChapters { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastModified { get; set; }

        //[DatabaseGenerated(DatabaseGeneratedOption.Computed)] // will need to create a constraint to default the value to 0
        public bool LastChapter { get; set; }
        public string? LastTableOfContentsUrl { get; set; }
        [StringLength(50)] public string? Status { get; set; }

        [StringLength(255)]
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
            ChapterUrls = new List<string>();
            Description = new List<string>();
            Genres = new List<string>();
            AlternativeNames = new List<string>();
        }

        public string Title { get; set; }
        public List<string> ChapterUrls { get; set; }
        public string NovelStatus { get; set; }
        public string LastTableOfContentsPageUrl { get; set; }
        public bool IsNovelCompleted { get; set; }
        public string ThumbnailUrl { get; set; }
        public double Rating { get; set; }
        public int TotalRatings { get; set; }
        public List<string>? Description { get; set; }
        public string Author { get; set; }
        public List<string> Genres { get; set; }
        public List<string> AlternativeNames { get; set; }
        public string MostRecentChapterTitle { get; set; }
        public string CurrentChapterUrl { get; set; }
        public string FirstChapter { get; set; }
        public byte[]? ThumbnailImage { get; set; }

        public void Dispose()
        {
            ChapterUrls.Clear();
            Description?.Clear();
            Genres.Clear();
            AlternativeNames.Clear();
            ThumbnailImage = null;
        }
    }
}
