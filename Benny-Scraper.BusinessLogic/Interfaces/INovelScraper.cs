using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.Interfaces
{
    public interface INovelScraper
    {
        public ScraperStrategy? GetScraperStrategy(Uri novelTableOfContentsUri, SiteConfiguration siteConfig);
        public Task<string> GetLatestChapterNameAsync(Uri uri, SiteConfiguration siteConfig);
        public Task<NovelData> RequestPaginatedDataAsync(Uri siteUrl, SiteConfiguration siteConfig, string lastSavedChapterUrl, bool getAllChapters, int pageToStartAt = 1);
        Task<List<ChapterData>> GetChaptersDataAsync(List<string> chapterUrls, SiteConfiguration siteConfig);
        public Task<NovelData> GetNovelDataAsync(Uri novelTableOfContentsUri, SiteConfiguration siteConfig);
    }
}
