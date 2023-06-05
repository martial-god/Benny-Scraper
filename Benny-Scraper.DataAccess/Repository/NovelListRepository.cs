
using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository
{
    internal class NovelListRepository : Repository<NovelList>, INovelListRepository
    {
        private Database _db;

        public NovelListRepository(Database db) : base(db)
        {
            _db = db;
        }

        public void Update(NovelList novelList)
        {
            _db.NovelLists.Update(novelList);
        }
    }
}
