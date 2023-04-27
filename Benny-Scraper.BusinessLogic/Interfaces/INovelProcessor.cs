using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.Interfaces
{
    public interface INovelProcessor
    {
        public Task ProcessNovelAsync(Uri novelTableOfContentsUri);
        public Task<object> CreateEpubAsync(Novel novel, List<Models.Chapter> chapters, string outputPath);
    }
}
