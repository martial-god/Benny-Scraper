using System.Globalization;
using System.IO.Compression;
using BennyScraper.BusinessLogic.FileGenerators.Interfaces;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.FileGenerators;

public class ComicBookArchiveGenerator : IComicBookArchiveGenerator
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
        ArgumentNullException.ThrowIfNull(novel);
        ArgumentNullException.ThrowIfNull(chapterDataBuffers);
        ArgumentNullException.ThrowIfNull(configuration);

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

    /// <summary>
    /// Updates an existing comic book archive with new pages from the novel without needing to re-create the entire archive, if the archive doesn't exist, it will create a new one.
    /// </summary>
    /// <param name="novel">The novel whose existing archive is being updated.</param>
    /// <param name="chapterDataBuffers">The new chapter data, including page image paths, to add to the archive.</param>
    /// <param name="outputDirectory">The directory used to create a new archive if one does not already exist.</param>
    /// <param name="configuration">The configuration used to determine the archive file extension when a new archive must be created.</param>
    /// <returns>Location where the archive was saved.</returns>
    public string UpdateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(novel);
        ArgumentNullException.ThrowIfNull(chapterDataBuffers);
        ArgumentNullException.ThrowIfNull(configuration);

        var comicBookArchivePath = novel.SaveLocation;

        if (string.IsNullOrEmpty(comicBookArchivePath) || !File.Exists(comicBookArchivePath))
        {
            return CreateComicBookArchive(novel, chapterDataBuffers, outputDirectory, configuration); // If the path is null or the file doesn't exist, simply create a new one
        }

        using (FileStream zipFileStream = new FileStream(comicBookArchivePath, FileMode.Open)) // this will directly modify the existing archive without needing to create a temp
        {
            using (ZipArchive archive = new ZipArchive(zipFileStream, ZipArchiveMode.Update))
            {
                foreach (var chapter in chapterDataBuffers)
                {
                    if (chapter.Pages == null)
                    {
                        continue;
                    }

                    var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList();

                    for (int i = 0; i < imagePaths.Count; i++)
                    {
                        var imageName = $"Chapter_{chapter.Number}_Page{(i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(chapter.Pages.Count.ToString(CultureInfo.InvariantCulture).Length, '0')}.{Path.GetExtension(imagePaths[i]).TrimStart('.')}";

                        // Delete existing image if it's already in the archive
                        var existingEntry = archive.GetEntry(imageName);
                        existingEntry?.Delete();

                        // Add new image
                        var entry = archive.CreateEntry(imageName);
                        using (var entryStream = entry.Open())
                        using (var fileStream = File.OpenRead(imagePaths[i]))
                        {
                            fileStream.CopyTo(entryStream);
                        }

                        File.Delete(imagePaths[i]);
                    }
                }
            }
        }

        return comicBookArchivePath;
    }

    private static string CreateSingleComicBookArchive(
        Novel novel,
        IEnumerable<ChapterDataBuffer> chapterDataBuffer,
        string outputDirectory,
        FileExtension fileExtension,
        string filenameSuffix = "")
    {
        Directory.CreateDirectory(outputDirectory);
        var tempDirectory = CommonHelper.CreateTempDirectory();
        string baseFilename = novel.Title;
        string filename = string.IsNullOrEmpty(filenameSuffix) ? baseFilename : $"{baseFilename} - {filenameSuffix}";
        var sanitzedTitle = CommonHelper.SanitizeFileName(filename);

        var chapterDataBuffers = chapterDataBuffer.ToList();
        var maxPages = chapterDataBuffers.Max(chapter => chapter.Pages?.Count ?? 0);
        var padLength = maxPages.ToString(CultureInfo.InvariantCulture).Length;

        foreach (var chapter in chapterDataBuffers)
        {
            var chapterDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory, $"Chapter_{chapter.Number}"));
            if (chapter.Pages == null)
            {
                continue;
            }

            var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList();
            for (int i = 0; i < imagePaths.Count; i++)
            {
                var imageName = $"Chapter_{chapter.Number}_Page{(i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(padLength, '0')}.{Path.GetExtension(imagePaths[i]).TrimStart('.')}";
                using (var fileStream = File.OpenRead(imagePaths[i]))
                using (var destinationStream = File.Create(Path.Combine(chapterDirectory.FullName, imageName)))
                {
                    fileStream.CopyTo(destinationStream);
                }

                File.Delete(imagePaths[i]);
            }
        }

        var outputFilePath = Path.Combine(outputDirectory, $"{sanitzedTitle}.{Enum.GetName(fileExtension)?.ToLowerInvariant()}");
        try
        {
            File.Delete(Path.Combine(outputDirectory, outputFilePath));
            ZipFile.CreateFromDirectory(tempDirectory, outputFilePath); // does not allow for duplicates files or an IO exception will be thrown
            CommonHelper.DeleteTempFolder(chapterDataBuffers.First().TempDirectory);
            CommonHelper.DeleteTempFolder(tempDirectory);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating comic book archive for {NovelTitle}", novel.Title);
        }

        return outputFilePath;
    }
}