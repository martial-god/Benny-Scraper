using System.ComponentModel.DataAnnotations;
using CommandLine;

namespace Benny_Scraper
{
    public class CommandLineOptions
    {
        [Option('l', "list", Required = false, HelpText = "List all novels in database.")]
        public bool List { get; set; }

        [Option("clear-database", Required = false, HelpText = "Clear all novels and chapters from database.")]
        public bool ClearDatabase { get; set; }
        
        [Option('d', "delete-novel-by-id", Required = false, HelpText = "Deletes a novel by its ID")]
        public Guid DeleteNovelById { get; set; }

        [Option('r', "recreate-epub-by-id", Required = false, HelpText = "Recreates Epub novel using the [ID].")]
        public Guid RecreateEpubById { get; set; }

        [Option('c', "concurrent-request", Required = false, HelpText = "Set the number [INT] of concurrent requests to a website. Default is 2, value will be limited to number of CPU cores on your computer. *Some websites may block your ip if too many requests are made in a short time*")]
        public int ConcurrentRequests { get; set; }

        [Option('s', "save-location", Required = false, HelpText = "Set default save location [PATH]. Overridden by specific 'manga' or 'novel' locations if set.")]
        public string SaveLocation { get; set; }

        [Option('m', "manga-save-location", Required = false, HelpText = "Set manga-specific save location [PATH]. Overrides 'save-location'.")]
        public string MangaSaveLocation { get; set; }

        [Option('n', "novel-save-location", Required = false, HelpText = "Set novel-specific save location [PATH]. Overrides 'save-location'.")]
        public string NovelSaveLocation { get; set; }

        [Option('e', "manga-extension", Required = false, Default = -1, HelpText = "Default extension for mangas (any image based novel) [INT] *count starts a 0*. Default is PDF.")]
        [Range(0, 6, ErrorMessage = "Value for {0} must be between {1} and {2}.")] // set to -1 to have a default value that would be false when checking to avoid invalid options using this
        public int MangaExtension { get; set; }

        [Option("get-extension", Required = false, HelpText = "Gets the saved default extensions for mangas.")]
        public bool ExtensionType { get; set; }

        [Option('f', "single-file", Required = false, HelpText = "Choose how to save Mangas: as a single file containing all chapters (Y), or as individual files for each chapter (N).")]
        public  string SingleFile { get; set; }

        [Option('L', "update-novel-saved-location-by-id", Required = false, HelpText = "Updates the saved location of a novel by its [ID]. Useful when a file has been moved, or never added due to previous bug.")]
        public Guid UpdateNovelSavedLocationById { get; set; }
    }
}
