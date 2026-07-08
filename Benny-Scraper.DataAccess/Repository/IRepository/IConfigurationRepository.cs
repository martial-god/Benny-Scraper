using BennyScraper.Models;

namespace BennyScraper.DataAccess.Repository.IRepository;

public interface IConfigurationRepository : IRepository<Configuration>
{
    void Update(Configuration obj);

    Task<Configuration> GetByIdAsync(int id);
}