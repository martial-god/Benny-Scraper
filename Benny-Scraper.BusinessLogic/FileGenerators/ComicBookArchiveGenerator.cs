using System.IO.Compression;
using Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;
using Benny_Scraper.BusinessLogic.Helper;
using Benny_Scraper.Models;
using PdfSharp.Drawing;

namespace Benny_Scraper.BusinessLogic.FileGenerators
{
    public class ComicBookArchiveGenerator : IComicBookArchiveGenerator
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Creates a single comic book archive, file extension is determined by the configuration.DefaultMangaFileExtension value
        /// </summary>
        /// <param name="novel"></param>
        /// <param name="chapterDataBuffers"></param>
        /// <param name="outputDirectory"></param>
        /// <param name="configuration"></param>
        /// <returns>Location where the archive was saved</returns>
        public string CreateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration)
        {
            string comicbookArchiveSaveLocation = string.Empty;
            int? totalPages = novel.Chapters.Where(chapter => chapter.Pages != null).SelectMany(chapter => chapter.Pages).Count();
            int totalMissingChapters = novel.Chapters.Count(chapter => chapter.Pages == null || !chapter.Pages.Any());
            var missingChapterUrls = novel.Chapters.Where(chapter => chapter.Pages == null).Select(chapter => chapter.Url);

            Logger.Info(new string('=', 50));
            comicbookArchiveSaveLocation = CreateSigleComicBookArchive(novel, chapterDataBuffers, outputDirectory, configuration.DefaultMangaFileExtension);

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write($"Total chapters: {novel.Chapters.Count}\nTotal pages {totalPages}:\n\n files created at: {outputDirectory}\n");
            if (totalMissingChapters > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Theere were {totalMissingChapters} chapters with no pages");
                Console.WriteLine($"Missing chapter urls: {string.Join("\n", missingChapterUrls)}");
            }
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Adding {Enum.GetName(configuration.DefaultMangaFileExtension)} to Calibre database");
            var result = EpubGenerator.ExecuteCommand($"calibredb add \"{outputDirectory}\" --series \"{novel.Title}\"");
            Logger.Info($"Command executed with code: {result}");
            Console.ResetColor();
            Logger.Info(new string('=', 50));
            Logger.Info($"Total chapters: {novel.Chapters.Count}\nTotal pages {totalPages}:\n\n files created at: {outputDirectory}\n");
            return comicbookArchiveSaveLocation;
        }

        /// <summary>
        /// Updates an existing comic book archive with new pages from the novel without needing to re-create the entire archive, if the archive doesn't exist, it will create a new one
        /// </summary>
        /// <param name="novel"></param>
        /// <param name="chapterDataBuffers"></param>
        /// <param name="outputDirectory"></param>
        /// <param name="configuration"></param>
        /// <returns>Location where the archive was saved</returns>
        public string UpdateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration)
        {
            var comicBookArchivePath = novel.SaveLocation;

            if (string.IsNullOrEmpty(comicBookArchivePath) || !File.Exists(comicBookArchivePath))
                return CreateComicBookArchive(novel, chapterDataBuffers, outputDirectory, configuration); // If the path is null or the file doesn't exist, simply create a new one

            using (FileStream zipFileStream = new FileStream(comicBookArchivePath, FileMode.Open)) // this will directly modify the existing archive without needing to create a temp
            {
                using (ZipArchive archive = new ZipArchive(zipFileStream, ZipArchiveMode.Update))
                {
                    foreach (var chapter in chapterDataBuffers)
                    {
                        if (chapter.Pages == null)
                            continue;

                        var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList();

                        for (int i = 0; i < imagePaths.Count; i++)
                        {
                            var imageName = $"Chapter_{chapter.Number}_Page{(i + 1).ToString().PadLeft(chapter.Pages.Count.ToString().Length, '0')}.{Path.GetExtension(imagePaths[i])}";

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


        private static string CreateSigleComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, string outputDirectory, FileExtension fileExtension)
        {
            Directory.CreateDirectory(outputDirectory);
            var tempDirectory = CommonHelper.CreateTempDirectory();
            var sanitzedTitle = CommonHelper.SanitizeFileName(novel.Title);

            var maxPages = chapterDataBuffer.Max(chapter => chapter.Pages?.Count ?? 0);
            var padLength = maxPages.ToString().Length;

            foreach (var chapter in chapterDataBuffer)
            {
                var chapterDirectory = Directory.CreateDirectory(Path.Combine(tempDirectory, $"Chapter_{chapter.Number}"));
                if (chapter.Pages == null)
                    continue;

                var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList();
                for (int i = 0; i < imagePaths.Count; i++)
                {
                    var imageName = $"Chapter_{chapter.Number}_Page{((i + 1).ToString().PadLeft(padLength, '0'))}.{Path.GetExtension(imagePaths[i])}";
                    using (var fileStream = File.OpenRead(imagePaths[i]))
                    {
                        var destinationStream = File.Create(Path.Combine(chapterDirectory.FullName, imageName));
                        fileStream.CopyTo(destinationStream);
                        destinationStream.Close();
                    }
                    File.Delete(imagePaths[i]);
                }
            }

            var outputFilePath = Path.Combine(outputDirectory, $"{sanitzedTitle}.{Enum.GetName(fileExtension)?.ToLowerInvariant()}");
            try
            {
                File.Delete(Path.Combine(outputDirectory, outputFilePath));
                ZipFile.CreateFromDirectory(tempDirectory, outputFilePath); // does not allow for duplicates files or an IO exception will be thrown
                CommonHelper.DeleteTempFolder(chapterDataBuffer.First().TempDirectory);
                CommonHelper.DeleteTempFolder(tempDirectory);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, $"Error creating comic book archive for {novel.Title}");
            }
            return outputFilePath;
        }
    }
}
