using BennyScraper.Models;

namespace BennyScraper.DataAccess.Repository.IRepository;

internal interface IChapterRepository : IRepository<Chapter>
{
    void Update(Chapter obj);

    void AddRange(ICollection<Chapter> chapters);

    Task<Chapter> GetLastSavedChapterAsyncByNovelId(Guid novelId);
}