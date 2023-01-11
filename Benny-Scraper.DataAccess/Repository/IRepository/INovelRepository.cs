using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository.IRepository
{
    internal interface INovelRepository : IRepository<Novel>
    {
        void Update(Novel obj);
    }
}
