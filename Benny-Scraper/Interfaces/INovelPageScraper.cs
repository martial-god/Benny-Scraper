using Benny_Scraper.Models;

namespace Benny_Scraper.Interfaces
{
    public interface INovelPageScraper
    {
        public Task<List<ChapterData>> GetChaptersDataAsync(List<string> chapterUrls, string titleXPathSelector, string contentXPathSelector, string novelTitle);
        public Task<string> GetLatestChapterAsync(string xPathSelector, Uri uri);
        public Task<NovelData> GetChaptersFromCheckPointAsync(string xPathSelector, Uri uri, string currentChapter);
    }
}
