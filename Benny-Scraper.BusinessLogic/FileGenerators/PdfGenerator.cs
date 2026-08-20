using System.Diagnostics;
using System.Globalization;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.Models;
using NLog;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace BennyScraper.BusinessLogic.FileGenerators;

internal static class PdfGenerator
{
    public const string PdfFileExtension = ".pdf";
    private const int _maximumJpegDimension = 65_500;
    private const double _maximumPdfPageDimension = 14_400;
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Updates an existing combined PDF with new chapters and replaces recovered chapter pages in their original reading position.
    /// </summary>
    /// <param name="novel">The novel whose existing pdf file is being updated.</param>
    /// <param name="chapterDataBuffer">The new or recovered chapter data, including page image paths.</param>
    /// <param name="configuration">The configuration for the pdf generation.</param>
    /// <param name="originalPageCountsByChapterNumber">
    /// Page counts captured before recovered chapters were updated. When supplied, matching chapters replace their
    /// existing PDF page range while chapters absent from the map are appended as new chapters.
    /// </param>
    /// <exception cref="ArgumentException">The path to the pdf file is not a pdf file. </exception>
    public static void UpdatePdf(
        Novel novel,
        IEnumerable<ChapterDataBuffer> chapterDataBuffer,
        Models.Configuration configuration,
        IReadOnlyDictionary<float, int>? originalPageCountsByChapterNumber = null)
    {
        var chapterDataBuffers = chapterDataBuffer
            .Where(chapter => chapter.Pages is { Count: > 0 })
            .OrderBy(chapter => chapter.SequenceNumber)
            .ToArray();
        if (chapterDataBuffers.Length == 0)
        {
            return;
        }

        var pdfFilePath = novel.SaveLocation;
        if (Path.GetExtension(pdfFilePath) != PdfFileExtension)
        {
            CommonHelper.DeleteTempFolder(chapterDataBuffers[0].TempDirectory);
            throw new ArgumentException("The path to the pdf file is not a pdf file. " + pdfFilePath);
        }

        if (!File.Exists(pdfFilePath))
        {
            CommonHelper.DeleteTempFolder(chapterDataBuffers[0].TempDirectory);
            throw new ArgumentException("The path to the pdf file does not exist. " + pdfFilePath + "\n Please try to update the save location of the novel by running the command 'benny-scraper -L " + novel.Id + "'");
        }

        var tempPdfFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + PdfFileExtension);
        var imagePathsToDelete = new List<string>();

