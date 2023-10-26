using Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;
using Benny_Scraper.Models;
using NLog;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf;
using Benny_Scraper.BusinessLogic.Helper;

namespace Benny_Scraper.BusinessLogic.FileGenerators
{
    public class PdfGenerator
    {
        private static readonly NLog.ILogger Logger = LogManager.GetCurrentClassLogger();
        public const string PdfFileExtension = ".pdf";

        public (string, bool) CreatePdf(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration)
        {
            string pdfSaveLocation = string.Empty;
            bool isPdfSplit = false;
            Logger.Info("Creating PDFs for {0}", novel.Title);
            int? totalPages = novel.Chapters.Where(chapter => chapter.Pages != null).SelectMany(chapter => chapter.Pages).Count();
            int totalMissingChapters = novel.Chapters.Count(chapter => chapter.Pages == null || !chapter.Pages.Any());
            var missingChapterUrls = novel.Chapters.Where(chapter => chapter.Pages == null).Select(chapter => chapter.Url);

            Logger.Info(new string('=', 50));
            Console.ForegroundColor = ConsoleColor.Blue;
            if (configuration.SaveAsSingleFile)
            {
                pdfSaveLocation = CreateSinglePdf(novel, chapterDataBuffers, outputDirectory);
            }
            else
            {
                pdfSaveLocation = CreatePdfByChapter(novel, chapterDataBuffers, outputDirectory);
                isPdfSplit = true;
            }

            Console.Write($"Total chapters: {novel.Chapters.Count}\nTotal pages {totalPages}:\n\nPDF files created at: {outputDirectory}\n");
            if (totalMissingChapters > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"There were {totalMissingChapters} chapters with no pages");
                Console.WriteLine($"Missing chapter urls: {string.Join("\n", missingChapterUrls)}");
            }
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Adding PDFs to Calibre database");
            var result = EpubGenerator.ExecuteCommand($"calibredb add \"{outputDirectory}\" --series \"{novel.Title}\"");
            Logger.Info($"Command executed with code: {result}");
            Console.ResetColor();
            Logger.Info(new string('=', 50));
            Logger.Info($"Total chapters: {novel.Chapters.Count}\nTotal pages {totalPages}:\n\nPDF files created at: {outputDirectory}\n");
            return (pdfSaveLocation, isPdfSplit);
        }

        public string CreatePdfByChapter(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, string pdfDirectoryPath)
        {
            Directory.CreateDirectory(pdfDirectoryPath);

            foreach (var chapter in chapterDataBuffer)
            {
                if (chapter.Pages == null)
                    continue;

                var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList(); // only Page from PageData has ImagePath as a member variable
                Console.WriteLine($"Total images in chapter {chapter.Title}: {imagePaths.Count}");

                PdfDocument document = new PdfDocument();

                document.Info.Title = $"{novel.Title} - {chapter.Title}";
                document.Info.Author = !string.IsNullOrEmpty(novel.Author) ? novel.Author : null;
                document.Info.Subject = novel.Genre;
                document.Info.Keywords = novel.Genre;
                document.Info.CreationDate = DateTime.Now;

                foreach (var imagePath in imagePaths)
                {
                    XImage img = XImage.FromFile(imagePath);

                    PdfPage page = document.AddPage();
                    page.Width = XUnit.FromPoint(img.PixelWidth);
                    page.Height = XUnit.FromPoint(img.PixelHeight);
                    XGraphics gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                }
                // to avoid the System.NotSupportedException: No data is available for encoding 1252. we have to install the Nugget package System.Text.Encoding.CodePages
                //https://stackoverflow.com/questions/50858209/system-notsupportedexception-no-data-is-available-for-encoding-1252

                var sanitizedTitle = CommonHelper.SanitizeFileName($"{novel.Title} - {chapter.Title}", true);
                var pdfFilePath = Path.Combine(pdfDirectoryPath, sanitizedTitle + PdfFileExtension);
                document.Save(pdfFilePath);
            }
            return pdfDirectoryPath;
        }

