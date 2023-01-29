using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benny_Scraper.Models
{
    public static class MyExtensions
    {
        /// <summary>
        /// Extension method for ICollection to add a range of items. Make
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="items"></param>
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            if (collection == null || items == null)
                return;

            foreach (var item in items)
            {
                collection.Add(item);
            }
            // Add items in parallel for large sets, does not add in order though
            //Parallel.ForEach(items, item =>
            //{
            //    collection.Add(item);
            //});
        }
    }

}
