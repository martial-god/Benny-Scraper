using System.ComponentModel.DataAnnotations;

namespace Benny_Scraper.Models
{
    public class Manga
    {
        [Key]
        public int Id { get; set; }
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
}
