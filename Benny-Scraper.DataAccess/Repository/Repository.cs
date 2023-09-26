
using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Benny_Scraper.DataAccess.Repository
{
    public class Repository<GenericDbObject> : IRepository<GenericDbObject> where GenericDbObject : class
    {
        // adds the database context
        private readonly Database _db;
        internal DbSet<GenericDbObject> _dbSet;

        public Repository(Database db)
        {
            _db = db;
            // make it so we don't have to keep using _db.Set.Add() or other methods
            this._dbSet = _db.Set<GenericDbObject>(); // set the dbset to the db set of the generic object. This is how we can use the generic repository
        }

        public void Add(GenericDbObject entity)
        {
            _dbSet.Add(entity);
        }

        public void AddRange(IEnumerable<GenericDbObject> entities)
        {
            _dbSet.AddRange(entities);
        }

        public async Task AddAsync(GenericDbObject entity, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            await _dbSet.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<GenericDbObject> entities, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            await _dbSet.AddRangeAsync(entities);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">A non empty guid</param>
        /// <returns></returns>
        public GenericDbObject GetById(Guid id)
        {
            try
            {
                return _dbSet.Find(new object[] { id });
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">A non-empty guid</param>
        /// <returns></returns>
        public async Task<GenericDbObject> GetByIdAsync(Guid id, CancellationToken cancellation = default)
        {
            try
            {
                cancellation.ThrowIfCancellationRequested();
                return await _dbSet.FindAsync(new object[] { id }, cancellation);

            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filter">Ex: filter: obj => obj.IsActive,</param>
        /// <param name="orderBy">Ex: orderBy: o => o.OrderByDescending(obj => obj.DateCreated)</param>
        /// <param name="includedProperties"></param>
        /// <returns></returns>
        public IEnumerable<GenericDbObject> GetAll(Expression<Func<GenericDbObject, bool>>? filter = null,
            Func<IQueryable<GenericDbObject>, IOrderedQueryable<GenericDbObject>>? orderBy = null,
            string? includedProperties = null)
        {
            IQueryable<GenericDbObject> query = _dbSet;
            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (orderBy != null)
            {
                query = orderBy(query);
            }

            if (includedProperties != null)
            {
                // Will not break if there are commas seperating properties, including ,,,
                foreach (var includedProp in includedProperties.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
        public async Task<IEnumerable<GenericDbObject>> GetAllAsync(Expression<Func<GenericDbObject, bool>>? filter = null,
            Func<IQueryable<GenericDbObject>, IOrderedQueryable<GenericDbObject>>? orderBy = null,
            string? includeProperties = null,
            CancellationToken cancellationToken = default
            )
        {
            IQueryable<GenericDbObject> query = _dbSet;
            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (orderBy != null)
            {
                query = orderBy(query);
            }

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
        public GenericDbObject GetFirstOrDefault(Expression<Func<GenericDbObject, bool>> filter, string? includeProperties = null)
        {
            IQueryable<GenericDbObject> query = _dbSet;
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
        public Task<GenericDbObject> GetFirstOrDefaultAsync(Expression<Func<GenericDbObject, bool>> filter,
            string? includeProperties = null,
            CancellationToken cancellationToken = default)
        {
            IQueryable<GenericDbObject> query = _dbSet;
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

        public void Remove(GenericDbObject entity)
        {
            _dbSet.Remove(entity);
        }        

        public void RemoveRange(IEnumerable<GenericDbObject> entity)
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
}
