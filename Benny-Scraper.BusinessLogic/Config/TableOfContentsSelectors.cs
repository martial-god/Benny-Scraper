namespace BennyScraper.BusinessLogic.Config;

public sealed class TableOfContentsSelectors
{
    /// <summary>
    /// Gets or sets the XPath for all chapter link nodes on the table of contents page.
    /// Prefer selecting the &lt;a&gt; element(s), not the @href attribute, since the code typically reads Attributes["href"].
    /// </summary>
    public string? ChapterLinks { get; set; }

    /// <summary>
    /// Gets or sets the optional XPath (relative to each chapter link node) to extract the chapter title text on the TOC.
    /// If null, the link's InnerText is used.
    /// </summary>
    public string? ChapterTitleInToc { get; set; }

    /// <summary>
    /// Gets or sets the selectors related to premium/paid chapters.
    /// </summary>
    public PremiumChapterSelectorsRelativeToChapterLinks PremiumChapterSelectors { get; set; } = new PremiumChapterSelectorsRelativeToChapterLinks();

    /// <summary>
    /// Gets or sets the XPath for the pagination list items on the table of contents page.
    /// Null if the site has no pagination.
    /// </summary>
    public string? TableOfContentsPaginationListItems { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the "last page" button/link on the table of contents.
    /// Null if the site has no pagination or has no last-page button.
    /// </summary>
    public string? LastTableOfContentsPage { get; set; }

    /// <summary>
    /// Gets or sets the attribute name used to extract the last page number (or URL) from <see cref="LastTableOfContentsPage"/>.
    /// Common values: "href", "data-page".
    /// </summary>
    public string? LastTableOfContentPageNumberAttribute { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the "latest chapter" link on the table of contents page.
    /// Used to show the most recent chapter and/or jump to the latest.
    /// </summary>
    public string? LatestChapterLink { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the novel status text (e.g., Completed/Ongoing).
    /// </summary>
    public string? NovelStatus { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the novel author text.
    /// </summary>
    public string? NovelAuthor { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the novel alternative names (may be multiple text nodes).
    /// </summary>
    public string? NovelAlternativeNames { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the novel genre list items.
    /// </summary>
    public string? NovelGenres { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the novel rating value.
    /// </summary>
    public string? NovelRating { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the novel title.
    /// </summary>
    public string? NovelTitle { get; set; }

    /// <summary>
    /// Gets or sets the XPath for total ratings/votes count.
    /// </summary>
    public string? TotalRatings { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the novel description container/text nodes.
    /// Can be a union XPath (e.g. "//p | //h2").
    /// </summary>
    public string? NovelDescription { get; set; }

    /// <summary>
    /// Gets or sets the XPath for the novel thumbnail image element.
    /// The URL value is extracted from <see cref="ThumbnailUrlAttribute"/>.
    /// </summary>
    public string? NovelThumbnailUrl { get; set; }

    /// <summary>
    /// Gets or sets the attribute name that contains the thumbnail URL on the node selected by <see cref="NovelThumbnailUrl"/>.
    /// Common values: "src", "data-src".
    /// </summary>
    public string? ThumbnailUrlAttribute { get; set; }
}
