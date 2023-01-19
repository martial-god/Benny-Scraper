using Benny_Scraper.Models;

namespace Benny_Scraper
{
    public interface INovelService
    {
        public Task CreateNovelAsync(Novel novel);
    }
}
