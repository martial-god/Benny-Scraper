using BennyScraper.BusinessLogic.FileGenerators;
using BennyScraper.Models;
using PdfSharp.Pdf.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BennyScraper.Tests;

public sealed class PdfGeneratorTests
{
    [Fact]
    public void CreatePdfByChapterResizesOversizedImagesAndSkipsInvalidImages()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"benny-scraper-pdf-test-{Guid.NewGuid()}");
        var imageDirectory = Path.Combine(testDirectory, "images");
        var outputDirectory = Path.Combine(testDirectory, "output");
        Directory.CreateDirectory(imageDirectory);

        try
        {
            var invalidImagePath = Path.Combine(imageDirectory, "invalid.jpg");
            File.WriteAllText(invalidImagePath, "not an image");
            var oversizedImagePath = Path.Combine(imageDirectory, "oversized.png");
            using (var image = new Image<Rgba32>(2, 65_536))
            {
                image.SaveAsPng(oversizedImagePath);
            }

            var novel = new Novel { Title = "PDF Test Novel" };
            using var chapter = new ChapterDataBuffer { Title = "Chapter 1", TempDirectory = imageDirectory };
            chapter.SetPages(
            [
                new PageData { ImagePath = invalidImagePath },
                new PageData { ImagePath = oversizedImagePath }
            ]);

            PdfGenerator.CreatePdfByChapter(novel, [chapter], outputDirectory);

            var pdfPath = Assert.Single(Directory.GetFiles(outputDirectory, "*.pdf"));
            using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
            Assert.Single(document.Pages);
            var page = document.Pages[0];
            Assert.True(page.Width.Point <= 14_400);
            Assert.True(page.Height.Point <= 14_400);
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }
    }
}