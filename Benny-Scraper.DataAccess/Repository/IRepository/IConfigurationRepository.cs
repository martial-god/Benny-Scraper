using BennyScraper.Models;

namespace BennyScraper.DataAccess.Repository.IRepository;

internal interface IConfigurationRepository : IRepository<Configuration>
{
    void Update(Configuration obj);

    Task<Configuration> GetByIdAsync(int id);
}