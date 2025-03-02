
using System.Linq.Expressions;
using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore;

namespace Benny_Scraper.DataAccess.Repository;
public class Repository<TGenericDbObject>(Database db) : IRepository<TGenericDbObject>
        where TGenericDbObject : class
{
    // adds the database context
    private readonly DbSet<TGenericDbObject> _dbSet = db.Set<TGenericDbObject>(); // set the dbset to the db set of the generic object. This is how we can use the generic repository

    // make it so we don't have to keep using _db.Set.Add() or other methods

    public void Add(TGenericDbObject entity)
    {
        _dbSet.Add(entity);
    }

    public void AddRange(IEnumerable<TGenericDbObject> entities)
    {
        _dbSet.AddRange(entities);
    }

    public async Task AddAsync(TGenericDbObject entity, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        await _dbSet.AddAsync(entity, cancellation);
    }

    public async Task AddRangeAsync(IEnumerable<TGenericDbObject> entities, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        await _dbSet.AddRangeAsync(entities, cancellation);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id">A non-empty guid</param>
    /// <returns></returns>
    public TGenericDbObject? GetById(Guid id)
    {
        return _dbSet.Find(id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id">A non-empty guid</param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public async Task<TGenericDbObject?> GetByIdAsync(Guid id, CancellationToken cancellation = default)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            return await _dbSet.FindAsync(id, cancellation);
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filter">Ex: filter: obj => obj.IsActive,</param>
    /// <param name="orderBy">Ex: orderBy: o => o.OrderByDescending(obj => obj.DateCreated)</param>
    /// <param name="includedProperties"></param>
    /// <returns></returns>
    public IEnumerable<TGenericDbObject> GetAll(Expression<Func<TGenericDbObject, bool>>? filter = null,
        Func<IQueryable<TGenericDbObject>, IOrderedQueryable<TGenericDbObject>>? orderBy = null,
        string? includedProperties = null)
    {
        IQueryable<TGenericDbObject> query = _dbSet;
        if (filter != null)
            query = query.Where(filter);

        if (orderBy != null)
            query = orderBy(query);

        if (includedProperties != null)
        {
            // Will not break if there are commas seperating properties, including ,,,
            foreach (var includedProp in includedProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                query = query.Include(includedProp);
            }
        }
        return query.ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filter">Ex: filter: obj => obj.IsActive,</param>
    /// <param name="orderBy"></param>
    /// <param name="includeProperties"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<TGenericDbObject>> GetAllAsync(Expression<Func<TGenericDbObject, bool>>? filter = null,
        Func<IQueryable<TGenericDbObject>, IOrderedQueryable<TGenericDbObject>>? orderBy = null,
        string? includeProperties = null,
        CancellationToken cancellationToken = default
        )
    {
        IQueryable<TGenericDbObject> query = _dbSet;
        if (filter != null)
            query = query.Where(filter);

        if (orderBy != null)
            query = orderBy(query);

        if (includeProperties != null)
        {
            // Will not break if there are commas seperating properties, including ,,,
            foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                query = query.Include(includeProp); // Include property so that our js files don't break when trying to get data from GetAll() from the API get
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filter">Ex: filter: obj => obj.IsActive,</param>
    /// <param name="includeProperties"></param>
    /// <returns></returns>
    public TGenericDbObject GetFirstOrDefault(Expression<Func<TGenericDbObject, bool>> filter, string? includeProperties = null)
    {
        IQueryable<TGenericDbObject> query = _dbSet;
        query = query.Where(filter);
        if (includeProperties != null)
        {
            // Will not brak is there are commas seperating properties, including ,,,
            foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                query = query.Include(includeProp); // Include property so that our js files don't break when trying to get data from GetAll() from the API get
            }
        }
        return query.FirstOrDefault(); // might return null
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filter">Ex: filter: obj => obj.IsActive,</param>
    /// <param name="includeProperties"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<TGenericDbObject?> GetFirstOrDefaultAsync(Expression<Func<TGenericDbObject, bool>> filter,
        string? includeProperties = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TGenericDbObject> query = _dbSet;
        query = query.Where(filter);
        if (includeProperties != null)
        {
            // Will not brak is there are commas seperating properties, including ,,,
            foreach (var includeProp in includeProperties.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                query = query.Include(includeProp); // Include property so that our js files don't break when trying to get data from GetAll() from the API get
            }
        }

        cancellationToken.ThrowIfCancellationRequested(); // will cancell this task
        return query.FirstOrDefaultAsync(cancellationToken); // might return null
    }

    public void Remove(TGenericDbObject entity)
    {
        _dbSet.Remove(entity);
    }

    public void RemoveRange(IEnumerable<TGenericDbObject> entity)
    {
        _dbSet.RemoveRange(entity);
    }

    public void RemoveById(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task RemoveByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}