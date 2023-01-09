using Benny_Scraper.Models;
using Benny_Scraper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Benny_Scraper.DataAccess.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Clear nuget packages with errors Scaffolding for Identity
        // https://social.msdn.microsoft.com/Forums/en-US/07c93e8b-5092-4211-80e6-3932d87664c3/always-got-this-error-when-scaffolding-suddenly-8220there-was-an-error-running-the-selected-code?forum=aspdotnetcore
        // Setup for this https://learn.microsoft.com/en-us/ef/ef6/modeling/code-first/workflows/new-database?source=recommendations
        private readonly string? _connectionString;
        public static string connectionString = "Server=localhost;Database=Test;TrustServerCertificate=True;Trusted_Connection=True;";

        // if this error appears it means that the connection string is not correct
        //Unable to create an object of type 'ApplicationDbContext'. For the different patterns supported at design time, see https://go.microsoft.com/fwlink/?linkid=851728
        // this is what allows the add-migration to work
        public ApplicationDbContext()
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Connection string is handled in the Program.cs
        /// </summary>
        /// <param name="options"></param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseSqlServer(_connectionString, options => options.MigrationsAssembly("Benny-Scraper.DataAccess"));
        }

        // Creates maps to the database
        public DbSet<Novel> Novels { get; set; }
        public DbSet<NovelList> NovelLists { get; set; }
    }

    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(ApplicationDbContext.connectionString, options => options.MigrationsAssembly("Benny-Scraper.DataAccess"));

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
