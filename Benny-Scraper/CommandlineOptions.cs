using CommandLine;
using System.ComponentModel.DataAnnotations;

namespace Benny_Scraper
{
    public class CommandLineOptions
    {
        [Option('l', "list", Required = false, HelpText = "List all novels in database. Options include -P, --page [INT] | -I, --items-per-page [INT] | -S, --search [STRING]")]
        public bool List { get; set; }

        [Option('i', "novel-info-by-id", Required = false, HelpText = "Gets the detailed saved information about a novel, including save location")]
        public Guid NovelInformation { get; set; }

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

        [Option('x', "novel-extension-by-id", Required = false, HelpText = "Set Extension/File type of a saved novel by using the [ID]. 0 - EPUB, 1 - PDF, 2 -CBZ.")]
        public Guid NovelExtensionById { get; set; }

        [Option('e', "manga-extension", Required = false, Default = -1, HelpText = "Default extension for mangas (any image based novel) [INT] *count starts a 0*. Default is PDF.")]
        [Range(0, 6, ErrorMessage = "Value for {0} must be between {1} and {2}.")] // set to -1 to have a default value that would be false when checking to avoid invalid options using this
        public int MangaExtension { get; set; }

        [Option("get-extension", Required = false, HelpText = "Gets the saved default extensions for mangas.")]
        public bool ExtensionType { get; set; }

        [Option('f', "single-file", Required = false, HelpText = "Choose how to save Mangas: as a single file containing all chapters (Y), or as individual files for each chapter (N).")]
        public string SingleFile { get; set; }

        [Option('L', "update-novel-saved-location-by-id", Required = false, HelpText = "Updates the saved location of a novel by its [ID]. Useful when a file has been moved, or never added due to previous bug.")]
        public Guid UpdateNovelSavedLocationById { get; set; }

        [Option('P', "page", Default = 1, Required = false, Hidden = true, HelpText = "Page number to display [INT]. Max 100")]
        public int Page { get; set; }

        [Option('I', "items-per-page", Default = 10, Required = false, Hidden = true, HelpText = "Number of items to display per page [INT]. Max 10,000")]
        public int ItemsPerPage { get; set; }

        [Option('S', "search", Required = false, Hidden = true, HelpText = "Search for novel by Title, can seach by partial name [STRING].")]
        public string SearchKeyword { get; set; }

        [Option('U', "update-all", Required = false, HelpText = "Updates all non-completed novels in database. Will only update ones that were not modified the same day")]
        public bool UpdateAll { get; set; }

        [Option('t', "test-site", Required = false, HelpText = "Test connectivity to a site [URL]. Attempts to fetch the page and extract the title to verify Cloudflare bypass is working.")]
        public string TestSite { get; set; }

        [Option("test-all", Required = false, HelpText = "Test connectivity to all supported sites. Useful for verifying Cloudflare bypass is working across all configured sites.")]
        public bool TestAll { get; set; }

        [Option("test-interactive", Required = false, HelpText = "Interactive mode for testing a new site [URL]. Guides you through testing each field and generates a JSON configuration.")]
        public string TestInteractive { get; set; }

        [Option("test-field", Required = false, HelpText = "Test a specific field with XPath [FIELD:XPATH]. TOC fields (test on table-of-contents page): Title, Author, Description, Genres, Status, AlternativeNames, Thumbnail, ChapterLinks. Chapter fields (test on chapter page): ChapterTitle, ChapterContent. TIP: When copying XPath from DevTools, change inner double quotes to single quotes to avoid shell quoting conflicts. Example: --test-field \"Title://*[@id='novel-title']\" <URL>")]
        public string TestField { get; set; }

        [Option("validate-config", Required = false, HelpText = "Validate an existing site configuration by name [STRING]. Tests all selectors against a live URL.")]
        public string ValidateConfig { get; set; }

        [Option("validate-all-configs", Required = false, HelpText = "Validate all active site configurations. Tests selectors for each configured site.")]
        public bool ValidateAllConfigs { get; set; }

        [Option('B', "begin-chapter", Required = false, HelpText = "Beginning chapter number for range selection [INT]. If not specified, starts from chapter 1.")]
        public int? BeginChapter { get; set; }

        [Option('E', "end-chapter", Required = false, HelpText = "Ending chapter number for range selection [INT]. If not specified, downloads to the last chapter.")]
        public int? EndChapter { get; set; }

        [Option("with-login", Required = false, HelpText = "Show browser for manual login to access premium chapters (e.g., WuxiaWorld). Your credentials are NEVER stored - you login manually in the browser window, then scraping continues. Useful for accessing premium/locked chapters you own.")]
        public bool WithLogin { get; set; }

        [Option("sites", Required = false, HelpText = "Display all supported websites for scraping with clickable URLs.")]
        public bool SupportedSites { get; set; }

        [Value(0, MetaName = "url", Required = false, HelpText = "Novel table of contents URL to download. Can be combined with -B and -E options for chapter range selection.")]
        public string Url { get; set; }
    }
}
