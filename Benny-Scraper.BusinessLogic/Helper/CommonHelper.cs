using BennyScraper.Models;
using System.Globalization;

namespace BennyScraper.BusinessLogic.Helper;

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
        string directory = string.Empty;

        if (string.IsNullOrEmpty(tempFile))
        {
            return;
        }

        FileAttributes attr = File.GetAttributes(tempFile);

        if (!attr.HasFlag(FileAttributes.Directory))
        {
            directory = Path.GetDirectoryName(tempFile);
        }
        else
        {
            directory = tempFile;
        }

        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, true);
                Console.WriteLine($"Deleted temp folder {directory}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to delete temp folder {directory}. Reason: {ex.Message}");
                Console.ResetColor();
            }
        }
    }


    public static string GetOutputDirectoryForTitle(string title, string? outputDirectory = null)
    {
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            return Path.Combine(outputDirectory, CommonHelper.SanitizeFileName(title, true));
        }

        string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var novelFileSafeTitle = CommonHelper.SanitizeFileName(title, true);
        return Path.Combine(documentsFolder, "BennyScrapedNovels", novelFileSafeTitle);
    }

    /// <summary>
    /// Creates a temporary file in the user's temp directory
    /// </summary>
    /// <returns></returns>
    public static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    public static ICollection<Chapter> SortNovelChaptersByDateCreated(ICollection<Chapter> chapters) =>
        chapters.OrderBy(chapter => chapter.DateCreated).ToList();

    /// <summary>
    /// Draws a box around the provided messages with automatic width calculation.
    /// Useful for highlighting important information or warnings in the console.
    /// </summary>
    /// <param name="messages">Array of messages to display inside the box. Each element is a separate line.</param>
    /// <param name="color">The console color to use for the box and text.</param>
    /// <example>
    /// var messages = new[] { "Warning!", "", "This is important information." };
    /// CommonHelper.DrawBox(messages, ConsoleColor.Red);
    /// </example>
    public static void DrawBox(string[] messages, ConsoleColor color)
    {
        var maxLength = messages.Max(m => m.Length);
        var boxWidth = maxLength + 4; // 2 spaces padding on each side

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;

        // Top border
        Console.WriteLine("\n╔" + new string('═', boxWidth) + "╗");

        // Content lines
        foreach (var message in messages)
        {
            var paddedMessage = message.PadRight(maxLength);
            Console.WriteLine($"║  {paddedMessage}  ║");
        }

        // Bottom border
        Console.WriteLine("╚" + new string('═', boxWidth) + "╝\n");

        Console.ForegroundColor = originalColor;
    }
}