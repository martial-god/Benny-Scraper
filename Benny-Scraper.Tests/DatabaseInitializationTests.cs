using BennyScraper.DataAccess.Data;
using BennyScraper.DataAccess.DbInitializer;
using BennyScraper.DataAccess.Repository;
using BennyScraper.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BennyScraper.Tests;

public sealed class DatabaseInitializationTests
{
    [Fact]
    public async Task InitializeAsyncCreatesAndSeedsNewDatabase()
    {
        SQLitePCL.Batteries.Init();
        var databasePath = Path.Combine(Path.GetTempPath(), $"BennyScraper-{Guid.NewGuid():N}.db");

        try
        {
            var databaseOptions = new DbContextOptionsBuilder<Database>()
                .UseSqlite(
                    $"Data Source={databasePath};",
                    sqliteOptions => sqliteOptions.MigrationsAssembly("Benny-Scraper.DataAccess"))
                .Options;

            var database = new Database(databaseOptions);
            await using (database.ConfigureAwait(false))
            {
                var databaseInitializer = new DbInitializer(database);

                Assert.True(await databaseInitializer.InitializeAsync());
                Assert.True(await database.Database.CanConnectAsync());
                var configuration = await database.Configurations.SingleAsync();
                Assert.Equal("Default", configuration.Name);
                Assert.Equal(SeleniumBrowser.Chrome, configuration.DefaultBrowser);

                configuration.DefaultBrowser = SeleniumBrowser.Firefox;
                new ConfigurationRepository(database).Update(configuration);
            }

            database = new Database(databaseOptions);
            await using (database.ConfigureAwait(false))
            {
                var databaseInitializer = new DbInitializer(database);

                Assert.False(await databaseInitializer.InitializeAsync());
                var configuration = Assert.Single(await database.Configurations.ToListAsync());
                Assert.Equal(SeleniumBrowser.Firefox, configuration.DefaultBrowser);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
            File.Delete(databasePath + "-shm");
            File.Delete(databasePath + "-wal");
        }
    }
}