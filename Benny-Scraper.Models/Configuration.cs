using System.ComponentModel.DataAnnotations;

namespace Benny_Scraper.Models
{
    public class Configuration
    {
        [Key]
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool AutoUpdate { get; set; }
        public int ConcurrencyLimit { get; set; }
        public string? SaveLocation { get; set; }
        public string? NovelSaveLocation { get; set; }
        public string? MangaSaveLocation { get; set; }
        public string? LogLocation { get; set; }
        public string? DatabaseLocation { get; set; }
        public string? DatabaseFileName { get; set; }
        public bool SaveAsSingleFile { get; set; }
        public string FontType { get; set; }
        public int FontSize { get; set; }
        public FileExtension DefaultMangaFileExtension { get; set; }
        public LogLevel DefaultLogLevel { get; set; } // 0 = Debug, 1 = Info, 2 = Warning, 3 = Error, 4 = Fatal
        public string DetermineSaveLocation(bool isManga = false)
        {
            if (!string.IsNullOrEmpty(NovelSaveLocation) && !isManga)
                return NovelSaveLocation;
            if (!string.IsNullOrEmpty(MangaSaveLocation) && isManga)
                return MangaSaveLocation;
            return SaveLocation ?? string.Empty;
        }
    }

    public enum FileExtension
    {
        Pdf,
        Cbz,
        Cbr,
        Cb7,
        Cbt,
        Cba
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
