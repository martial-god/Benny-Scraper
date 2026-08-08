using System.Linq.Expressions;

namespace BennyScraper.DataAccess.Repository.IRepository;

public interface IRepository<T>
    where T : class // Generic repository where we can pass in any object
{
    Task<T> GetByIdAsync(Guid id);

    Task<IEnumerable<T>> GetAllAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        string? includeProperties = null);

    Task<T?> GetFirstOrDefaultAsync(
        Expression<Func<T, bool>> filter,
        string? includeProperties = null);

    Task AddAsync(T entity);

    void Remove(T entity);

    void RemoveRange(IEnumerable<T> entity);
}