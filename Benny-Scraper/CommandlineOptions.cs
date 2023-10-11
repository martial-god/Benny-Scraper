using System.ComponentModel.DataAnnotations;
using CommandLine;

namespace Benny_Scraper
{
    public class CommandLineOptions
    {
        [Option('l', "list", Required = false, HelpText = "List all novels in database.")]
        public bool List { get; set; }

        [Option('c', "clear-database", Required = false, HelpText = "Clear all novels and chapters from database.")]
        public bool ClearDatabase { get; set; }
        
        [Option('d', "delete-novel-by-id", Required = false, HelpText = "Deletes a novel by its ID")]
        public int DeleteNovelById { get; set; }

        [Option('r', "recreate-epub", Required = false, HelpText = "Recreates Epub novel using the [URL].")]
        public string RecreateEpub { get; set; }

        [Option('c', "concurrent-request", Required = false, HelpText = "Set the number [INT] of concurrent requests to a website. Default is 2, value will be limited to number of CPU cores on your computer. *Some websites may block your ip if too many requests are made in a short time*")]
        public int ConcurrentRequest { get; set; }

        [Option('s', "save-location", Required = false, HelpText = "Default save location [PATH]. Overridden by specific 'manga' or 'novel' locations if set.")]
        public string SaveLocation { get; set; }

        [Option('m', "manga-save-location", Required = false, HelpText = "Manga-specific save location [PATH]. Overrides 'save-location'.")]
        public string MangaSaveLocation { get; set; }

        [Option('n', "novel-save-location", Required = false, HelpText = "Novel-specific save location [PATH]. Overrides 'save-location'.")]
        public string NovelSaveLocation { get; set; }

        [Option('e', "manga-extension", Required = false, HelpText = "Default extension for mangas (any image based novel) [INT]. Default is PDF.")]
        [Range(0, 6, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
        public int MangaExtension { get; set; }

    }
}
