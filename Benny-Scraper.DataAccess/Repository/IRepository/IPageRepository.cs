using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository.IRepository
{
    public interface IPageRepository : IRepository<Page>
    {
        void Update(Page page);
        void AddRange(ICollection<Page> pages);
    }
}
