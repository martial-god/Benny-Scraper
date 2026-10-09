using System.IO.Compression;
using System.Xml;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.FileGenerators;
using BennyScraper.Models;
using ImageMagick;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf;
using Configuration = BennyScraper.Models.Configuration;

namespace BennyScraper.Tests;

public sealed class ArchiveUpdateTests
{
    [Fact]
    public void ComicUpdateReplacesBothLegacyLayoutsWithoutChangingOtherChapters()
    {
        WithTemporaryDirectory(temporaryDirectory =>
        {
            string archivePath = Path.Combine(temporaryDirectory, "book.cbz");
            string[] originalEntryNames =
            [
                "Chapter_1/Chapter_1_Page1.jpg",
                "Chapter_1_Page2.jpg",
                "Chapter_10/Chapter_10_Page1.jpg",
                "ComicInfo.xml"
            ];
            CreateArchive(archivePath, originalEntryNames);
            string imagePath = CreateImage(temporaryDirectory, "actually-png.jpg");
            byte[] sourceImageBytes = File.ReadAllBytes(imagePath);
            using var chapter = CreateChapter(1, imagePath);
            var novel = new Novel { SaveLocation = archivePath, FileType = NovelFileType.Cbz };
            var generator = new ComicBookArchiveGenerator();

            generator.UpdateComicBookArchive(novel, [chapter], temporaryDirectory, new Configuration());
            generator.UpdateComicBookArchive(novel, [chapter], temporaryDirectory, new Configuration());

            using var updatedArchive = ZipFile.OpenRead(archivePath);
            Assert.Equal(3, updatedArchive.Entries.Count);
            Assert.Null(updatedArchive.GetEntry("Chapter_1_Page2.jpg"));
            Assert.Null(updatedArchive.GetEntry("Chapter_1/Chapter_1_Page1.jpg"));
            var updatedPage = updatedArchive.GetEntry("Chapter_1/Chapter_1_Page1.png");
            Assert.NotNull(updatedPage);
            using var imageEntryStream = updatedPage.Open();
            using var copiedImageStream = new MemoryStream();
            imageEntryStream.CopyTo(copiedImageStream);
            Assert.Equal(sourceImageBytes, copiedImageStream.ToArray());
            using var preservedEntryReader = new StreamReader(updatedArchive.GetEntry("Chapter_10/Chapter_10_Page1.jpg")!.Open());
            Assert.Equal("Chapter_10/Chapter_10_Page1.jpg", preservedEntryReader.ReadToEnd());
            Assert.NotNull(updatedArchive.GetEntry("ComicInfo.xml"));
            Assert.Empty(Directory.GetFiles(temporaryDirectory, "*.tmp"));
        });
    }

    [Fact]
    public void FailedComicUpdatePreservesArchiveAndDownloadedImages()
    {
        WithTemporaryDirectory(temporaryDirectory =>
        {
            string archivePath = Path.Combine(temporaryDirectory, "book.cbz");
            CreateArchive(archivePath, ["Chapter_1/Chapter_1_Page1.jpg"]);
            byte[] originalArchiveBytes = File.ReadAllBytes(archivePath);
            string sourceImagePath = CreateImage(temporaryDirectory, "valid.png");
            using var chapter = CreateChapter(1, sourceImagePath, Path.Combine(temporaryDirectory, "missing.png"));

            Assert.Throws<FileNotFoundException>(() => ComicArchiveWriter.WriteArchive(archivePath, [chapter], archivePath));
            Assert.Equal(originalArchiveBytes, File.ReadAllBytes(archivePath));
            Assert.True(File.Exists(sourceImagePath));
            Assert.Empty(Directory.GetFiles(temporaryDirectory, "*.tmp"));
        });
    }

    [Theory]
    [InlineData(".cbr")]
    [InlineData(".cb7")]
    [InlineData(".cbt")]
    [InlineData(".cba")]
    public void LegacyZipWithWrongExtensionBecomesCbzAndKeepsOriginal(string archiveExtension)
    {
        WithTemporaryDirectory(temporaryDirectory =>
        {
            string archivePath = Path.Combine(temporaryDirectory, "book" + archiveExtension);
            CreateArchive(archivePath, ["Chapter_1_Page1.jpg"]);
            byte[] originalArchiveBytes = File.ReadAllBytes(archivePath);
            using var chapter = CreateChapter(2, CreateImage(temporaryDirectory, "new.png"));
            var novel = new Novel { SaveLocation = archivePath, FileType = NovelFileType.Cbr };
            var generator = new ComicBookArchiveGenerator();

            string updatedArchivePath = generator.UpdateComicBookArchive(novel, [chapter], temporaryDirectory, new Configuration());

            Assert.Equal(Path.ChangeExtension(archivePath, ".cbz"), updatedArchivePath);
            Assert.Equal(NovelFileType.Cbz, novel.FileType);
            Assert.Equal(originalArchiveBytes, File.ReadAllBytes(archivePath));
            using var updatedArchive = ZipFile.OpenRead(updatedArchivePath);
            Assert.Equal(2, updatedArchive.Entries.Count);
        });
    }

