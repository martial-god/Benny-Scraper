

using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;
using Microsoft.EntityFrameworkCore;

namespace Benny_Scraper.DataAccess.Repository
{
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
}
