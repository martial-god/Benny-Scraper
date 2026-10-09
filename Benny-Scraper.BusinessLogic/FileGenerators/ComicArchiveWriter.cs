using System.Globalization;
using System.IO.Compression;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.FileGenerators;

internal static class ComicArchiveWriter
{
    public static void WriteArchive(
        string outputArchivePath,
        IEnumerable<ChapterDataBuffer> chapterDataBuffers,
        string? existingArchivePath = null)
    {
        var replacementChapters = chapterDataBuffers.Where(chapter => chapter.Pages is { Count: > 0 }).ToArray();
        if (replacementChapters.Length == 0)
        {
            throw new InvalidOperationException("No chapter images were supplied. The existing archive was not changed.");
        }

        if (replacementChapters.Any(chapter => chapter.IsPartial))
        {
            throw new InvalidOperationException("Incomplete chapters cannot replace existing archive content.");
        }

        var replacementChapterNumbers = replacementChapters
            .Select(chapter => chapter.Number.ToString(CultureInfo.InvariantCulture))
            .ToHashSet(StringComparer.Ordinal);
        if (replacementChapterNumbers.Count != replacementChapters.Length)
        {
            throw new InvalidOperationException("Chapter titles produce duplicate archive numbers. The archive was not changed.");
        }

        string outputFilePath = Path.GetFullPath(outputArchivePath);
        string temporaryArchivePath = $"{outputFilePath}.{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

        try
        {
            using (var outputFileStream = new FileStream(temporaryArchivePath, FileMode.CreateNew, FileAccess.Write))
            using (var outputArchive = new ZipArchive(outputFileStream, ZipArchiveMode.Create))
            {
                var writtenEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (existingArchivePath != null)
                {
                    using var existingArchive = ZipFile.OpenRead(existingArchivePath);
                    foreach (var existingEntry in existingArchive.Entries)
                    {
                        string existingEntryName = existingEntry.FullName.Replace('\\', '/');
                        bool belongsToReplacementChapter = replacementChapterNumbers.Any(chapterNumber =>
                            existingEntryName.StartsWith($"Chapter_{chapterNumber}/", StringComparison.Ordinal) ||
                            existingEntryName.StartsWith($"Chapter_{chapterNumber}_Page", StringComparison.Ordinal));
                        if (belongsToReplacementChapter)
                        {
                            continue;
                        }

                        ValidateEntryName(existingEntryName, writtenEntryNames);
                        var copiedEntry = outputArchive.CreateEntry(existingEntryName, CompressionLevel.NoCompression);
                        copiedEntry.LastWriteTime = existingEntry.LastWriteTime;
                        using var sourceEntryStream = existingEntry.Open();
                        using var destinationEntryStream = copiedEntry.Open();
                        sourceEntryStream.CopyTo(destinationEntryStream);
                    }
                }

                foreach (var replacementChapter in replacementChapters.OrderBy(chapter => chapter.Number))
                {
                    string chapterNumber = replacementChapter.Number.ToString(CultureInfo.InvariantCulture);
                    var chapterPages = replacementChapter.Pages!.ToArray();
                    int pageNumberPadding = chapterPages.Length.ToString(CultureInfo.InvariantCulture).Length;
                    for (int pageIndex = 0; pageIndex < chapterPages.Length; pageIndex++)
                    {
                        string sourceImagePath = chapterPages[pageIndex].ImagePath;
                        using var sourceImageStream = File.OpenRead(sourceImagePath);
                        var imageReadSettings = RasterImageReader.CreateReadSettings(sourceImageStream);
                        string imageExtension = RasterImageReader.GetFileExtension(imageReadSettings.Format);
                        string pageNumber = (pageIndex + 1).ToString(CultureInfo.InvariantCulture).PadLeft(pageNumberPadding, '0');
                        string imageEntryName = $"Chapter_{chapterNumber}/Chapter_{chapterNumber}_Page{pageNumber}.{imageExtension}";
                        ValidateEntryName(imageEntryName, writtenEntryNames);
                        var imageEntry = outputArchive.CreateEntry(imageEntryName, CompressionLevel.NoCompression);
                        using var destinationImageStream = imageEntry.Open();
                        sourceImageStream.CopyTo(destinationImageStream);
                    }
                }
            }

            using (var completedArchive = ZipFile.OpenRead(temporaryArchivePath))
            {
                foreach (var completedEntry in completedArchive.Entries)
                {
                    using var completedEntryStream = completedEntry.Open();
                    completedEntryStream.CopyTo(Stream.Null);
                }
            }

            bool replacesExistingArchive = existingArchivePath != null &&
                string.Equals(
                    Path.GetFullPath(existingArchivePath),
                    outputFilePath,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            if (File.Exists(outputFilePath) && (existingArchivePath == null || replacesExistingArchive))
            {
                File.Replace(temporaryArchivePath, outputFilePath, null);
            }
            else
            {
                File.Move(temporaryArchivePath, outputFilePath, overwrite: false);
            }
        }
        finally
        {
            CommonHelper.DeleteTemporaryFile(temporaryArchivePath);
        }
    }

    private static void ValidateEntryName(string entryName, HashSet<string> writtenEntryNames)
    {
        if (entryName.StartsWith('/') || entryName.Contains(':', StringComparison.Ordinal) ||
            entryName.Contains('\0', StringComparison.Ordinal) ||
            entryName.Split('/').Contains("..", StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Unsafe archive entry: {entryName}");
        }

        if (!writtenEntryNames.Add(entryName))
        {
            throw new InvalidDataException($"Duplicate archive entry: {entryName}. Repair the affected chapter before updating.");
        }
    }
}