

using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository
{
    internal class ChapterRespository : Repository<Chapter>, IChapterRepository
    {
        private ApplicationDbContext _db;

        /// <summary>
        /// Values will be passed in by the UnitOfWork class
        /// </summary>
        /// <param name="db"></param>
        public ChapterRespository(ApplicationDbContext db) : base(db)
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
    }
}
