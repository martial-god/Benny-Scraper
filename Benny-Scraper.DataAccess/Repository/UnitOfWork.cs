using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;

namespace Benny_Scraper.DataAccess.Repository
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly Database _db;

        public UnitOfWork(Database db)
        {
            _db = db;
            Chapter = new ChapterRepository(_db);
            Novel = new NovelRepository(_db);
            NovelList = new NovelListRepository(_db);
            Page = new PageRepository(_db);
            Configuration = new ConfigurationRepository(_db);
        }

        public IChapterRepository Chapter { get; private set; }
        public INovelRepository Novel { get; private set; }
        public INovelListRepository NovelList { get; private set; }
        public IPageRepository Page { get; private set; }
        public IConfigurationRepository Configuration { get; private set; }

        public Task<int> SaveAsync()
        {
            return _db.SaveChangesAsync();
        }

    }
}