    [Fact]
    public void LegacyConversionCannotOverwriteAnotherCbz()
    {
        WithTemporaryDirectory(temporaryDirectory =>
        {
            string archivePath = Path.Combine(temporaryDirectory, "book.cbr");
            string existingOutputPath = Path.ChangeExtension(archivePath, ".cbz");
            CreateArchive(archivePath, ["Chapter_1_Page1.jpg"]);
            File.WriteAllText(existingOutputPath, "keep this file");
            using var chapter = CreateChapter(2, CreateImage(temporaryDirectory, "new.png"));
            var novel = new Novel { SaveLocation = archivePath };
            var generator = new ComicBookArchiveGenerator();

            Assert.Throws<IOException>(() =>
                generator.UpdateComicBookArchive(novel, [chapter], temporaryDirectory, new Configuration()));
            Assert.Equal("keep this file", File.ReadAllText(existingOutputPath));
        });
    }

    [Fact]
    public void NonZipAndMissingArchivesFailWithoutCreatingIncompleteBooks()
    {
        WithTemporaryDirectory(temporaryDirectory =>
        {
            string archivePath = Path.Combine(temporaryDirectory, "book.cbr");
            byte[] rarSignature = [0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x00];
            File.WriteAllBytes(archivePath, rarSignature);
            using var chapter = CreateChapter(2, CreateImage(temporaryDirectory, "new.png"));
            var novel = new Novel { SaveLocation = archivePath };
            var generator = new ComicBookArchiveGenerator();

            Assert.Throws<NotSupportedException>(() =>
                generator.UpdateComicBookArchive(novel, [chapter], temporaryDirectory, new Configuration()));
            Assert.Equal(rarSignature, File.ReadAllBytes(archivePath));
            novel.SaveLocation = Path.Combine(temporaryDirectory, "missing.cbz");
            Assert.Throws<FileNotFoundException>(() =>
                generator.UpdateComicBookArchive(novel, [chapter], temporaryDirectory, new Configuration()));
            Assert.False(File.Exists(novel.SaveLocation));
        });
    }

    [Fact]
    public void PartialAndDuplicateChaptersCannotReplaceAnArchive()
    {
        WithTemporaryDirectory(temporaryDirectory =>
        {
            string archivePath = Path.Combine(temporaryDirectory, "book.cbz");
            CreateArchive(archivePath, ["Chapter_1_Page1.jpg"]);
            byte[] originalArchiveBytes = File.ReadAllBytes(archivePath);
            using var chapter = CreateChapter(1, CreateImage(temporaryDirectory, "new.png"));
            chapter.IsPartial = true;
            Assert.Throws<InvalidOperationException>(() => ComicArchiveWriter.WriteArchive(archivePath, [chapter], archivePath));
            chapter.IsPartial = false;
            Assert.Throws<InvalidOperationException>(() => ComicArchiveWriter.WriteArchive(archivePath, [chapter, chapter], archivePath));
            Assert.Equal(originalArchiveBytes, File.ReadAllBytes(archivePath));
        });
    }

    [Fact]
    public void PdfUpdateFailurePreservesEveryOriginalPage()
    {
        WithTemporaryDirectory(temporaryDirectory =>
        {
            string pdfPath = Path.Combine(temporaryDirectory, "book.PDF");
            using (var originalDocument = new PdfDocument())
            {
                originalDocument.AddPage();
                originalDocument.Save(pdfPath);
            }

            byte[] originalPdfBytes = File.ReadAllBytes(pdfPath);
            string validImagePath = CreateImage(temporaryDirectory, "valid.png");
            using var chapter = CreateChapter(1, validImagePath, Path.Combine(temporaryDirectory, "missing.png"));
            var novel = new Novel { SaveLocation = pdfPath };

            Assert.Throws<InvalidDataException>(() =>
                PdfGenerator.UpdatePdf(novel, [chapter], new Configuration(), new Dictionary<float, int> { [1] = 1 }));
            Assert.Equal(originalPdfBytes, File.ReadAllBytes(pdfPath));
            Assert.True(File.Exists(validImagePath));
            Assert.Empty(Directory.GetFiles(temporaryDirectory, "*.tmp"));
        });
    }

    [Fact]
    public void EpubGenerationFailureIsReportedAndPreservesExistingBook()
    {
        WithTemporaryDirectory(temporaryDirectory =>
        {
            string epubPath = Path.Combine(temporaryDirectory, "book.epub");
            File.WriteAllText(epubPath, "existing book");
            var generator = new EpubGenerator(Options.Create(new EpubTemplates { ContainerXml = "invalid XML" }));

            Assert.Throws<XmlException>(() => generator.CreateEpub(new Novel { Title = "Book" }, [], epubPath, null));
            Assert.Equal("existing book", File.ReadAllText(epubPath));
            Assert.Empty(Directory.GetFiles(temporaryDirectory, "*.tmp"));
        });
    }

    private static ChapterDataBuffer CreateChapter(int chapterNumber, params string[] imagePaths)
    {
        var chapter = new ChapterDataBuffer { Title = $"Chapter {chapterNumber}", SequenceNumber = chapterNumber };
        chapter.SetPages(imagePaths.Select(imagePath => new PageData { ImagePath = imagePath }));
        return chapter;
    }

    private static string CreateImage(string temporaryDirectory, string filename)
    {
        string imagePath = Path.Combine(temporaryDirectory, filename);
        using var sourceImage = new MagickImage(new MagickColor("#1e5aa0"), 16, 12);
        sourceImage.Write(imagePath, MagickFormat.Png);
        return imagePath;
    }

    private static void CreateArchive(string archivePath, string[] entryNames)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (string entryName in entryNames)
        {
            using var entryWriter = new StreamWriter(archive.CreateEntry(entryName).Open());
            entryWriter.Write(entryName);
        }
    }

    private static void WithTemporaryDirectory(Action<string> testAction)
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-archive-test-").FullName;
        try
        {
            testAction(temporaryDirectory);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }
}