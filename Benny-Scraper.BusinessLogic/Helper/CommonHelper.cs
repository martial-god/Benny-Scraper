using System.Globalization;
using BennyScraper.BusinessLogic.Extensions;
using BennyScraper.Models;
using ImageMagick;

namespace BennyScraper.BusinessLogic.Helper;

internal static class CommonHelper
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
        return new string(fileName.Where(ch => !invalidChars.Contains(ch)).ToArray()).ToTitleCase();
    }

    public static void DeleteTempFolder(string tempFile)
    {
        if (string.IsNullOrEmpty(tempFile))
        {
            return;
        }

        try
        {
            string? directory = Directory.Exists(tempFile)
                ? tempFile
                : File.Exists(tempFile) ? Path.GetDirectoryName(tempFile) : null;
            if (directory == null)
            {
                return;
            }

            Directory.Delete(directory, true);
            Console.WriteLine($"Deleted temp folder {directory}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to delete temp folder {tempFile}. Reason: {ex.Message}");
            Console.ResetColor();
        }
    }

    public static void DeleteTemporaryFile(string temporaryFilePath)
    {
        try
        {
            File.Delete(temporaryFilePath);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to delete temporary file {temporaryFilePath}. Reason: {exception.Message}");
        }
    }

    public static async Task ConvertImageToWebpAsync(
        string sourcePath,
        string outputPath,
        bool lossless = true,
        CancellationToken cancellationToken = default)
    {
        const int MaximumImageDimension = 16383;
        const long MaximumImagePixelCount = 40_000_000;

        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        cancellationToken.ThrowIfCancellationRequested();

        string sourceImagePath = Path.GetFullPath(sourcePath);
        string outputImagePath = Path.GetFullPath(outputPath);

        if (!string.Equals(Path.GetExtension(outputImagePath), ".webp", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The output image must use the .webp extension.", nameof(outputPath));
        }

        if (File.Exists(outputImagePath))
        {
            throw new IOException($"The output image already exists: {outputImagePath}");
        }

        using var sourceImageStream = File.OpenRead(sourceImagePath);
        var imageReadSettings = RasterImageReader.CreateReadSettings(sourceImageStream, 2);
        if (imageReadSettings.Format == MagickFormat.Png)
        {
            RasterImageReader.RejectAnimatedPng(sourceImageStream);
        }

        using var sourceImageInformation = new MagickImageCollection();
        await sourceImageInformation.PingAsync(sourceImageStream, imageReadSettings, cancellationToken).ConfigureAwait(false);

        if (sourceImageInformation.Count != 1)
        {
            throw new NotSupportedException("Only single-frame images can be converted.");
        }

        if (sourceImageInformation[0].Width > MaximumImageDimension || sourceImageInformation[0].Height > MaximumImageDimension)
        {
            throw new NotSupportedException($"WebP images cannot exceed {MaximumImageDimension} pixels in either dimension.");
        }

        if ((long)sourceImageInformation[0].Width * sourceImageInformation[0].Height > MaximumImagePixelCount)
        {
            throw new NotSupportedException($"Image conversion is limited to {MaximumImagePixelCount} pixels.");
        }

        sourceImageStream.Position = 0;
        using var sourceImages = new MagickImageCollection();
        await sourceImages.ReadAsync(sourceImageStream, imageReadSettings, cancellationToken).ConfigureAwait(false);
        if (sourceImages.Count != 1)
        {
            throw new NotSupportedException("Only single-frame images can be converted.");
        }

        var sourceImage = sourceImages[0];
        sourceImage.AutoOrient();
        sourceImage.Quality = lossless ? 100U : 88U;
        sourceImage.Settings.SetDefine(MagickFormat.WebP, "lossless", lossless);
        sourceImage.Settings.SetDefine(MagickFormat.WebP, "exact", true);

        string temporaryOutputImagePath = $"{outputImagePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await sourceImage.WriteAsync(temporaryOutputImagePath, MagickFormat.WebP, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryOutputImagePath, outputImagePath, overwrite: false);
        }
        finally
        {
            DeleteTemporaryFile(temporaryOutputImagePath);
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
    /// Creates a temporary file in the user's temp directory.
    /// </summary>
    /// <returns>The full path of the newly created temporary directory.</returns>
    public static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    public static ICollection<Chapter> SortNovelChaptersByNumber(ICollection<Chapter> chapters) =>
        chapters
            .OrderBy(chapter => chapter.Number)
            .ThenBy(chapter => chapter.DateCreated)
            .ToList();

    /// <summary>
    /// Draws a box around the provided messages with automatic width calculation.
    /// Useful for highlighting important information or warnings in the console.
    /// </summary>
    /// <param name="messages">Array of messages to display inside the box. Each element is a separate line.</param>
    /// <param name="color">The console color to use for the box and text.</param>
    /// <example>
    /// var messages = new[] { "Warning!", "", "This is important information." };
    /// CommonHelper.DrawBox(messages, ConsoleColor.Red);.
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