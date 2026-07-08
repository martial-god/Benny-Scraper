using BennyScraper.Models;

namespace BennyScraper.DataAccess.Repository.IRepository;

public interface IPageRepository : IRepository<Page>
{
    void Update(Page page);

    void AddRange(ICollection<Page> pages);
}