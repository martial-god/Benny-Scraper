using BennyScraper.DataAccess.Data;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;

namespace BennyScraper.DataAccess.Repository;

public class PageRepository : Repository<Page>, IPageRepository
{
    private Database _db;

    public PageRepository(Database db)
        : base(db)
    {
        _db = db;
    }

    public void Update(Page page) => _db.Pages.Update(page);

    public void AddRange(ICollection<Page> pages) => _db.Pages.AddRange(pages);
}