using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper.DataAccess.Repository;
public class ConfigurationRepository : Repository<Configuration>, IConfigurationRepository
{
    private Database _db;

    public ConfigurationRepository(Database db) : base(db)
    {
        _db = db;
    }
    
    public void Update(Configuration configuration)
    {
        _db.Configurations.Update(configuration);
        _db.SaveChanges();
    }

    public async Task<Configuration> GetByIdAsync(int id)
    {
        return await _db.Configurations.FindAsync(id);
    }

    public Configuration GetById(int id)
    {
        return _db.Configurations.Find(id);
    }
}
