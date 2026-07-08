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
    /// Human-readable site name. Used for logs and display.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Substring used to match <see cref="Uri.Host"/> (e.g. "novelfull.com").
    /// </summary>
    public string UrlPattern { get; set; }

    /// <summary>
    /// Enables/disables this site config without deleting it.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether the table of contents spans multiple pages.
    /// </summary>
    public bool HasPagination { get; set; }

    /// <summary>
    /// Pagination format string appended to the TOC URL (e.g. "?page={0}").
    /// Null for non-paginated sites.
    /// </summary>
    public string? PaginationType { get; set; }

    /// <summary>
    /// Small substring used to detect/parse pagination query (e.g. "?page=").
    /// Null for non-paginated sites.
    /// </summary>
    public string? PaginationQueryPartial { get; set; }

    /// <summary>
    /// True if the novel metadata (title/description/etc.) is on a different page than the TOC.
    /// </summary>
    public bool HasNovelInfoOnDifferentPage { get; set; }

    /// <summary>
    /// Approximate number of chapters per TOC page for paginated sites.
    /// Use -1/0 for sites where this value is not applicable.
    /// </summary>
    public int ChaptersPerPage { get; set; }

    /// <summary>
    /// Page index offset (0- or 1-based) for pagination.
    /// </summary>
    public int PageOffSet { get; set; }

    /// <summary>
    /// Substring that indicates a completed novel when found in the status text.
    /// </summary>
    public string? CompletedStatus { get; set; }

    /// <summary>
    /// True if chapter "content" is a list of images/pages (manga/comics style).
    /// When true, ChapterContent selects image nodes and the scraper downloads them.
    /// </summary>
    public bool HasImagesForChapterContent { get; set; }

    /// <summary>
    /// True if page requires premium/paid access for some chapters.
    /// </summary>
    public bool HasPremiumChapters { get; set; }

    /// <summary>
    /// True if the entire site must be scraped using Selenium (JS-rendered).
    /// </summary>
    public bool EntireSiteRequiresSelenium { get; init; }

    /// <summary>
    /// True if the table of contents requires Selenium to load chapter links.
    /// </summary>
    public bool TableOfContentsRequiresSelenium { get; set; }

    /// <summary>
    /// True if chapter pages require Selenium to load chapter content.
    /// This is separate from <see cref="HasImagesForChapterContent"/>.
    /// </summary>
    public bool ChapterContentRequiresSelenium { get; set; }

    /// <summary>
    /// Indicates the Cloudflare protection level expected for the site.
    /// </summary>
    public CloudflareProtectionLevel? CloudflareProtection { get; set; }

    /// <summary>
    /// Determines how chapter links should be ordered after extraction.
    /// </summary>
    public ChapterSortOrder ChapterSortOrder { get; set; } = ChapterSortOrder.Ascending;

    /// <summary>
    /// XPath selectors and attribute names specific to this site.
    /// </summary>
    public Selectors Selectors { get; init; }

    public PremiumInfo? PremiumInfo { get; set; }

    /// <summary>
    /// Minimum number of matching nodes considered "enough" for ChapterContent.
    /// If fewer nodes are found, the scraper may try AlternativeChapterContent.
    /// 
    /// Null means use the application default.
    /// </summary>
    public int? MinimumChapterParagraphThreshold { get; set; }
}

public sealed class TableOfContentsSelectors
{
    /// <summary>
    /// XPath for all chapter link nodes on the table of contents page.
    /// Prefer selecting the &lt;a&gt; element(s), not the @href attribute, since the code typically reads Attributes["href"].
    /// </summary>
    public string? ChapterLinks { get; set; }

    /// <summary>
    /// Optional XPath (relative to each chapter link node) to extract the chapter title text on the TOC.
    /// If null, the link's InnerText is used.
    /// </summary>
    public string? chapterTitleInToc { get; set; }

    /// <summary>
    /// Selectors related to premium/paid chapters.
    /// </summary>
    public PremiumChapterSelectorsRelativeToChapterLinks PremiumChapterSelectors { get; set; } = new PremiumChapterSelectorsRelativeToChapterLinks();

    /// <summary>
    /// XPath for the pagination list items on the table of contents page.
    /// Null if the site has no pagination.
    /// </summary>
    public string? TableOfContentsPaginationListItems { get; set; }

    /// <summary>
    /// XPath for the "last page" button/link on the table of contents.
    /// Null if the site has no pagination or has no last-page button.
    /// </summary>
    public string? LastTableOfContentsPage { get; set; }

