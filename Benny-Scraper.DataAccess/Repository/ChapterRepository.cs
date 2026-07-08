

using BennyScraper.DataAccess.Data;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;
using Microsoft.EntityFrameworkCore;

namespace BennyScraper.DataAccess.Repository;

public class ChapterRepository : Repository<Chapter>, IChapterRepository
{
    private Database _db;

    /// <summary>
    /// Values will be passed in by the UnitOfWork class
    /// </summary>
    /// <param name="db"></param>
    public ChapterRepository(Database db) : base(db)
    {
        _db = db;
    }

    public void Update(Chapter chapter)
    {
        _db.Chapters.Update(chapter);
    }

    public void AddRange(ICollection<Chapter> chapters)
    {
        _db.Chapters.AddRange(chapters);
    }

    public async Task<Chapter> GetLastSavedChapterAsyncByNovelId(Guid novelId)
    {
        return await _db.Chapters.Where(c => c.NovelId == novelId).OrderByDescending(c => c.DateLastModified).FirstOrDefaultAsync();
    }
}