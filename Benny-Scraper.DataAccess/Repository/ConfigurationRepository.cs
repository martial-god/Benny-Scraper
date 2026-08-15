using BennyScraper.DataAccess.Data;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;

namespace BennyScraper.DataAccess.Repository;

internal sealed class ConfigurationRepository(Database db) : Repository<Configuration>(db), IConfigurationRepository
{
    private readonly Database _db = db;

    public void Update(Configuration obj)
    {
        _db.Configurations.Update(obj);
        _db.SaveChanges();
    }

    public async Task<Configuration> GetByIdAsync(int id) =>
        await _db.Configurations.FindAsync(id).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"No configuration found with id {id}.");
}