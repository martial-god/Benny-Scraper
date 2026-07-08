namespace BennyScraper.BusinessLogic.Config;

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
    /// <summary>
    /// Gets or sets the human-readable site name. Used for logs and display.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the substring used to match <see cref="Uri.Host"/> (e.g. "novelfull.com").
    /// </summary>
    public string UrlPattern { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this site config is enabled without deleting it.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the table of contents spans multiple pages.
    /// </summary>
    public bool HasPagination { get; set; }

    /// <summary>
    /// Gets or sets the pagination format string appended to the TOC URL (e.g. "?page={0}").
    /// Null for non-paginated sites.
    /// </summary>
    public string? PaginationType { get; set; }

    /// <summary>
    /// Gets or sets the small substring used to detect/parse pagination query (e.g. "?page=").
    /// Null for non-paginated sites.
    /// </summary>
    public string? PaginationQueryPartial { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the novel metadata (title/description/etc.) is on a different page than the TOC.
    /// </summary>
    public bool HasNovelInfoOnDifferentPage { get; set; }

    /// <summary>
    /// Gets or sets the approximate number of chapters per TOC page for paginated sites.
    /// Use -1/0 for sites where this value is not applicable.
    /// </summary>
    public int ChaptersPerPage { get; set; }

    /// <summary>
    /// Gets or sets the page index offset (0- or 1-based) for pagination.
    /// </summary>
    public int PageOffSet { get; set; }

    /// <summary>
    /// Gets or sets the substring that indicates a completed novel when found in the status text.
    /// </summary>
    public string? CompletedStatus { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether chapter "content" is a list of images/pages (manga/comics style).
    /// When true, ChapterContent selects image nodes and the scraper downloads them.
    /// </summary>
    public bool HasImagesForChapterContent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether page requires premium/paid access for some chapters.
    /// </summary>
    public bool HasPremiumChapters { get; set; }

    /// <summary>
    /// Gets a value indicating whether the entire site must be scraped using Selenium (JS-rendered).
    /// </summary>
    public bool EntireSiteRequiresSelenium { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the table of contents requires Selenium to load chapter links.
    /// </summary>
    public bool TableOfContentsRequiresSelenium { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether chapter pages require Selenium to load chapter content.
    /// This is separate from <see cref="HasImagesForChapterContent"/>.
    /// </summary>
    public bool ChapterContentRequiresSelenium { get; set; }

    /// <summary>
    /// Gets or sets the Cloudflare protection level expected for the site.
    /// </summary>
    public CloudflareProtectionLevel? CloudflareProtection { get; set; }

    /// <summary>
    /// Gets or sets how chapter links should be ordered after extraction.
    /// </summary>
    public ChapterSortOrder ChapterSortOrder { get; set; } = ChapterSortOrder.Ascending;

    /// <summary>
    /// Gets the XPath selectors and attribute names specific to this site.
    /// </summary>
    public Selectors Selectors { get; init; }

    public PremiumInfo? PremiumInfo { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of matching nodes considered "enough" for ChapterContent.
    /// If fewer nodes are found, the scraper may try AlternativeChapterContent.
    ///
    /// Null means use the application default.
    /// </summary>
    public int? MinimumChapterParagraphThreshold { get; set; }
}
