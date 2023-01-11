using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository.IRepository
{
    internal interface INovelListRepository : IRepository<NovelList>
    {
        void Update(NovelList obj);
    }
}
