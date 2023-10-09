using Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;
using Benny_Scraper.Models;

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
            Console.ForegroundColor = ConsoleColor.Blue;
            if (configuration.SaveAsSingleFile)
                CreateSigleComicBookArchive(novel, chapterDataBuffers, outputDirectory);
            else
                CreateComicBookArchiveByChapter(novel, chapterDataBuffers, outputDirectory);
            

            Console.Write($"Total chapters: {novel.Chapters.Count()}\nTotal pages {totalPages}:\n\n files created at: {outputDirectory}\n");
            if (totalMissingChapters > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Theere were {totalMissingChapters} chapters with no pages");
                Console.WriteLine($"Missing chapter urls: {string.Join("\n", missingChapterUrls)}");
            }
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Adding PDFs to Calibre database");
            var result = EpubGenerator.ExecuteCommand($"calibredb add \"{outputDirectory}\" --series \"{novel.Title}\"");
            Logger.Info($"Command executed with code: {result}");
            Console.ResetColor();
            Logger.Info(new string('=', 50));
            Logger.Info($"Total chapters: {novel.Chapters.Count()}\nTotal pages {totalPages}:\n\n files created at: {outputDirectory}\n");
        }

        public void CreateSigleComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, string outputDirectory)
        {
            // create directory, then add each image to the folder and zip it up into a cbr
            Directory.CreateDirectory(outputDirectory);

            foreach (var chapter in chapterDataBuffer)
            {
                if (chapter.Pages == null)
                    continue;

                var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList(); // only Page from PageData has ImagePath as a member variable
                foreach (var imagePath in imagePaths)
                {
                }
            }
        }

        public void CreateComicBookArchiveByChapter(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, string pdfDirectoryPath)
        {
            throw new NotImplementedException();
        }
    }
}
