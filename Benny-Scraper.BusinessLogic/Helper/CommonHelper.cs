using System.Globalization;
using System.Text.RegularExpressions;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.Helper
{
    public static class CommonHelper
    {
        /// <summary>
        /// Removes invalid characters from a file name and optionally capitalizes the first letter of each word.
        /// </summary>
        /// <param name="fileName">The input name to be processed.</param>
        /// <param name="capitalize">Whether to capitalize the first letter of each word. Default is false.</param>
        /// <param name="culture">The culture to be used for text transformation if capitalizing. Default is the current culture.</param>
        /// <returns>A file-safe name that is title-cased.</returns>
        public static string SanitizeFileName(string fileName, bool capitalize = false, CultureInfo? culture = null)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new string(fileName.Where(ch => !invalidChars.Contains(ch)).ToArray());

            if (capitalize)
            {
                culture ??= CultureInfo.CurrentCulture;
                TextInfo textInfo = culture.TextInfo;
                sanitized = textInfo.ToTitleCase(sanitized.ToLowerInvariant());
            }

            return sanitized;
        }

        public static void DeleteTempFolder(string tempFile)
        {
            if (string.IsNullOrEmpty(tempFile))
                return;
            string directory = Path.GetDirectoryName(tempFile);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, false);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Deleted temp folder {directory}");
                Console.ResetColor();
            }
        }

        public static string GetOutputDirectoryForTitle(string title, string? outputDirectory = null)
        {
            if (!string.IsNullOrEmpty(outputDirectory))
                return Path.Combine(outputDirectory, CommonHelper.SanitizeFileName(title, true));
            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var novelFileSafeTitle = CommonHelper.SanitizeFileName(title, true);
            return Path.Combine(documentsFolder, "BennyScrapedNovels", novelFileSafeTitle);
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
        }
    }

    public static class FileExtensionExtensions
    {
        public static string ToFileString(this FileExtension fileExtension)
        {
            switch (fileExtension)
            {
                case FileExtension.PDF:
                    return ".pdf";
                case FileExtension.CBZ:
                    return ".cbz";
                case FileExtension.CBR:
                    return ".cbr";
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileExtension), fileExtension, null);
            }
        }
    }
}
