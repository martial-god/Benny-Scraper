using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.Interfaces
{
    public interface INovelScraper
    {
        public Task<string> GetLatestChapterNameAsync(Uri uri, SiteConfiguration siteConfig);
        public Task GoToTableOfContentsPageAsync(Uri novelTableOfContentsUri);
        public Task<List<string>> BuildChaptersUrlsFromTableOfContentUsingPaginationAsync(int pageToStartAt, Uri siteUrl, SiteConfiguration siteConfig, string lastSavedChapterUrl);
        Task<NovelData> GetChaptersFromCheckPointAsync(Uri novelTableOfContentLatestUri, string currentChapter, SiteConfiguration siteConfig);
        Task<List<ChapterData>> GetChaptersDataAsync(List<string> chapterUrls, SiteConfiguration siteConfig);
    }
}
