using Benny_Scraper.Models;

namespace Benny_Scraper
{
    public interface IStartUpService
    {
        public Task CreateNovel(Novel novel);
    }
}