    /// <summary>
    /// Attribute name used to extract the last page number (or URL) from <see cref="LastTableOfContentsPage"/>.
    /// Common values: "href", "data-page".
    /// </summary>
    public string? LastTableOfContentPageNumberAttribute { get; set; }

    /// <summary>
    /// XPath for the "latest chapter" link on the table of contents page.
    /// Used to show the most recent chapter and/or jump to the latest.
    /// </summary>
    public string? LatestChapterLink { get; set; }

    /// <summary>
    /// XPath for the novel status text (e.g., Completed/Ongoing).
    /// </summary>
    public string? NovelStatus { get; set; }

    /// <summary>
    /// XPath for the novel author text.
    /// </summary>
    public string? NovelAuthor { get; set; }

    /// <summary>
    /// XPath for the novel alternative names (may be multiple text nodes).
    /// </summary>
    public string? NovelAlternativeNames { get; set; }

    /// <summary>
    /// XPath for the novel genre list items.
    /// </summary>
    public string? NovelGenres { get; set; }

    /// <summary>
    /// XPath for the novel rating value.
    /// </summary>
    public string? NovelRating { get; set; }

    /// <summary>
    /// XPath for the novel title.
    /// </summary>
    public string? NovelTitle { get; set; }

    /// <summary>
    /// XPath for total ratings/votes count.
    /// </summary>
    public string? TotalRatings { get; set; }

    /// <summary>
    /// XPath for the novel description container/text nodes.
    /// Can be a union XPath (e.g. "//p | //h2").
    /// </summary>
    public string? NovelDescription { get; set; }

    /// <summary>
    /// XPath for the novel thumbnail image element.
    /// The URL value is extracted from <see cref="ThumbnailUrlAttribute"/>.
    /// </summary>
    public string? NovelThumbnailUrl { get; set; }

    /// <summary>
    /// Attribute name that contains the thumbnail URL on the node selected by <see cref="NovelThumbnailUrl"/>.
    /// Common values: "src", "data-src".
    /// </summary>
    public string? ThumbnailUrlAttribute { get; set; }
}

public sealed class Selectors
{
    /// <summary>
    /// Table of contents specific selectors (chapter links, pagination, novel meta on TOC, premium info).
    /// </summary>
    public TableOfContentsSelectors TableOfContents { get; init; }

    /// <summary>
    /// Dictionary mapping currency names to XPath selectors for user balance in the global navigation bar.
    /// Key: Currency name (e.g., "Karma", "SpiritStones")
    /// Value: XPath selector to extract the user's current balance for that currency
    ///
    /// These selectors typically target elements in the user menu/profile dropdown (global nav).
    /// Can be accessed from any page (TOC, chapter pages) to check/verify balance.
    ///
    /// Example for WuxiaWorld:
    /// {
    ///   "Karma": "//svg[@data-testid='YinYangIcon']/following-sibling::p/text()",
    ///   "SpiritStones": "//svg[@width='21' and @height='20']/following-sibling::p/text()"
    /// }
    /// </summary>
    public Dictionary<string, string>? UserCurrencyBalances { get; set; }

    /// <summary>
    /// XPath for the chapter title node on a chapter page.
    /// </summary>
    public string? ChapterTitle { get; set; }

    /// <summary>
    /// XPath for chapter content nodes.
    /// For text novels, typically selects.
    /// For image-based chapters (manga/comics), typically selects nodes containing image URLs (e.g. div[@data-url]).
    /// </summary>
    public string? ChapterContent { get; set; }

    /// <summary>
    /// Fallback XPath for chapter content when <see cref="ChapterContent"/> does not return enough nodes.
    /// Often set to "//p".
    /// </summary>
    public string? AlternativeChapterContent { get; set; }

    /// <summary>
    /// Attribute name on the node selected by <see cref="ChapterContent"/> used to extract the image URL.
    /// Only used when the site has images for chapter content.
    /// </summary>
    public string? ChapterContentImageUrlAttribute { get; set; }

    /// <summary>
    /// XPath for the "next chapter" button/link on chapter pages.
    /// Used for SPA-aware navigation to avoid full page reloads that destroy auth state.
    /// When set alongside an authenticated session, enables client-side navigation between chapters.
    /// </summary>
    public string? NextChapterButton { get; set; }
}

public sealed class PremiumChapterSelectorsRelativeToChapterLinks
{
    public string? PremiumCost { get; set; }

    public string? PremiumIndicator { get; set; }
}

public sealed class PremiumInfo
{
    /// <summary>
    /// Primary currency name displayed on table of contents for premium chapters.
    /// Default: "Credits"
    /// </summary>
    public string CurrencyName { get; set; } = "Credits";
}