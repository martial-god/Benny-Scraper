using BennyScraper.DataAccess.Data;
using BennyScraper.Models;
using Microsoft.EntityFrameworkCore;

namespace BennyScraper.DataAccess.DbInitializer;

public class DbInitializer
{
    private readonly Database _db;

    public DbInitializer(Database db)
    {
        _db = db;
    }

    public bool Initialize()
    {
        bool changesMade = false;
        try
        {
            if (_db.Database.GetPendingMigrations().Any())
            {
                _db.Database.Migrate();
                changesMade = true;
            }

            if (SeedData().Result)
            {
                changesMade = true;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }

        return changesMade;
    }

    private async Task<bool> SeedData()
    {
        bool dataSeeded = false;
        using var transaction = await _db.Database.BeginTransactionAsync().ConfigureAwait(false);
        try
        {
            if (!_db.Configurations.Any())
            {
                var defaultConfig = new Configuration
                {
                    Name = "Default",
                    AutoUpdate = false,
                    ConcurrencyLimit = 2,
                    SaveLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BennyScrapedNovels"),
                    NovelSaveLocation = string.Empty,
                    MangaSaveLocation = string.Empty,
                    LogLocation = string.Empty,
                    DatabaseLocation = string.Empty,
                    DatabaseFileName = "BennyTestDb.db",
                    SaveAsSingleFile = true,
                    DefaultMangaFileExtension = FileExtension.Pdf,
                    DefaultLogLevel = LogLevel.Info,
                    FontType = "Arial"
                };
                _db.Configurations.Add(defaultConfig);
                await _db.SaveChangesAsync().ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);
                dataSeeded = true;
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
            throw new InvalidOperationException("An error occurred while seeding the database: " + ex.Message, ex);
        }

        return dataSeeded;
    }
}