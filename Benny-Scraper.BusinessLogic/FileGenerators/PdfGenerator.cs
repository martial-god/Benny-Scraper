using Benny_Scraper.Models;
using NLog;
using SixLabors.ImageSharp;
using PdfSharpCore.Drawing;
using Benny_Scraper.BusinessLogic.Helper;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace Benny_Scraper.BusinessLogic.FileGenerators
{
    public class PdfGenerator
    {
        private static readonly NLog.ILogger Logger = LogManager.GetCurrentClassLogger();
        public const string PdfFileExtension = ".pdf";

        public (string, bool) CreatePdf(Novel novel, List<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Models.Configuration configuration)
        {
            string pdfSaveLocation;
            var isPdfSplit = false;
            Logger.Info("Creating PDFs for {0}", novel.Title);
            int? totalPages = novel.Chapters.Where(chapter => chapter.Pages != null).SelectMany(chapter => chapter.Pages).Count();
            var missingChapters = novel.Chapters.Where(chapter => chapter.Pages == null || !chapter.Pages.Any()).ToList();
            var totalMissingChapters = missingChapters.Count();
            var missingChapterUrls = missingChapters.Select(chapter => chapter.Url);

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
            var result = CommandExecutor.ExecuteCommand($"calibredb add \"{outputDirectory}\" --series \"{novel.Title}\"");
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

                var totalPages = chapter.Pages.Count();
                Console.WriteLine($"Total images in chapter {chapter.Title}: {totalPages}");

                PdfDocument document = new PdfDocument();

                document.Info.Title = $"{novel.Title} - {chapter.Title}";
                document.Info.Author = !string.IsNullOrEmpty(novel.Author) ? novel.Author : null;
                document.Info.Subject = novel.Genre;
                document.Info.Keywords = novel.Genre;
                document.Info.CreationDate = DateTime.Now;

                foreach (var pageData in chapter.Pages)
                {
                    using var image = SixLabors.ImageSharp.Image.Load(pageData.ImagePath);
                    var pdfPage = document.AddPage();
                    pdfPage.Width = XUnit.FromPoint(image.Width);
                    pdfPage.Height = XUnit.FromPoint(image.Height);

                    var gfx = XGraphics.FromPdfPage(pdfPage);

                    using var xImage = XImage.FromStream(() => ConvertImageToStream(image)); // this expects a delegate that is only evaluated when needed by the fromstream
                    gfx.DrawImage(xImage, 0, 0, pdfPage.Width, pdfPage.Height);
                }

                var sanitizedTitle = CommonHelper.SanitizeFileName($"{novel.Title} - {chapter.Title}", true);
                var pdfFilePath = Path.Combine(pdfDirectoryPath, sanitizedTitle + PdfFileExtension);
                document.Save(pdfFilePath);
            }

            return pdfDirectoryPath;
        }


        public string CreateSinglePdf(Novel novel, List<ChapterDataBuffer> chapterDataBuffer, string pdfDirectoryPath)
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
                    var image = Image.Load(imagePath);
                    img = XImage.FromStream(() => ConvertImageToStream(image));
                    PdfPage page = document.AddPage();
                    page.Width = XUnit.FromPoint(img.PixelWidth);
                    page.Height = XUnit.FromPoint(img.PixelHeight);
                    XGraphics gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawImage(img, 0, 0, page.Width, page.Height);
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
        public void UpdatePdf(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, Models.Configuration configuration)
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
            using (FileStream pdfFile = File.OpenRead(pdfFilePath))
            {
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
                            using var image = Image.Load(imagePath);
                            img = XImage.FromStream(() => ConvertImageToStream(image));
                            PdfPage page = document.AddPage();
                            page.Width = XUnit.FromPoint(img.PixelWidth);
                            page.Height = XUnit.FromPoint(img.PixelHeight);

                            XGraphics gfx = XGraphics.FromPdfPage(page);
                            gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                            File.Delete(imagePath);

                            document.Save(tempPdfFilePath);
                        }
                    }
                }
            } // dispose the filestream after use to avoid the error "The process cannot access the file because it is being used by another process"
            
            CommonHelper.DeleteTempFolder(chapterDataBuffer.First().TempDirectory);

            Logger.Info($"Saving Pdf to {pdfFilePath}");
            File.Copy(tempPdfFilePath, pdfFilePath, true);
            File.Delete(tempPdfFilePath);
            Logger.Info("Pdf file updated");
            Console.WriteLine($"Pdf file updated at {pdfFilePath}");
        }

        private static MemoryStream ConvertImageToStream(SixLabors.ImageSharp.Image image)
        {
            var memoryStream = new MemoryStream();
            image.Save(memoryStream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}
