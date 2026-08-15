using BennyScraper.Models;

namespace BennyScraper.DataAccess.Repository.IRepository;

internal interface IPageRepository : IRepository<Page>
{
    void Update(Page page);

    void AddRange(ICollection<Page> pages);
}