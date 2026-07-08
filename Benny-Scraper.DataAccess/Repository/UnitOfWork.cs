using BennyScraper.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore.Storage;
using Database = BennyScraper.DataAccess.Data.Database;

namespace BennyScraper.DataAccess.Repository;

public class UnitOfWork : IUnitOfWork
{
    private readonly Database _db;

    public UnitOfWork(Database db)
    {
        _db = db;
        Chapter = new ChapterRepository(_db);
        Novel = new NovelRepository(_db);
        Page = new PageRepository(_db);
        Configuration = new ConfigurationRepository(_db);
    }

    public IChapterRepository Chapter { get; private set; }

    public INovelRepository Novel { get; private set; }

    public IPageRepository Page { get; private set; }

    public IConfigurationRepository Configuration { get; private set; }

    public Task<int> SaveAsync()
    {
        return _db.SaveChangesAsync();
    }

    public IDbContextTransaction BeginTransaction()
    {
        return _db.Database.BeginTransaction();
    }
}