        public string CreateSinglePdf(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, string pdfDirectoryPath)
        {
            Directory.CreateDirectory(pdfDirectoryPath);

            PdfDocument document = new PdfDocument();

            document.Info.Title = $"{novel.Title}";
            document.Info.Author = !string.IsNullOrEmpty(novel.Author) ? novel.Author : null;
            document.Info.Subject = novel.Genre;
            document.Info.Keywords = novel.Genre;
            document.Info.CreationDate = DateTime.Now;

            foreach (var chapter in chapterDataBuffer)
            {
                if (chapter.Pages == null)
                    continue;

                var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList(); // only Page from PageData has ImagePath as a member variable
                Console.WriteLine($"Total images in chapter {chapter.Title}: {imagePaths.Count}");

                foreach (var imagePath in imagePaths)
                {
                    XImage img;
                    using (var imageStream = File.OpenRead(imagePath))
                    {
                        img = XImage.FromStream(imageStream);
                        PdfPage page = document.AddPage();
                        page.Width = XUnit.FromPoint(img.PixelWidth);
                        page.Height = XUnit.FromPoint(img.PixelHeight);
                        XGraphics gfx = XGraphics.FromPdfPage(page);
                        gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                    }
                    File.Delete(imagePath);
                }
            }
            CommonHelper.DeleteTempFolder(chapterDataBuffer.First().TempDirectory);

            var sanitizedTitle = CommonHelper.SanitizeFileName(novel.Title, true);
            Logger.Info($"Saving Pdf to {pdfDirectoryPath}");
            var pdfFilePath = Path.Combine(pdfDirectoryPath, sanitizedTitle + PdfFileExtension);
            document.Save(pdfFilePath);
            Logger.Info($"Pdf saved to {pdfFilePath}");
            Console.WriteLine($"Pdf saved to {pdfFilePath}");
            return pdfFilePath;
        }

        /// <summary>
        /// Method that will update an existing pdf file with new chapters, does not work with single chapter pdfs
        /// </summary>
        /// <param name="novel"></param>
        /// <param name="chapterDataBuffer"></param>
        /// <param name="configuration"></param>
        /// <exception cref="ArgumentException"></exception>
        public void UpdatePdf(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, Configuration configuration)
        {
            var pdfFilePath = novel.SaveLocation;
            if (Path.GetExtension(pdfFilePath) != PdfFileExtension)
            {
                CommonHelper.DeleteTempFolder(chapterDataBuffer.First().TempDirectory);
                throw new ArgumentException("The path to the pdf file is not a pdf file. " + pdfFilePath);
            }
            if (!File.Exists(pdfFilePath))
            {
                CommonHelper.DeleteTempFolder(chapterDataBuffer.First().TempDirectory);
                throw new ArgumentException("The path to the pdf file does not exist. " + pdfFilePath + "\n Please try to update the save location of the novel by running the command 'benny-scraper -L " + novel.Id + "'");
            }

            var tempPdfFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + PdfFileExtension);

            Logger.Info("Updating Pdf file: " + pdfFilePath);
            using (FileStream pdfFile = File.OpenRead(pdfFilePath)) // dispose the filestream after use to avoid the error "The process cannot access the file because it is being used by another process"
            using (PdfDocument document = PdfReader.Open(pdfFile, PdfDocumentOpenMode.Modify))
            {
                document.Info.ModificationDate = DateTime.Now;
                foreach (var chapter in chapterDataBuffer)
                {
                    if (chapter.Pages == null)
                        continue;

                    var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList();
                    Console.WriteLine($"Total images in chapter {chapter.Title}: {imagePaths.Count}");

                    foreach (var imagePath in imagePaths)
                    {
                        XImage img;
                        using (var imageStream = File.OpenRead(imagePath))
                        {
                            img = XImage.FromStream(imageStream);
                            PdfPage page = document.AddPage();
                            page.Width = XUnit.FromPoint(img.PixelWidth);
                            page.Height = XUnit.FromPoint(img.PixelHeight);

                            XGraphics gfx = XGraphics.FromPdfPage(page);

                            gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                        }
                        File.Delete(imagePath);
                        document.Save(tempPdfFilePath);
                    }
                }
            }
            CommonHelper.DeleteTempFolder(chapterDataBuffer.First().TempDirectory);

            Logger.Info($"Saving Pdf to {pdfFilePath}");
            File.Copy(tempPdfFilePath, pdfFilePath, true);
            File.Delete(tempPdfFilePath);
            Logger.Info("Pdf file updated");
            Console.WriteLine($"Pdf file updated at {pdfFilePath}");
        }
    }
}
