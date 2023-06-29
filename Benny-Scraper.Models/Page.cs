using System.ComponentModel.DataAnnotations;

namespace Benny_Scraper.Models
{
    public class Page
    {
        [Key]
        public int Id { get; set; }
        public Guid ChapterId { get; set; }
        public virtual Chapter Chapter { get; set; } // Lazy loaded, on demand
        public string Url { get; set; }
        public byte[]? Image { get; set; }
    }
}
