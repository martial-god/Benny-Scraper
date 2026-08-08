using Microsoft.EntityFrameworkCore.Storage;

namespace BennyScraper.DataAccess.Repository.IRepository;

public interface IUnitOfWork
{
    IChapterRepository Chapter { get; }

    INovelRepository Novel { get; }

    IPageRepository Page { get; }

    IConfigurationRepository Configuration { get; }

    Task<int> SaveAsync();

    IDbContextTransaction BeginTransaction();
}