using Benny_Scraper.Models;

namespace Benny_Scraper
{
    public interface INovelService
    {
        public Task CreateAsync(Novel novel);
        public Task<bool> IsNovelInDatabaseAsync(string tableOfContentsUrl);
        public Task<Novel> GetByUrlAsync(Uri uri);
        public Task UpdateAsync(Novel novel);
    }
}
