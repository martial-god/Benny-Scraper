using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Benny_Scraper.DataAccess.Repository.IRepository
{
    public interface IRepository<GenericDbObject> where GenericDbObject : class // Generic repository where we can pass in any object
    {
        GenericDbObject GetById(Guid id);

        Task<GenericDbObject> GetByIdAsync(Guid id, CancellationToken cancellation = default);

        IEnumerable<GenericDbObject> GetAll(
            Expression<Func<GenericDbObject, bool>>? filter = null, // filter is a lambda expression
            Func<IQueryable<GenericDbObject>, IOrderedQueryable<GenericDbObject>>? orderBy = null,
            string? includeProperties = null
        );

        /// <summary>
        /// Gets the first or default object from the database
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="orderBy"></param>
        /// <param name="includeProperties"></param>
        /// <param name="cancellationToken">Allows the caller to request cancellation of the operation</param>
        /// <returns></returns>
        Task<IEnumerable<GenericDbObject>> GetAllAsync(
            Expression<Func<GenericDbObject, bool>>? filter = null,
            Func<IQueryable<GenericDbObject>, IOrderedQueryable<GenericDbObject>>? orderBy = null,
            string? includeProperties = null,
            CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Example: GetFirstOrDefault(x => x.Id == cartId)
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="includeProperties"></param>
        /// <returns></returns>
        GenericDbObject GetFirstOrDefault(
            Expression<Func<GenericDbObject, bool>> filter,
            string? includeProperties = null
        );

        Task<GenericDbObject> GetFirstOrDefaultAsync(
            Expression<Func<GenericDbObject, bool>> filter,
            string? includeProperties = null,
            CancellationToken cancellationToken = default
        );
        
        /// <summary>
        /// Add to the database
        /// </summary>
        /// <param name="entity">Object to add to the database</param>
        void Add(GenericDbObject entity);
        void AddRange(IEnumerable<GenericDbObject> entities);
        Task AddAsync(GenericDbObject entity, CancellationToken cancellationToken = default);
        Task AddRangeAsync(IEnumerable<GenericDbObject> entities, CancellationToken cancellationToken = default);

        void RemoveById(Guid id);

        Task RemoveByIdAsync(Guid id, CancellationToken cancellationToken = default);
        void Remove(GenericDbObject entity);

        /// <summary>
        /// Removes multiple things
        /// </summary>
        /// <param name="entity"></param>
        void RemoveRange(IEnumerable<GenericDbObject> entity);
    }
}
