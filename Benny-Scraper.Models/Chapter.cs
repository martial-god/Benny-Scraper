using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Benny_Scraper.Models
{
    /// <summary>
    /// One to many relationship between Novel and Chapter. Each novel has many chapters, and each chapter belongs to one novel.
    /// </summary>
    public class Chapter
    {
        [Key]
        public Guid Id { get; set; }
        public Guid NovelId { get; set; }
        public Novel Novel { get; set; }
        [StringLength(255)]
        public string? Title { get; set; }
        public string Url { get; set; }
        public string? Content { get; set; }
        public float Number { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastModified { get; set; }
        public virtual ICollection<Page>? Pages { get; set; } // New property for manga pages
    }

    public class ChapterDataBuffer : IDisposable
    {
        public string Url { get; set; }
        public string? Content { get; set; }
        public string Title { get; set; }
        public float Number
        {
            get
            {
                if (string.IsNullOrEmpty(Title))
                    return 0f;
                var digitMatch = Regex.Match(Title, @"[+-]?([0-9]*[.])?[0-9]+");
                return (digitMatch.Success ? float.Parse(digitMatch.Groups[0].Value) : 0f);
            }
        }
        public DateTime DateLastModified { get; set; }
        public ICollection<PageData>? Pages { get; set; }
        public string TempDirectory { get; set; }

        public void Dispose()
        {
            if (Pages != null)
            {
                foreach (var page in Pages)
                {
                    page.ImagePath = null;
                }
            }
        }
    }

    public class PageData
    {
        public string Url { get; set; }
        public string ImagePath { get; set; }
    }

}
