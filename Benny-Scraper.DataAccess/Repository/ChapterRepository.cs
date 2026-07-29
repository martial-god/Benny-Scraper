using BennyScraper.DataAccess.Data;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;
using Microsoft.EntityFrameworkCore;

namespace BennyScraper.DataAccess.Repository;

public class ChapterRepository(Database db) : Repository<Chapter>(db), IChapterRepository
{
    private readonly Database _db = db;

    public void Update(Chapter obj) => _db.Chapters.Update(obj);

    public void AddRange(ICollection<Chapter> chapters) => _db.Chapters.AddRange(chapters);

    public Chapter GetLastSavedChapterAsyncByNovelId(Guid novelId)
    {
        return _db.Chapters
            .AsNoTracking()
            .Where(c => c.NovelId == novelId)
            .OrderByDescending(c => c.DateLastModified)
            .First();
    }
}