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
}
