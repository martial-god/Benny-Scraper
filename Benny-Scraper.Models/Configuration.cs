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
        public string? DatabaseFileName { get; set; }
        public bool SaveAsSingleFile { get; set; }
        public FileExtension DefaultMangaFileExtension { get; set; } = FileExtension.PDF;
        public LogLevel DefaultLogLevel { get; set; } = LogLevel.Info; // 0 = Debug, 1 = Info, 2 = Warning, 3 = Error, 4 = Fatal
    }

    public enum FileExtension
    {
        PDF,
        CBZ,
        CBR
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }
}
