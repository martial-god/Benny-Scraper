using System.ComponentModel.DataAnnotations;

namespace Benny_Scraper.Models
{
    public class Manga
    {
        [Key]
        public Guid Id { get; set; }
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

        public bool LastChapter { get; set; }
        public string? LastTableOfContentsUrl { get; set; }
        [StringLength(50)] public string? Status { get; set; }

        [StringLength(255)]
        public string? SaveLocation { get; set; }
    }

    public class MangaData
    {
        public MangaData()
        {
            ChapterUrls = new List<string>();
            Description = new List<string>();
            Genres = new List<string>();
            AlternativeNames = new List<string>();
        }

        public string Title { get; set; }
        public List<string> ChapterUrls { get; set; }
        public string MangaStatus { get; set; }
        public string LastTableOfContentsPageUrl { get; set; }
        public bool IsMangaCompleted { get; set; }
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
    }

}
