using Benny_Scraper.BusinessLogic.Helper;
using Benny_Scraper.Models;
using NLog;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SixLabors.ImageSharp;
using System.Diagnostics;

namespace Benny_Scraper.BusinessLogic.FileGenerators
{
    public class PdfGenerator
    {
        private static readonly NLog.ILogger Logger = LogManager.GetCurrentClassLogger();
        public const string PdfFileExtension = ".pdf";

        public (string, bool) CreatePdf(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Models.Configuration configuration, string filenameSuffix = "")
        {
            string pdfSaveLocation;
            var isPdfSplit = false;
            Logger.Info("Creating PDFs for {0}", novel.Title);
            var totalPages = novel.Chapters.Where(chapter => chapter.Pages != null).SelectMany(chapter =>
            {
                Debug.Assert(chapter.Pages != null, "chapter.Pages != null");
                return chapter.Pages;
            }).Count();
            var totalMissingChapters = novel.Chapters.Count(chapter => chapter.Pages == null || !chapter.Pages.Any());
            var missingChapterUrls = novel.Chapters.Where(chapter => chapter.Pages == null).Select(chapter => chapter.Url);

            Logger.Info(new string('=', 50));
            Console.ForegroundColor = ConsoleColor.Blue;
            if (configuration.SaveAsSingleFile)
            {
                pdfSaveLocation = CreateSinglePdf(novel, chapterDataBuffers, outputDirectory, filenameSuffix);
            }
            else
            {
                pdfSaveLocation = CreatePdfByChapter(novel, chapterDataBuffers, outputDirectory, filenameSuffix);
                isPdfSplit = true;
            }

            // Display completion summary in a formatted box
            Console.WriteLine();
            var pdfMessages = new[] { "PDF GENERATION COMPLETE!" };
            CommonHelper.DrawBox(pdfMessages, ConsoleColor.Green);
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  Novel:          {novel.Title}");
            Console.WriteLine($"  Novel ID:       {novel.Id}");
            if (novel.ChapterRanges.Any())
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
            Logger.Debug($"Calibre command executed with code: {result}");

            if (result == "0")
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Successfully added to Calibre");
                Console.ResetColor();
            }

            Console.WriteLine();
            Logger.Debug($"PDF generation complete - Novel: {novel.Title}, Chapters: {novel.Chapters.Count}, Pages: {totalPages}, Location: {outputDirectory}");
            return (pdfSaveLocation, isPdfSplit);
        }

        public string CreatePdfByChapter(Novel? novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, string pdfDirectoryPath, string filenameSuffix = "")
        {
            Directory.CreateDirectory(pdfDirectoryPath);

            foreach (var chapter in chapterDataBuffer)
            {
                if (chapter.Pages == null)
                    continue;

                var totalPages = chapter.Pages.Count();
                Console.WriteLine($"Total images in chapter {chapter.Title}: {totalPages}");

                var document = new PdfDocument();

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

                    using var imageStream = ConvertImageToStream(image);
                    using var xImage = XImage.FromStream(imageStream);
                    gfx.DrawImage(xImage, 0, 0, pdfPage.Width.Point, pdfPage.Height.Point);
                }

                var baseFilename = $"{novel.Title} - {chapter.Title}";
                var filename = string.IsNullOrEmpty(filenameSuffix) ? baseFilename : $"{novel.Title} - {filenameSuffix} - {chapter.Title}";
                var sanitizedTitle = CommonHelper.SanitizeFileName(filename, true);
                var pdfFilePath = Path.Combine(pdfDirectoryPath, sanitizedTitle + PdfFileExtension);
                document.Save(pdfFilePath);
            }

            return pdfDirectoryPath;
        }


        private static string CreateSinglePdf(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, string pdfDirectoryPath, string filenameSuffix = "")
        {
            Directory.CreateDirectory(pdfDirectoryPath);

            var document = new PdfDocument();

            document.Info.Title = $"{novel.Title}";
            document.Info.Author = !string.IsNullOrEmpty(novel.Author) ? novel.Author : null;
            document.Info.Subject = novel.Genre;
            document.Info.Keywords = novel.Genre;
            document.Info.CreationDate = DateTime.Now;

            var chapterDataBuffers = chapterDataBuffer as ChapterDataBuffer[] ?? chapterDataBuffer.ToArray();
            foreach (var chapter in chapterDataBuffers)
            {
                if (chapter.Pages == null)
                    continue;

                var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList(); // only Page from PageData has ImagePath as a member variable
                Console.WriteLine($"Total images in chapter {chapter.Title}: {imagePaths.Count}");

                foreach (var imagePath in imagePaths)
                {
                    using var image = Image.Load(imagePath);
                    using var imageStream = ConvertImageToStream(image);
                    using var img = XImage.FromStream(imageStream);
                    var page = document.AddPage();
                    page.Width = XUnit.FromPoint(img.PixelWidth);
                    page.Height = XUnit.FromPoint(img.PixelHeight);
                    var gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawImage(img, 0, 0, page.Width.Point, page.Height.Point);
                    File.Delete(imagePath);
                }
            }
            CommonHelper.DeleteTempFolder(chapterDataBuffers.First().TempDirectory);

            var baseFilename = novel.Title;
            var filename = string.IsNullOrEmpty(filenameSuffix) ? baseFilename : $"{baseFilename} - {filenameSuffix}";
            var sanitizedTitle = CommonHelper.SanitizeFileName(filename, true);
            var pdfFilePath = Path.Combine(pdfDirectoryPath, sanitizedTitle + PdfFileExtension);
            document.Save(pdfFilePath);
            Logger.Debug($"PDF saved to {pdfFilePath}");
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
            var chapterDataBuffers = chapterDataBuffer as ChapterDataBuffer[] ?? chapterDataBuffer.ToArray();
            using (var pdfFile = File.OpenRead(pdfFilePath))
            {
                using (var document = PdfReader.Open(pdfFile, PdfDocumentOpenMode.Modify))
                {
                    document.Info.ModificationDate = DateTime.Now;
                    foreach (var chapter in chapterDataBuffers)
                    {
                        if (chapter.Pages == null)
                            continue;

                        var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList();
                        Console.WriteLine($"Total images in chapter {chapter.Title}: {imagePaths.Count}");

                        foreach (var imagePath in imagePaths)
                        {
                            using var image = Image.Load(imagePath);
                            using var imageStream = ConvertImageToStream(image);
                            using var img = XImage.FromStream(imageStream);
                            var page = document.AddPage();
                            page.Width = XUnit.FromPoint(img.PixelWidth);
                            page.Height = XUnit.FromPoint(img.PixelHeight);

                            var gfx = XGraphics.FromPdfPage(page);
                            gfx.DrawImage(img, 0, 0, page.Width.Point, page.Height.Point);
                            File.Delete(imagePath);

                            document.Save(tempPdfFilePath);
                        }
                    }
                }
            } // dispose the filestream after use to avoid the error "The process cannot access the file because it is being used by another process"

            CommonHelper.DeleteTempFolder(chapterDataBuffers.First().TempDirectory);

            Logger.Info($"Saving Pdf to {pdfFilePath}");
            File.Copy(tempPdfFilePath, pdfFilePath, true);
            File.Delete(tempPdfFilePath);
            Logger.Info("Pdf file updated");
            Console.WriteLine($"Pdf file updated at {pdfFilePath}");
        }

        private static MemoryStream ConvertImageToStream(Image image)
        {
            var memoryStream = new MemoryStream();
            image.Save(memoryStream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
            memoryStream.Position = 0;
            return memoryStream;
        }
    }
}
