using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.Models;
using Microsoft.EntityFrameworkCore;

namespace Benny_Scraper.DataAccess.DbInitializer;
public class DbInitializer(Database db)
{
    /// <summary>
    /// Initializes the database. Will allow us to not need to call update-databse in the package manager console.
    /// If there are migrations, apply them
    /// </summary>
    /// <exception cref="Exception"></exception>
    public bool Initialize()
    {
        bool changesMade = false;
        // apply Migrations if they are not applied
        try
        {
            if (db.Database.GetPendingMigrations().Any())
            {
                db.Database.Migrate();
                changesMade = true;
            }

            if (SeedData().Result)
            {
                changesMade = true;
            }
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
        return changesMade;
    }

    public async Task<bool> SeedData()
    {
        bool dataSeeded = false;
        using var transaction = db.Database.BeginTransaction();
        try
        {
            if (!db.Configurations.Any())
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
                db.Configurations.Add(defaultConfig);
                db.SaveChanges();
                await transaction.CommitAsync();
                dataSeeded = true;
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new Exception("An error occurred while seeding the database: " + ex.Message, ex);
        }
        return dataSeeded;
    }
}