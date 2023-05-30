using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Benny_Scraper.DataAccess.Data
{
    /// <summary>
    /// DesignTimeDbContextFactory is a class implementing IDesignTimeDbContextFactory<T> interface,
    /// which is used by EF Core tools to create an instance of the ApplicationDbContext during design-time operations,
    /// such as running migrations or generating code from the database schema.
    /// It provides a way to configure the DbContext with the correct settings (e.g., connection string)
    /// without relying on the application's runtime configuration.
    /// Resolve issue with not having an IHost due to using Autofac
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();

            string addDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dbPath = Path.Combine(addDataPath, "BennyScraper", "BennyTestDb.db");
            var connectionString = $"Data Source={dbPath};";
            builder.UseSqlite(connectionString);

            return new ApplicationDbContext(builder.Options);
        }
    }
}
