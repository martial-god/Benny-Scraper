
using Benny_Scraper.Models;

namespace Benny_Scraper
{
    public interface INovelPageScraper
    {
        public Task<List<ChapterData>> GetChaptersDataAsync(List<string> chapterUrls, string titleXPathSelector, string contentXPathSelector, string novelTitle);
        public Task<string> GetLatestChapterAsync(string xPathSelector, Uri uri);
    }
}
