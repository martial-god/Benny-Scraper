namespace Benny_Scraper.BusinessLogic.Config;
public class SiteConfiguration
{
    public required string Name { get; set; }
    public required string UrlPattern { get; set; }
    public bool HasPagination { get; set; }
    public string? PaginationType { get; set; }
    public string? PaginationQueryPartial { get; set; }
    public required Selectors Selectors { get; set; }
    public int ChaptersPerPage { get; set; }
    public int PageOffSet { get; set; }
    public string CompletedStatus { get; set; }
    public bool HasNovelInfoOnDifferentPage { get; set; }
    public bool IsSeleniumSite { get; set; }
    public bool HasImagesForChapterContent { get; set; }
}
