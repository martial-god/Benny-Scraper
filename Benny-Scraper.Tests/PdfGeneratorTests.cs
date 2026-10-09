using BennyScraper.BusinessLogic.FileGenerators;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.Models;
using ImageMagick;
using PdfSharp.Pdf.IO;
using Configuration = BennyScraper.Models.Configuration;

namespace BennyScraper.Tests;

public sealed class PdfGeneratorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidImageCannotReplaceAnExistingPdf(bool singlePdf)
    {
        string temporaryDirectory = CommonHelper.CreateTempDirectory();
        string outputDirectory = CommonHelper.CreateTempDirectory();
        try
        {
            string imagePath = Path.Combine(temporaryDirectory, "valid.png");
            using (var sourceImage = new MagickImage(MagickColors.White, 8, 8))
            {
                sourceImage.Write(imagePath, MagickFormat.Png);
            }

            var novel = new Novel { Title = "Book" };
            string pdfPath = Path.Combine(outputDirectory, singlePdf ? "Book.pdf" : "Book - Chapter 1.pdf");
            File.WriteAllText(pdfPath, "original book");
            using var chapter = new ChapterDataBuffer { Title = "Chapter 1", TempDirectory = temporaryDirectory };
            chapter.SetPages([
                new PageData { ImagePath = imagePath },
                new PageData { ImagePath = Path.Combine(temporaryDirectory, "missing.png") }
            ]);

            Assert.Throws<InvalidDataException>(() =>
            {
                using var downloadScope = new ChapterDownloadScope([chapter]);
                if (singlePdf)
                {
                    PdfGenerator.CreatePdf(novel, [chapter], outputDirectory, new Configuration { SaveAsSingleFile = true });
                }
                else
                {
                    PdfGenerator.CreatePdfByChapter(novel, [chapter], outputDirectory);
                }
            });

            Assert.Equal("original book", File.ReadAllText(pdfPath));
            Assert.False(Directory.Exists(temporaryDirectory));
            Assert.Empty(Directory.GetFiles(outputDirectory, "*.tmp"));
        }
        finally
        {
            CommonHelper.DeleteTempFolder(temporaryDirectory);
            CommonHelper.DeleteTempFolder(outputDirectory);
        }
    }

    [Fact]
    public void CreatePdfByChapterResizesOversizedImages()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), $"benny-scraper-pdf-test-{Guid.NewGuid()}");
        var imageDirectory = Path.Combine(testDirectory, "images");
        var outputDirectory = Path.Combine(testDirectory, "output");
        Directory.CreateDirectory(imageDirectory);

        try
        {
            var oversizedImagePath = Path.Combine(imageDirectory, "oversized.png");
            using (var image = new MagickImage(MagickColors.White, 2, 65_536))
            {
                image.Write(oversizedImagePath, MagickFormat.Png);
            }

            var novel = new Novel { Title = "PDF Test Novel" };
            using var chapter = new ChapterDataBuffer { Title = "Chapter 1", TempDirectory = imageDirectory };
            chapter.SetPages(
            [
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