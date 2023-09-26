using System.Globalization;
using System.Text.RegularExpressions;

namespace Benny_Scraper.BusinessLogic.Helper
{
    public static class CommonHelper
    {
        /// <summary>
        /// Gets a file-safe name by removing invalid characters and capitalizing the first letter of each word.
        /// </summary>
        /// <param name="name">The input name to be processed.</param>
        /// <param name="culture">The culture to be used for text transformation. Default is the current culture. example: new CultureInfo("en-US", false)</param>
        /// <returns>A file-safe name with valid characters and capitalized words.</returns>
        public static string GetFileSafeName(string name, CultureInfo culture = null)
        {

            culture ??= CultureInfo.CurrentCulture; // If culture is null, use the current culture
            string fileRegex = @"[^a-zA-Z0-9-\s]";
            TextInfo textInfo = culture.TextInfo;
            return textInfo.ToTitleCase(Regex.Replace(name, fileRegex, string.Empty).ToLower().ToLowerInvariant());
        }
    }

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
