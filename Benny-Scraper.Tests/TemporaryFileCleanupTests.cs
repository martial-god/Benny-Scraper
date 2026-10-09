using BennyScraper.BusinessLogic.FileGenerators;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.Models;
using ImageMagick;

namespace BennyScraper.Tests;

public sealed class TemporaryFileCleanupTests
{
    [Fact]
    public void DownloadScopeKeepsSharedImagesUntilTheWholeOperationCompletes()
    {
        string temporaryDirectory = CommonHelper.CreateTempDirectory();
        string secondTemporaryDirectory = CommonHelper.CreateTempDirectory();
        try
        {
            string firstImagePath = Path.Combine(temporaryDirectory, "first.png");
            string secondImagePath = Path.Combine(temporaryDirectory, "second.png");
            File.WriteAllText(firstImagePath, "first image");
            File.WriteAllText(secondImagePath, "second image");
            using var firstChapter = CreateChapter(temporaryDirectory, firstImagePath);
            using var secondChapter = CreateChapter(temporaryDirectory, secondImagePath);
            using var emptyChapter = CreateChapter(secondTemporaryDirectory);

            using (var downloadScope = new ChapterDownloadScope([firstChapter, secondChapter, emptyChapter]))
            {
                firstChapter.Dispose();
                Assert.True(File.Exists(firstImagePath));
                Assert.True(File.Exists(secondImagePath));
            }

            Assert.False(Directory.Exists(temporaryDirectory));
            Assert.False(Directory.Exists(secondTemporaryDirectory));
            Assert.Null(secondChapter.Pages!.Single().ImagePath);
        }
        finally
        {
            CommonHelper.DeleteTempFolder(temporaryDirectory);
            CommonHelper.DeleteTempFolder(secondTemporaryDirectory);
        }
    }

    [Fact]
    public void FailedArchiveWriteCleansDownloadsWithoutChangingTheOriginal()
    {
        string temporaryDirectory = CommonHelper.CreateTempDirectory();
        string outputDirectory = CommonHelper.CreateTempDirectory();
        try
        {
            string sourceImagePath = Path.Combine(temporaryDirectory, "first.png");
            using (var sourceImage = new MagickImage(MagickColors.White, 8, 8))
            {
                sourceImage.Write(sourceImagePath, MagickFormat.Png);
            }

            string archivePath = Path.Combine(outputDirectory, "book.cbz");
            using var originalChapter = CreateChapter(temporaryDirectory, sourceImagePath);
            ComicArchiveWriter.WriteArchive(archivePath, [originalChapter]);
            byte[] originalArchiveBytes = File.ReadAllBytes(archivePath);
            using var replacementChapter = CreateChapter(temporaryDirectory, sourceImagePath, Path.Combine(temporaryDirectory, "missing.png"));

            Assert.Throws<FileNotFoundException>(() =>
            {
                using var downloadScope = new ChapterDownloadScope([replacementChapter]);
                ComicArchiveWriter.WriteArchive(archivePath, [replacementChapter], archivePath);
            });

            Assert.False(Directory.Exists(temporaryDirectory));
            Assert.Equal(originalArchiveBytes, File.ReadAllBytes(archivePath));
            Assert.Empty(Directory.GetFiles(outputDirectory, "*.tmp"));
        }
        finally
        {
            CommonHelper.DeleteTempFolder(temporaryDirectory);
            CommonHelper.DeleteTempFolder(outputDirectory);
        }
    }

    [Fact]
    public void CleanupToleratesDirectoriesThatWereAlreadyPurged()
    {
        string temporaryDirectory = CommonHelper.CreateTempDirectory();
        using var chapter = CreateChapter(temporaryDirectory);
        using var downloadScope = new ChapterDownloadScope([chapter]);
        Directory.Delete(temporaryDirectory);

        CommonHelper.DeleteTempFolder(temporaryDirectory);
        downloadScope.Dispose();

        Assert.False(Directory.Exists(temporaryDirectory));
    }

    [Fact]
    public void DownloadScopeDoesNotDeleteAnUnrecognizedDirectory()
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-preserve-test-").FullName;
        try
        {
            string outputFilePath = Path.Combine(temporaryDirectory, "book.pdf");
            File.WriteAllText(outputFilePath, "keep this book");
            using var chapter = CreateChapter(temporaryDirectory);
            using (var downloadScope = new ChapterDownloadScope([chapter]))
            {
                Assert.True(File.Exists(outputFilePath));
            }

            Assert.Equal("keep this book", File.ReadAllText(outputFilePath));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Fact]
    public void TemporaryFileCleanupDoesNotHideTheOriginalFailure()
    {
        string temporaryDirectory = CommonHelper.CreateTempDirectory();
        try
        {
            var originalException = new InvalidDataException("original generation failure");
            Action failGeneration = () =>
            {
                try
                {
                    throw originalException;
                }
                finally
                {
                    CommonHelper.DeleteTemporaryFile(temporaryDirectory);
                }
            };

            var actualException = Assert.Throws<InvalidDataException>(failGeneration);
            Assert.Same(originalException, actualException);
            Assert.True(Directory.Exists(temporaryDirectory));
        }
        finally
        {
            CommonHelper.DeleteTempFolder(temporaryDirectory);
        }
    }

    private static ChapterDataBuffer CreateChapter(string temporaryDirectory, params string[] imagePaths)
    {
        var chapter = new ChapterDataBuffer
        {
            Title = "Chapter 1",
            SequenceNumber = 1,
            TempDirectory = temporaryDirectory
        };
        chapter.SetPages(imagePaths.Select(imagePath => new PageData { ImagePath = imagePath }));
        return chapter;
    }
}