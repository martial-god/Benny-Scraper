using System.ComponentModel.DataAnnotations;

namespace Benny_Scraper.Models
{
    public class Configuration
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool AutoUpdate { get; set; }
        public int ConcurrencyLimit { get; set; } = 2;
        public string? SaveLocation { get; set; }
        public string? NovelSaveLocation { get; set; }
        public string? MangaSaveLocation { get; set; }
        public string? LogLocation { get; set; }
        public string? DatabaseLocation { get; set; }
        public string? DatabaseName { get; set; }
        public string DefaultMangaFileExtension { get; set; } = ".pdf";
        public int DefaultLogLevel { get; set; } = 2;
    }
}
