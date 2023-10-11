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

        public void CreateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration)
        {
            int? totalPages = novel.Chapters.Where(chapter => chapter.Pages != null).SelectMany(chapter => chapter.Pages).Count();
            int totalMissingChapters = novel.Chapters.Count(chapter => chapter.Pages == null || !chapter.Pages.Any());
            var missingChapterUrls = novel.Chapters.Where(chapter => chapter.Pages == null).Select(chapter => chapter.Url);

            Logger.Info(new string('=', 50));
            CreateSigleComicBookArchive(novel, chapterDataBuffers, outputDirectory, configuration.DefaultMangaFileExtension);

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
        }

        private static void CreateSigleComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, string outputDirectory, FileExtension fileExtension)
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

            var outputFilePath = Path.Combine(outputDirectory, $"{sanitzedTitle}.{Enum.GetName(fileExtension)}");
            try
            {
                File.Delete(Path.Combine(outputDirectory, outputFilePath));
                ZipFile.CreateFromDirectory(tempDirectory, outputFilePath); // does not allow for duplicates files or an IO exception will be thrown
                CommonHelper.DeleteTempFolder(chapterDataBuffer.First().Pages.First().ImagePath);
                CommonHelper.DeleteTempFolder(tempDirectory);

            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message, $"Error creating comic book archive for {novel.Title}");
            }
            
        }
    }
}
