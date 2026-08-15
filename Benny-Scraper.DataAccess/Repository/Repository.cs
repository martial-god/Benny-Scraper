using System.Linq.Expressions;
using BennyScraper.DataAccess.Data;
using BennyScraper.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore;

namespace BennyScraper.DataAccess.Repository;

internal class Repository<T> : IRepository<T>
    where T : class
{
    private readonly DbSet<T> _dbSet;

    // adds the database context
    private readonly Database _db;

    protected Repository(Database db)
    {
        _db = db;

        // make it so we don't have to keep using _db.Set.Add() or other methods
        this._dbSet = _db.Set<T>(); // set the dbset to the db set of the generic object. This is how we can use the generic repository
    }

    public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity).ConfigureAwait(false);

    /// <summary>
    /// Gets the object by id.
    /// </summary>
    /// <param name="id">The id of the object.</param>
    /// <returns>The object with the specified id.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no object with the specified id is found.</exception>
    public async Task<T> GetByIdAsync(Guid id) =>
        await _dbSet.FindAsync(id).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"No {typeof(T).Name} found with id {id}.");

    /// <summary>
    /// Gets all objects of type T, optionally filtered, ordered, and including related properties.
    /// </summary>
    /// <param name="filter">Ex: filter: obj => obj.IsActive.</param>
    /// <param name="orderBy">Ex: orderBy: q => q.OrderBy(obj => obj.Name).</param>
    /// <param name="includeProperties">Comma-separated list of related properties to include.</param>
    /// <returns>A list of objects of type T.</returns>
    public async Task<IEnumerable<T>> GetAllAsync(
        Expression<Func<T, bool>>? filter = null,
        Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
        string? includeProperties = null)
    {
        IQueryable<T> query = _dbSet;
        if (filter != null)
        {
            query = query.Where(filter);
        }

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        if (includeProperties == null)
        {
            return await query.ToListAsync().ConfigureAwait(false);
        }

        // Will not break if there are commas seperating properties, including ,,,
        query = includeProperties.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Aggregate(query, (current, includeProp) => current.Include(includeProp)); // Sames as ForEach, but more efficient and less code. For each property in the includeProperties string, we include it in the query.

        return await query.ToListAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the first object of type T that matches the specified filter, optionally including related properties.
    /// </summary>
    /// <param name="filter">Ex: filter: obj => obj.IsActive.</param>
    /// <param name="includeProperties">Comma-separated list of related properties to include.</param>
    /// <returns>The first object of type T that matches the filter, or null if no match is found.</returns>
    public Task<T?> GetFirstOrDefaultAsync(
        Expression<Func<T, bool>> filter,
        string? includeProperties = null)
    {
        IQueryable<T> query = _dbSet;
        query = query.Where(filter);
        if (includeProperties != null)
        {
            // Will not brak is there are commas seperating properties, including ,,,
            query = includeProperties.Split([','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Aggregate(query, (current, includeProp) => current.Include(includeProp));
        }

        return query.FirstOrDefaultAsync(); // might return null
    }

    public void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    public void RemoveRange(IEnumerable<T> entity)
    {
        _dbSet.RemoveRange(entity);
    }
}