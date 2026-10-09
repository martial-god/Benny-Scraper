using System.Globalization;
using System.IO.Compression;
using BennyScraper.BusinessLogic.FileGenerators.Interfaces;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.FileGenerators;

internal sealed class ComicBookArchiveGenerator : IComicBookArchiveGenerator
{
    private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Creates a single comic book archive, file extension is determined by the configuration.DefaultMangaFileExtension value.
    /// </summary>
    /// <param name="novel">The novel whose chapters are being archived.</param>
    /// <param name="chapterDataBuffers">The chapter data, including page image paths, to include in the archive.</param>
    /// <param name="outputDirectory">The directory where the generated archive will be saved.</param>
    /// <param name="configuration">The configuration used to determine the archive file extension.</param>
    /// <param name="filenameSuffix">An optional suffix appended to the generated archive's filename.</param>
    /// <returns>Location where the archive was saved.</returns>
    public string CreateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration, string filenameSuffix = "")
    {
        int? totalPages = novel.Chapters.Where(chapter => chapter.Pages != null).Sum(chapter => chapter.Pages?.Count);
        int totalMissingChapters = novel.Chapters.Count(chapter => chapter.Pages == null || chapter.Pages.Count == 0);
        var missingChapterUrls = novel.Chapters.Where(chapter => chapter.Pages == null).Select(chapter => chapter.Url);

        _logger.Info(new string('=', 50));
        var comicBookArchiveSaveLocation = CreateSingleComicBookArchive(novel, chapterDataBuffers, outputDirectory, configuration.DefaultMangaFileExtension, filenameSuffix);

        // Display completion summary in a formatted box
        Console.WriteLine();
        var fileTypeName = Enum.GetName(configuration.DefaultMangaFileExtension)?.ToUpper(CultureInfo.InvariantCulture) ?? "COMIC BOOK ARCHIVE";
        var messages = new[] { $"{fileTypeName} GENERATION COMPLETE!" };
        CommonHelper.DrawBox(messages, ConsoleColor.Green);
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  Novel:          {novel.Title}");
        Console.WriteLine($"  Novel ID:       {novel.Id}");
        if (novel.ChapterRanges.Count != 0)
        {
            var ranges = novel.ChapterRanges.OrderBy(r => r.Begin).Select(r => $"{r.Begin}-{r.End}").ToList();
            var totalInRanges = novel.ChapterRanges.Sum(r => r.End - r.Begin + 1);
            Console.WriteLine($"  Chapter Ranges: {string.Join(", ", ranges)} ({totalInRanges} chapters)");
        }

        Console.WriteLine($"  Total Chapters: {novel.Chapters.Count}");
        Console.WriteLine($"  Total Pages:    {totalPages}");
        Console.WriteLine($"  Saved to:       {outputDirectory}");
        Console.ResetColor();

        if (totalMissingChapters > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ⚠ Warning: {totalMissingChapters} chapters had no pages");
            Console.WriteLine($"  Missing URLs: {string.Join(", ", missingChapterUrls)}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.WriteLine(new string('─', 78));
        Console.WriteLine();

        // Try to add to Calibre
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Adding to Calibre database...");
        Console.ResetColor();
        var result = CommandExecutor.ExecuteCommand($"calibredb add \"{outputDirectory}\" --series \"{novel.Title}\"");
        _logger.Debug($"Calibre command executed with code: {result}");

        if (result == "0")
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✓ Successfully added to Calibre");
            Console.ResetColor();
        }

        Console.WriteLine();
        _logger.Debug($"{fileTypeName} generation complete - Novel: {novel.Title}, Chapters: {novel.Chapters.Count}, Pages: {totalPages}, Location: {outputDirectory}");
        return comicBookArchiveSaveLocation;
    }

    public string UpdateComicBookArchive(
        Novel novel,
        IEnumerable<ChapterDataBuffer> chapterDataBuffers,
        string outputDirectory,
        Configuration configuration)
    {
        string? existingArchivePath = novel.SaveLocation;
        if (string.IsNullOrWhiteSpace(existingArchivePath) || !File.Exists(existingArchivePath))
        {
            throw new FileNotFoundException(
                "The existing comic archive is missing. Restore it or recreate the complete book before updating.",
                existingArchivePath);
        }

        var replacementChapters = chapterDataBuffers.ToArray();
        if (!replacementChapters.Any(chapter => chapter.Pages is { Count: > 0 }))
        {
            return existingArchivePath;
        }

        try
        {
            using var existingArchive = ZipFile.OpenRead(existingArchivePath);
        }
        catch (InvalidDataException exception)
        {
            throw new NotSupportedException(
                "Only ZIP-based comic archives can be updated. Convert genuine RAR, 7z, TAR or ACE archives to CBZ first.",
                exception);
        }

        string outputArchivePath = string.Equals(Path.GetExtension(existingArchivePath), ".cbz", StringComparison.OrdinalIgnoreCase)
            ? existingArchivePath
            : Path.ChangeExtension(existingArchivePath, ".cbz");
        ComicArchiveWriter.WriteArchive(outputArchivePath, replacementChapters, existingArchivePath);
        novel.FileType = NovelFileType.Cbz;
        return outputArchivePath;
    }

    private static string CreateSingleComicBookArchive(
        Novel novel,
        IEnumerable<ChapterDataBuffer> chapterDataBuffers,
        string outputDirectory,
        FileExtension fileExtension,
        string filenameSuffix = "")
    {
        if (fileExtension != FileExtension.Cbz)
        {
            throw new NotSupportedException(
                "Comic archive creation supports CBZ only. Select CBZ instead of writing ZIP data with another format's extension.");
        }

        string filename = string.IsNullOrEmpty(filenameSuffix) ? novel.Title : $"{novel.Title} - {filenameSuffix}";
        string sanitizedTitle = CommonHelper.SanitizeFileName(filename);
        string outputArchivePath = Path.Combine(outputDirectory, $"{sanitizedTitle}.cbz");
        ComicArchiveWriter.WriteArchive(outputArchivePath, chapterDataBuffers);
        return outputArchivePath;
    }
}