        _logger.Info("Updating Pdf file: " + pdfFilePath);
        try
        {
            using (var pdfFile = File.OpenRead(pdfFilePath))
            using (var document = PdfReader.Open(pdfFile, PdfDocumentOpenMode.Modify))
            {
                document.Info.ModificationDate = DateTime.Now;
                var currentPageCountsByChapterNumber = originalPageCountsByChapterNumber == null
                    ? null
                    : new Dictionary<float, int>(originalPageCountsByChapterNumber);

                if (currentPageCountsByChapterNumber != null)
                {
                    var expectedExistingPageCount = currentPageCountsByChapterNumber.Values.Sum();
                    if (document.PageCount != expectedExistingPageCount)
                    {
                        throw new InvalidOperationException(
                            $"Cannot safely replace recovered pages in {pdfFilePath}. " +
                            $"The PDF contains {document.PageCount} pages but the database describes {expectedExistingPageCount} pages.");
                    }
                }

                foreach (var chapter in chapterDataBuffers)
                {
                    var imagePaths = chapter.Pages!.Select(page => page.ImagePath).ToList();
                    Console.WriteLine($"Total images in chapter {chapter.Title}: {imagePaths.Count}");
                    var chapterNumber = (float)chapter.SequenceNumber;
                    var chapterAlreadyExists = currentPageCountsByChapterNumber?.ContainsKey(chapterNumber) == true;
                    var pageIndex = currentPageCountsByChapterNumber != null
                        ? currentPageCountsByChapterNumber
                            .Where(pageCount => pageCount.Key < chapterNumber)
                            .Sum(pageCount => pageCount.Value)
                        : document.PageCount;

                    var pagesAdded = 0;
                    foreach (var imagePath in imagePaths)
                    {
                        int? insertionIndex = currentPageCountsByChapterNumber != null ? pageIndex + pagesAdded : null;
                        if (TryAddImageToPdf(document, imagePath, insertionIndex))
                        {
                            pagesAdded++;
                            imagePathsToDelete.Add(imagePath);
                        }
                    }

                    if (chapterAlreadyExists && pagesAdded > 0)
                    {
                        var originalPageCount = currentPageCountsByChapterNumber![chapterNumber];
                        for (var pageNumber = 0; pageNumber < originalPageCount; pageNumber++)
                        {
                            document.Pages.RemoveAt(pageIndex + pagesAdded);
                        }
                    }

                    if (currentPageCountsByChapterNumber != null && pagesAdded > 0)
                    {
                        currentPageCountsByChapterNumber[chapterNumber] = pagesAdded;
                    }
                    else if (chapterAlreadyExists)
                    {
                        _logger.Warn($"No replacement images for chapter {chapter.Title} could be added. The existing PDF pages were kept.");
                    }
                }

                document.Save(tempPdfFilePath);
            }

            // The source stream must be closed before overwriting the original PDF.
            _logger.Info($"Saving Pdf to {pdfFilePath}");
            File.Copy(tempPdfFilePath, pdfFilePath, true);

            foreach (var imagePath in imagePathsToDelete)
            {
                File.Delete(imagePath);
            }

            CommonHelper.DeleteTempFolder(chapterDataBuffers[0].TempDirectory);
            _logger.Info("Pdf file updated");
            Console.WriteLine($"Pdf file updated at {pdfFilePath}");
        }
        finally
        {
            if (File.Exists(tempPdfFilePath))
            {
                File.Delete(tempPdfFilePath);
            }
        }
    }

    public static string CreatePdfByChapter(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, string pdfDirectoryPath, string filenameSuffix = "")
    {
        Directory.CreateDirectory(pdfDirectoryPath);

        foreach (var chapter in chapterDataBuffer)
        {
            if (chapter.Pages == null)
            {
                continue;
            }

            var totalPages = chapter.Pages.Count;
            Console.WriteLine($"Total images in chapter {chapter.Title}: {totalPages}");

            using var document = new PdfDocument();

            document.Info.Title = $"{novel.Title} - {chapter.Title}";
            document.Info.Author = !string.IsNullOrEmpty(novel.Author) ? novel.Author : string.Empty;
            document.Info.Subject = novel.Genre ??= string.Empty;
            document.Info.Keywords = novel.Genre;
            document.Info.CreationDate = DateTime.Now;

            foreach (var pageData in chapter.Pages)
            {
                TryAddImageToPdf(document, pageData.ImagePath);
            }

            var baseFilename = $"{novel.Title} - {chapter.Title}";
            var filename = string.IsNullOrEmpty(filenameSuffix) ? baseFilename : $"{novel.Title} - {filenameSuffix} - {chapter.Title}";
            var sanitizedTitle = CommonHelper.SanitizeFileName(filename, true);
            var pdfFilePath = Path.Combine(pdfDirectoryPath, sanitizedTitle + PdfFileExtension);
            if (document.PageCount > 0)
            {
                document.Save(pdfFilePath);
            }
            else
            {
                _logger.Warn($"PDF was not created for chapter {chapter.Title} because none of its images could be processed.");
            }
        }

        return pdfDirectoryPath;
    }

    public static (string SaveLocation, bool IsFileSplit) CreatePdf(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Models.Configuration configuration, string filenameSuffix = "")
    {
        string pdfSaveLocation;
        var isPdfSplit = false;
        _logger.Info(CultureInfo.InvariantCulture, "Creating PDFs for {0}", novel.Title);
        var totalPages = novel.Chapters.Where(chapter => chapter.Pages != null).SelectMany(chapter =>
        {
            Debug.Assert(chapter.Pages != null, "chapter.Pages != null");
            return chapter.Pages;
        }).Count();
        var totalMissingChapters = novel.Chapters.Count(chapter => chapter.Pages == null || chapter.Pages.Count == 0);
        var missingChapterUrls = novel.Chapters.Where(chapter => chapter.Pages == null).Select(chapter => chapter.Url);

        _logger.Info(new string('=', 50));
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
        _logger.Debug($"PDF generation complete - Novel: {novel.Title}, Chapters: {novel.Chapters.Count}, Pages: {totalPages}, Location: {outputDirectory}");
        return (pdfSaveLocation, isPdfSplit);
    }

    private static string CreateSinglePdf(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffer, string pdfDirectoryPath, string filenameSuffix = "")
    {
        Directory.CreateDirectory(pdfDirectoryPath);

        using var document = new PdfDocument();

        document.Info.Title = $"{novel.Title}";
        document.Info.Author = !string.IsNullOrEmpty(novel.Author) ? novel.Author : string.Empty;
        document.Info.Subject = novel.Genre ??= string.Empty;
        document.Info.Keywords = novel.Genre;
        document.Info.CreationDate = DateTime.Now;

        var chapterDataBuffers = chapterDataBuffer as ChapterDataBuffer[] ?? chapterDataBuffer.ToArray();
        foreach (var chapter in chapterDataBuffers)
        {
            if (chapter.Pages == null)
            {
                continue;
            }

            var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList(); // only Page from PageData has ImagePath as a member variable
            Console.WriteLine($"Total images in chapter {chapter.Title}: {imagePaths.Count}");

            foreach (var imagePath in imagePaths)
            {
                if (TryAddImageToPdf(document, imagePath))
                {
                    File.Delete(imagePath);
                }
            }
        }

        CommonHelper.DeleteTempFolder(chapterDataBuffers[0].TempDirectory);

        var baseFilename = novel.Title;
        var filename = string.IsNullOrEmpty(filenameSuffix) ? baseFilename : $"{baseFilename} - {filenameSuffix}";
        var sanitizedTitle = CommonHelper.SanitizeFileName(filename, true);
        var pdfFilePath = Path.Combine(pdfDirectoryPath, sanitizedTitle + PdfFileExtension);
        if (document.PageCount == 0)
        {
            throw new InvalidOperationException($"PDF was not created for {novel.Title} because none of its images could be processed.");
        }

        document.Save(pdfFilePath);
        _logger.Debug($"PDF saved to {pdfFilePath}");
        return pdfFilePath;
    }

    private static bool TryAddImageToPdf(PdfDocument document, string imagePath, int? insertionIndex = null)
    {
        PdfPage? pdfPage = null;
        try
        {
            using var image = Image.Load(imagePath);
            ResizeImageForJpeg(image, imagePath);
            using var imageStream = ConvertImageToStream(image);
            using var pdfImage = XImage.FromStream(imageStream);
            pdfPage = insertionIndex.HasValue && insertionIndex.Value < document.PageCount
                ? document.Pages.Insert(insertionIndex.Value)
                : document.AddPage();
            SetPdfPageSize(pdfPage, pdfImage);

            using var graphics = XGraphics.FromPdfPage(pdfPage);
            graphics.DrawImage(pdfImage, 0, 0, pdfPage.Width.Point, pdfPage.Height.Point);
            return true;
        }
        catch (Exception exception)
        {
            if (pdfPage != null)
            {
                document.Pages.Remove(pdfPage);
            }

            _logger.Error(exception, $"Could not add image {imagePath} to the PDF. Skipping this image.");
            Console.WriteLine($"Could not add image to the PDF. Skipping: {imagePath}");
            return false;
        }
    }

    private static void ResizeImageForJpeg(Image image, string imagePath)
    {
        if (image is { Width: <= _maximumJpegDimension, Height: <= _maximumJpegDimension })
        {
            return;
        }

        var scale = Math.Min(_maximumJpegDimension / (double)image.Width, _maximumJpegDimension / (double)image.Height);
        var resizedWidth = Math.Max(1, (int)Math.Floor(image.Width * scale));
        var resizedHeight = Math.Max(1, (int)Math.Floor(image.Height * scale));
        _logger.Debug($"Resizing oversized image {imagePath} from {image.Width}x{image.Height} to {resizedWidth}x{resizedHeight} for PDF creation.");
        image.Mutate(context => context.Resize(resizedWidth, resizedHeight));
    }

    private static void SetPdfPageSize(PdfPage pdfPage, XImage image)
    {
        var scale = Math.Min(1, _maximumPdfPageDimension / Math.Max(image.PixelWidth, image.PixelHeight));
        pdfPage.Width = XUnit.FromPoint(image.PixelWidth * scale);
        pdfPage.Height = XUnit.FromPoint(image.PixelHeight * scale);
    }

    private static MemoryStream ConvertImageToStream(Image image)
    {
        var memoryStream = new MemoryStream();
        try
        {
            image.Save(memoryStream, new JpegEncoder());
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            memoryStream.Dispose();
            throw;
        }
    }
}