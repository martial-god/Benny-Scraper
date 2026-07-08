using BennyScraper.Models;

namespace BennyScraper.DataAccess.Repository.IRepository;

public interface INovelRepository : IRepository<Novel>
{
    void Update(Novel? obj);

    void UpdateRange(ICollection<Chapter> chapters);
}