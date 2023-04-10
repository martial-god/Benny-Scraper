using Benny_Scraper.Models;
using Microsoft.EntityFrameworkCore;

namespace Benny_Scraper.DataAccess.Data
{
    public class ApplicationDbContext : DbContext
    {
        // Clear nuget packages with errors Scaffolding for Identity
        // https://social.msdn.microsoft.com/Forums/en-US/07c93e8b-5092-4211-80e6-3932d87664c3/always-got-this-error-when-scaffolding-suddenly-8220there-was-an-error-running-the-selected-code?forum=aspdotnetcore
        // Setup for this https://learn.microsoft.com/en-us/ef/ef6/modeling/code-first/workflows/new-database?source=recommendations//}

        /// <summary>
        /// Connection string is handled in the Program.cs injection
        /// The ApplicationDbContext class must expose a public constructor with a DbContextOptions<ApplicationDbContext> parameter. 
        /// This is how context configuration from AddDbContext is passed to the DbContext. https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
        /// </summary>
        /// <param name="options"></param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        #region Required
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Novel>().ToTable("novel");
            modelBuilder.Entity<Chapter>().ToTable("chapter");

            // Rename column            
            modelBuilder.Entity<Chapter>().Property(x => x.Id).HasColumnName("id").HasColumnOrder(0);
            modelBuilder.Entity<Chapter>().Property(x => x.NovelId).HasColumnName("novel_id").HasColumnOrder(1);
            modelBuilder.Entity<Chapter>().Property(x => x.Title).HasColumnName("title").HasColumnOrder(2);
            modelBuilder.Entity<Chapter>().Property(x => x.Url).HasColumnName("url").HasColumnOrder(3);
            modelBuilder.Entity<Chapter>().Property(x => x.DateCreated).HasColumnName("date_created").HasColumnOrder(4);
            modelBuilder.Entity<Chapter>().Property(x => x.DateLastModified).HasColumnName("date_last_modified").HasColumnOrder(5);
            modelBuilder.Entity<Chapter>().Property(x => x.Number).HasColumnName("number").HasColumnOrder(6);
            modelBuilder.Entity<Chapter>().Property(x => x.Content).HasColumnName("content").HasColumnOrder(7);

            modelBuilder.Entity<Novel>().Property(x => x.Id).HasColumnName("id");
            modelBuilder.Entity<Novel>().Property(x => x.Title).HasColumnName("title");
            modelBuilder.Entity<Novel>().Property(x => x.Url).HasColumnName("url");
            modelBuilder.Entity<Novel>().Property(x => x.DateCreated).HasColumnName("date_created");
            modelBuilder.Entity<Novel>().Property(x => x.DateLastModified).HasColumnName("date_last_modified");
            modelBuilder.Entity<Novel>().Property(x => x.Author).HasColumnName("author");
            modelBuilder.Entity<Novel>().Property(x => x.Description).HasColumnName("description");
            modelBuilder.Entity<Novel>().Property(x => x.Genre).HasColumnName("genre");
            modelBuilder.Entity<Novel>().Property(x => x.Status).HasColumnName("status");
            modelBuilder.Entity<Novel>().Property(x => x.TotalChapters).HasColumnName("total_chapters");
            modelBuilder.Entity<Novel>().Property(x => x.SiteName).HasColumnName("site_name");
            modelBuilder.Entity<Novel>().Property(x => x.SaveLocation).HasColumnName("save_location");
            modelBuilder.Entity<Novel>().Property(x => x.LastChapter).HasColumnName("last_chapter");
            modelBuilder.Entity<Novel>().Property(x => x.FirstChapter).HasColumnName("first_chapter");
            modelBuilder.Entity<Novel>().Property(x => x.CurrentChapter).HasColumnName("current_chapter");
            modelBuilder.Entity<Novel>().Property(x => x.LastTableOfContentsUrl).HasColumnName("last_table_of_contents_url");
            modelBuilder.Entity<Novel>().Property(x => x.CurrentChapterUrl).HasColumnName("current_chapter_url");


        }
        #endregion

        // Creates maps to the database
        public DbSet<Novel> Novels { get; set; }
        public DbSet<NovelList> NovelLists { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
    }
}
