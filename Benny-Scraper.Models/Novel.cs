using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Benny_Scraper.Models
{
    public class Novel
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; }

        [StringLength(50)]
        public string? Author { get; set; }

        [Required]
        [StringLength(50)]
        public string? SiteName { get; set; }
        public string? Genre { get; set; }
        public string? Description { get; set; }

        [Required]
        [StringLength(144)]
        public string? ChapterName { get; set;}
        public int? ChapterNumber { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateLastModified { get { return DateTime.Now; } }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)] // will need to create a constraint to default the value to 0
        public bool LastChapter { get { return false; } }
        public string Url { get; set; }

        [StringLength(255)]
        public string? SaveLocation { get; set; }
        
    }
}
