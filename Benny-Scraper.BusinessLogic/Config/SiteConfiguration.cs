namespace Benny_Scraper.BusinessLogic.Config
{
    public enum CloudflareProtectionLevel
    {
        None,
        Detected,
        JsChallenge
    }

    public enum ChapterSortOrder
    {
        Ascending,
        Descending,
        None
    }

    public class SiteConfiguration
    {
        public string Name { get; set; }
        public string UrlPattern { get; set; }
        public bool IsActive { get; set; } = true;
        public bool HasPagination { get; set; }
        public string? PaginationType { get; set; }
        public string? PaginationQueryPartial { get; set; }
        public bool HasNovelInfoOnDifferentPage { get; set; }
        public int ChaptersPerPage { get; set; }
        public int PageOffSet { get; set; }
        public string? CompletedStatus { get; set; }
        public bool HasImagesForChapterContent { get; set; }
        public bool EntireSiteRequiresSelenium { get; set; }
        public bool TableOfContentsRequiresSelenium { get; set; }
        public bool ChapterContentRequiresSelenium { get; set; }
        public CloudflareProtectionLevel? CloudflareProtection { get; set; }
        public ChapterSortOrder ChapterSortOrder { get; set; } = ChapterSortOrder.Ascending;
        public Selectors Selectors { get; set; }
    }
}
