
using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository
{
    public class NovelRepository : Repository<Novel>, INovelRepository
    {
        private Database _db;

        /// <summary>
        /// Values will be passed in by the UnitOfWork class
        /// </summary>
        /// <param name="db"></param>
        public NovelRepository(Database db) : base(db)
        {
            _db = db;
        }

        public void Update(Novel novel)
        {
            _db.Novels.Update(novel);
        }

        public void UpdateRange(ICollection<Chapter> chapters)
        {
            _db.Chapters.UpdateRange(chapters);
        }
    }
}
