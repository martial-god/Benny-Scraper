using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository.IRepository
{
    public interface INovelRepository : IRepository<Novel>
    {
        void Update(Novel obj);
    }
}
