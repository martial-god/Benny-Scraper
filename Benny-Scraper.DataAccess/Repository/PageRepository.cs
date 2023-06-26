using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository
{
    public class PageRepository : Repository<Page>, IPageRepository
    {
        private Database _db;

        /// <summary>
        /// Values will be passed in by the UnitOfWork class
        /// </summary>
        /// <param name="db"></param>
        public PageRepository(Database db) : base(db)
        {
            _db = db;
        }

        public void Update(Page page)
        {
            _db.Pages.Update(page);
        }

        public void AddRange(ICollection<Page> pages)
        {
            _db.Pages.AddRange(pages);
        }        
    }
}
