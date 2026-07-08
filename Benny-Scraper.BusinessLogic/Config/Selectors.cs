namespace BennyScraper.BusinessLogic.Config;

public sealed class Selectors
{
    /// <summary>
    /// Gets the table of contents specific selectors (chapter links, pagination, novel meta on TOC, premium info).
    /// </summary>
    public TableOfContentsSelectors TableOfContents { get; init; }

    /// <summary>
    /// Gets the dictionary mapping currency names to XPath selectors for user balance in the global navigation bar.
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
    public Dictionary<string, string> UserCurrencyBalances { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the XPath for the chapter title node on a chapter page.
    /// </summary>
    public string? ChapterTitle { get; set; }

    /// <summary>
    /// Gets or sets the XPath for chapter content nodes.
    /// For text novels, typically selects.
    /// For image-based chapters (manga/comics), typically selects nodes containing image URLs (e.g. div[@data-url]).
    /// </summary>
    public string? ChapterContent { get; set; }

    /// <summary>
    /// Gets or sets the fallback XPath for chapter content when <see cref="ChapterContent"/> does not return enough nodes.
    /// Often set to "//p".
    /// </summary>
    public string? AlternativeChapterContent { get; set; }

    /// <summary>
    /// Gets or sets the attribute name on the node selected by <see cref="ChapterContent"/> used to extract the image URL.
    /// Only used when the site has images for chapter content.
    /// </summary>
    public string? ChapterContentImageUrlAttribute { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the "next chapter" button/link on chapter pages.
    /// Used for SPA-aware navigation to avoid full page reloads that destroy auth state.
    /// When set alongside an authenticated session, enables client-side navigation between chapters.
    /// </summary>
    public string? NextChapterButton { get; set; }
}
