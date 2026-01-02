namespace Benny_Scraper.BusinessLogic.Config
{
    public class Selectors
    {
        /// <summary>
        /// XPath for all chapter link nodes on the table of contents page.
        /// Prefer selecting the &lt;a&gt; element(s), not the @href attribute, since the code typically reads Attributes["href"].
        /// </summary>
        public string? ChapterLinks { get; set; }

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
    }
}
