using Benny_Scraper.Models;
using Microsoft.EntityFrameworkCore;

namespace Benny_Scraper.DataAccess.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Clear nuget packages with errors Scaffolding for Identity
        // https://social.msdn.microsoft.com/Forums/en-US/07c93e8b-5092-4211-80e6-3932d87664c3/always-got-this-error-when-scaffolding-suddenly-8220there-was-an-error-running-the-selected-code?forum=aspdotnetcore
        // Setup for this https://learn.microsoft.com/en-us/ef/ef6/modeling/code-first/workflows/new-database?source=recommendations

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // Creates maps to the database
        public DbSet<Novel> Novels { get; set; }
        public DbSet<NovelList> NovelsList { get; set; }
    }
}
