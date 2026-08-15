using BennyScraper.DataAccess.Data;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;

namespace BennyScraper.DataAccess.Repository;

internal sealed class NovelRepository(Database db) : Repository<Novel>(db), INovelRepository
{
    private readonly Database _db = db;

    public void Update(Novel? obj)
    {
        if (obj == null)
        {
            return;
        }

        _db.Novels.Update(obj);
    }

    public void UpdateRange(ICollection<Chapter> chapters) => _db.Chapters.UpdateRange(chapters);
}