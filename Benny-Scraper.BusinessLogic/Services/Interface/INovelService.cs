using Benny_Scraper.Models;
using System.Linq.Expressions;

namespace Benny_Scraper.BusinessLogic.Services.Interface
{
    public interface INovelService
    {
        public Task<Guid> CreateAsync(Novel novel);
        public Task<bool> IsNovelInDatabaseAsync(string tableOfContentsUrl);
        public Task<bool> IsNovelInDatabaseAsync(Guid id);
        public Task<Novel> GetByUrlAsync(Uri uri);
        public Task<Novel> GetByIdAsync(Guid id);
        public Task<IEnumerable<Novel>> GetAllAsync();
        public Task UpdateAsync(Novel novel);
        public Task UpdateAndAddChaptersAsync(Novel novel, IEnumerable<Chapter> chapters);
        public Task RemoveAllAsync();
        public Task RemoveByIdAsync(Guid id);
    }
}
