using System.ComponentModel.DataAnnotations;

namespace Benny_Scraper.Models
{
    public class Manga
    {
        [Key]
        public int Id { get; set; }
        public ICollection<Chapter> Chapters { get; set; }

        [Required]
        public string Title { get; set; }

        public string? Author { get; set; }

        public string SiteName { get; set; }
        public string Url { get; set; }
        public string? Genre { get; set; }
        public string? Description { get; set; }

        public string FirstChapter { get; set; }
        public string CurrentChapter { get; set; }
        public string CurrentChapterUrl { get; set; }
        public int? TotalChapters { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastModified { get; set; }

        public bool LastChapter { get; set; }
        public string? LastTableOfContentsUrl { get; set; }
        public string? Status { get; set; }

        public string? SaveLocation { get; set; }
    }
}
