using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic
{
    /// <summary>
    /// A selenium implementation of the INovelScraper interface. Use this for sites that require login-in to get the chapter contents like wuxiaworld.com
    /// </summary>
    public class SeleniumNovelScraper : INovelScraper
    {
        
        public Task<List<string>> BuildChaptersUrlsFromTableOfContentUsingPaginationAsync(int pageToStartAt, Uri siteUrl, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> BuildChaptersUrlsFromTableOfContentUsingPaginationAsync(int pageToStartAt, Uri siteUrl, SiteConfiguration siteConfig, string lastSavedChaptersName)
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> BuildChaptersUrlsFromTableOfContentUsingPaginationAsync(int pageToStartAt, Uri siteUrl, SiteConfiguration siteConfig, Uri lastSavedChapterUri)
        {
            throw new NotImplementedException();
        }

        public Task<List<ChapterData>> GetChaptersDataAsync(List<string> chapterUrls, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }

        public Task<NovelData> GetChaptersFromCheckPointAsync(Uri novelTableOfContentLatestUri, string currentChapter, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetLatestChapterNameAsync(Uri uri, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }

        public Task GoToTableOfContentsPageAsync(Uri novelTableOfContentsUri)
        {
            throw new NotImplementedException();
        }
    }
}
