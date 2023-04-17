using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Benny_Scraper.Models
{
    /// <summary>
    /// One to One relationship between NovelList and Novel. Each novel list has one novel, and each novel belongs to one novel list.
    /// </summary>
    public class NovelList
    {
        public Guid Id { get; set; }
        [ForeignKey(nameof(NovelList.Id))]
        public Guid NovelId { get; set; }
        public Novel Novel { get; set; }

        [StringLength(255)]
        public string Title { get; set; }
        public string? Description { get; set; }
        public int TotalChapters;
        public bool Completed { get; set; } // may need to create enumarable for Completed, Haitus,
        public DateTime DateCreated { get; set; }
        public DateTime DateLastModified { get { return DateTime.Now; } }

    }
}