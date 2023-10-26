using Benny_Scraper.Models;
using Microsoft.EntityFrameworkCore;

namespace Benny_Scraper.DataAccess.Data
{
    public class Database : DbContext
    {
        // Clear nuget packages with errors Scaffolding for Identity
        // https://social.msdn.microsoft.com/Forums/en-US/07c93e8b-5092-4211-80e6-3932d87664c3/always-got-this-error-when-scaffolding-suddenly-8220there-was-an-error-running-the-selected-code?forum=aspdotnetcore
        // Setup for this https://learn.microsoft.com/en-us/ef/ef6/modeling/code-first/workflows/new-database?source=recommendations//}

        /// <summary>
        /// Connection string is handled in the Program.cs injection
        /// The Database class must expose a public constructor with a DbContextOptions<Database> parameter. 
        /// This is how context configuration from AddDbContext is passed to the DbContext. https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
        /// </summary>
        /// <param name="options"></param>
        public Database(DbContextOptions<Database> options) : base(options)
        {
        }

        #region Required
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Novel>().ToTable("novel");
            modelBuilder.Entity<Chapter>().ToTable("chapter");
            modelBuilder.Entity<Page>().ToTable("page");
            modelBuilder.Entity<Configuration>().ToTable("configuration");

            // Rename column            
            modelBuilder.Entity<Chapter>().Property(x => x.Id).HasColumnName("id").HasColumnOrder(0);
            modelBuilder.Entity<Chapter>().Property(x => x.NovelId).HasColumnName("novel_id").HasColumnOrder(1);
            modelBuilder.Entity<Chapter>().Property(x => x.Title).HasColumnName("title").HasColumnOrder(2);
            modelBuilder.Entity<Chapter>().Property(x => x.Url).HasColumnName("url").HasColumnOrder(3);
            modelBuilder.Entity<Chapter>().Property(x => x.DateCreated).HasColumnName("date_created").HasColumnOrder(4);
            modelBuilder.Entity<Chapter>().Property(x => x.DateLastModified).HasColumnName("date_last_modified").HasColumnOrder(5);
            modelBuilder.Entity<Chapter>().Property(x => x.Number).HasColumnName("number").HasColumnOrder(6);
            modelBuilder.Entity<Chapter>().Property(x => x.Content).HasColumnName("content").HasColumnOrder(7);
            modelBuilder.Entity<Chapter>()
                .HasMany(x => x.Pages)
                .WithOne(x => x.Chapter)
                .HasForeignKey(x => x.ChapterId);

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
            modelBuilder.Entity<Novel>().Property(x => x.FileType).HasColumnName("file_type");
            modelBuilder.Entity<Novel>().Property(x => x.SavedFileIsSplit).HasColumnName("saved_file_is_split");
            modelBuilder.Entity<Novel>()
                .HasMany(x => x.Chapters)
                .WithOne(x => x.Novel)
                .HasForeignKey(x => x.NovelId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Page>().Property(x => x.Id).HasColumnName("id");
            modelBuilder.Entity<Page>().Property(x => x.ChapterId).HasColumnName("chapter_id");
            modelBuilder.Entity<Page>().Property(x => x.Url).HasColumnName("url");
            modelBuilder.Entity<Page>().Property(x => x.Image).HasColumnName("image");
            modelBuilder.Entity<Page>()
                .HasOne(x => x.Chapter)
                .WithMany(x => x.Pages)
                .HasForeignKey(x => x.ChapterId);

            modelBuilder.Entity<Configuration>().Property(x => x.Id).HasColumnName("id");
            modelBuilder.Entity<Configuration>().Property(x => x.Name).HasColumnName("name");
            modelBuilder.Entity<Configuration>().Property(x => x.AutoUpdate).HasColumnName("auto_update");
            modelBuilder.Entity<Configuration>().Property(x => x.ConcurrencyLimit).HasColumnName("concurrency_limit");
            modelBuilder.Entity<Configuration>().Property(x => x.SaveLocation).HasColumnName("save_location");
            modelBuilder.Entity<Configuration>().Property(x => x.NovelSaveLocation).HasColumnName("novel_save_location");
            modelBuilder.Entity<Configuration>().Property(x => x.MangaSaveLocation).HasColumnName("manga_save_location");
            modelBuilder.Entity<Configuration>().Property(x => x.LogLocation).HasColumnName("log_location");
            modelBuilder.Entity<Configuration>().Property(x => x.DatabaseLocation).HasColumnName("database_locatoin");
            modelBuilder.Entity<Configuration>().Property(x => x.DatabaseFileName).HasColumnName("database_file_name");
            modelBuilder.Entity<Configuration>().Property(x => x.DefaultMangaFileExtension).HasColumnName("default_manga_file_extension");
            modelBuilder.Entity<Configuration>().Property(x => x.DefaultLogLevel).HasColumnName("default_log_level");
            modelBuilder.Entity<Configuration>().Property(x => x.SaveAsSingleFile).HasColumnName("save_as_single_file");
        }
        #endregion

        // Creates maps to the database
        public DbSet<Novel> Novels { get; set; }
        public DbSet<NovelList> NovelLists { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Page> Pages { get; set; }
        public DbSet<Configuration> Configurations { get; set; }
    }
}
