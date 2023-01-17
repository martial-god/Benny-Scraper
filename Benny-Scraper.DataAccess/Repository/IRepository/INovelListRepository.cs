using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository.IRepository
{
    public interface INovelListRepository : IRepository<NovelList>
    {
        void Update(NovelList obj);
    }
}
