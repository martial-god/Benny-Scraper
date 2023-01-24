using Benny_Scraper.Models;

namespace Benny_Scraper
{
    public interface INovelService
    {
        public Task CreateNovelAsync(Novel novel);
        public Task<bool> IsNovelInDatabaseAsync(string tableOfContentsUrl);
        public Task<Novel> GetNovelByUrlAsync(string url);
    }
}
