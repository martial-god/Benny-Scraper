using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Benny_Scraper.Models
{
    /// <summary>
    /// One to many relationship between Novel and Chapter. Each novel has many chapters, and each chapter belongs to one novel.
    /// </summary>
    public class Chapter
    {
        public Guid Id { get; set; }
        public Guid NovelId { get; set; }
        public Novel Novel { get; set; }
        [StringLength(255)]
        public string? Title { get; set; }
        public string Url { get; set; }
        public string Content { get; set; }
        public string Number { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastModified { get; set; }
    }

    public class ChapterData
    {
        public string Url { get; set; }
        public string Content { get; set; }
        public string Title { get; set; }
        public string Number
        {
            get
            {
                var digitMatch = Regex.Match(Title, @"\d+");
                return (digitMatch.Success ? digitMatch.Groups[0].Value : "0");
            }
        }
        public DateTime DateLastModified { get; set; }
    }
}
