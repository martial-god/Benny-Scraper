using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Benny_Scraper.DataAccess.Repository.IRepository
{
    public interface IRepository<GenericDbObjeect> where GenericDbObjeect : class // Generic repository where we can pass in any object
    {
        GenericDbObjeect Get(Guid id);
        
        IEnumerable<GenericDbObjeect> GetAll(
            Expression<Func<GenericDbObjeect, bool>>? filter = null, // filter is a lambda expression
            Func<IQueryable<GenericDbObjeect>, IOrderedQueryable<GenericDbObjeect>>? orderBy = null,
            string? includeProperties = null
        );
        GenericDbObjeect GetFirstOrDefault(
            Expression<Func<GenericDbObjeect, bool>> filter,
            string? includeProperties = null
        );
        /// <summary>
        /// Add to the database
        /// </summary>
        /// <param name="entity">Object to add to the database</param>
        void Add(GenericDbObjeect entity);
        void RemoveById(Guid id);
        void Remove(GenericDbObjeect entity);
        /// <summary>
        /// Removes multiple things
        /// </summary>
        /// <param name="entity"></param>
        void RemoveRange(IEnumerable<GenericDbObjeect> entity);
    }
}
