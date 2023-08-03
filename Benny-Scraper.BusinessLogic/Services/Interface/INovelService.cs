using Benny_Scraper.Models;
using System.Linq.Expressions;

namespace Benny_Scraper.BusinessLogic.Services.Interface
{
    public interface INovelService
    {
        public Task CreateAsync(Novel novel);
        public Task<bool> IsNovelInDatabaseAsync(string tableOfContentsUrl);
        public Task<Novel> GetByUrlAsync(Uri uri);
        public Task<IEnumerable<Novel>> GetAllAsync();
        public Task UpdateAndAddChapters(Novel novel, IEnumerable<Chapter> chapters);
        public Task RemoveAllAsync();
        public Task RemoveByIdAsync(Guid id);
    }
}
