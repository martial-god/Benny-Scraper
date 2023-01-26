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
        
        [StringLength(144)] public string FirstChapter { get; set;}
        [StringLength(144)] public string CurrentChapter { get; set; }
        public int? TotalChapters { get; set; }        
        public DateTime DateCreated { get; set; }
        public DateTime DateLastModified { get { return DateTime.UtcNow; } }

        //[DatabaseGenerated(DatabaseGeneratedOption.Computed)] // will need to create a constraint to default the value to 0
        public bool LastChapter { get; set; }
        [StringLength(50)] public string? Status { get; set; }
        

        [StringLength(255)]
        public string? SaveLocation { get; set; }
        
    }
}
