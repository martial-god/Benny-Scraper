
using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository
{
    internal class NovelListRepository : Repository<NovelList>, INovelListRepository
    {
        private ApplicationDbContext _db;

        public NovelListRepository(ApplicationDbContext db) : base(db)
        {
            _db = db;
        }

        public void Update(NovelList novelList)
        {
            _db.NovelLists.Update(novelList);
        }
    }
}
