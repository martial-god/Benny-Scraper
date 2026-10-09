using BennyScraper.BusinessLogic.Helper;
using ImageMagick;

namespace BennyScraper.Tests;

public sealed class ImageConversionTests
{
    [Theory]
    [InlineData(MagickFormat.Jpeg)]
    [InlineData(MagickFormat.Png)]
    [InlineData(MagickFormat.WebP)]
    [InlineData(MagickFormat.Bmp)]
    [InlineData(MagickFormat.Tiff)]
    [InlineData(MagickFormat.Gif)]
    [InlineData(MagickFormat.Ico)]
    [InlineData(MagickFormat.Qoi)]
    public async Task ConversionRecognizesImageContentRegardlessOfExtension(MagickFormat sourceImageFormat)
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-image-test-").FullName;
        try
        {
            string sourceImagePath = Path.Combine(temporaryDirectory, "download.bin");
            string outputImagePath = Path.Combine(temporaryDirectory, "converted.webp");
            using (var sourceImage = new MagickImage(MagickColors.Coral, 16, 12))
            {
                await sourceImage.WriteAsync(sourceImagePath, sourceImageFormat);
            }

            await CommonHelper.ConvertImageToWebpAsync(sourceImagePath, outputImagePath);
            using var convertedImage = new MagickImage(outputImagePath);
            Assert.Equal(MagickFormat.WebP, convertedImage.Format);
            Assert.Equal(16U, convertedImage.Width);
            Assert.Equal(12U, convertedImage.Height);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Fact]
    public async Task LosslessConversionPreservesTransparency()
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-image-test-").FullName;
        try
        {
            string sourceImagePath = Path.Combine(temporaryDirectory, "source.png");
            string outputImagePath = Path.Combine(temporaryDirectory, "converted.webp");
            using (var sourceImage = new MagickImage(new MagickColor("#1e5aa080"), 16, 12))
            {
                await sourceImage.WriteAsync(sourceImagePath, MagickFormat.Png);
            }

            await CommonHelper.ConvertImageToWebpAsync(sourceImagePath, outputImagePath);
            using var convertedImage = new MagickImage(outputImagePath);
            using var convertedPixels = convertedImage.GetPixels();
            Assert.True(convertedImage.HasAlpha);
            Assert.Equal(new MagickColor("#1e5aa080"), convertedPixels.GetPixel(0, 0).ToColor());
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Fact]
    public async Task ConversionAppliesExifOrientation()
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-image-test-").FullName;
        try
        {
            string sourceImagePath = Path.Combine(temporaryDirectory, "source.jpg");
            string outputImagePath = Path.Combine(temporaryDirectory, "converted.webp");
            using (var sourceImage = new MagickImage(MagickColors.Coral, 16, 12))
            {
                var exifProfile = new ExifProfile();
                exifProfile.SetValue(ExifTag.Orientation, (ushort)OrientationType.RightTop);
                sourceImage.SetProfile(exifProfile);
                sourceImage.Orientation = OrientationType.RightTop;
                await sourceImage.WriteAsync(sourceImagePath, MagickFormat.Jpeg);
            }

            using (var savedSourceImage = new MagickImage(sourceImagePath))
            {
                Assert.Equal(OrientationType.RightTop, savedSourceImage.Orientation);
            }

            await CommonHelper.ConvertImageToWebpAsync(sourceImagePath, outputImagePath);
            using var convertedImage = new MagickImage(outputImagePath);
            Assert.Equal(12U, convertedImage.Width);
            Assert.Equal(16U, convertedImage.Height);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Theory]
    [InlineData("not an image")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"16\" height=\"12\"></svg>")]
    public async Task ConversionRejectsNonRasterInputWithoutLeavingOutput(string sourceContent)
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-image-test-").FullName;
        try
        {
            string sourceImagePath = Path.Combine(temporaryDirectory, "source.png");
            string outputImagePath = Path.Combine(temporaryDirectory, "converted.webp");
            await File.WriteAllTextAsync(sourceImagePath, sourceContent);
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                CommonHelper.ConvertImageToWebpAsync(sourceImagePath, outputImagePath));
            Assert.False(File.Exists(outputImagePath));
            Assert.Empty(Directory.GetFiles(temporaryDirectory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConversionWritesWebpWithoutChangingSource(bool lossless)
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-image-test-").FullName;
        try
        {
            string sourceImagePath = Path.Combine(temporaryDirectory, "source.png");
            string outputImagePath = Path.Combine(temporaryDirectory, "converted.webp");
            using (var sourceImage = new MagickImage(new MagickColor("#1e5aa0"), 16, 12))
            {
                await sourceImage.WriteAsync(sourceImagePath, MagickFormat.Png);
            }

            byte[] originalImageBytes = await File.ReadAllBytesAsync(sourceImagePath);
            await CommonHelper.ConvertImageToWebpAsync(sourceImagePath, outputImagePath, lossless);

            Assert.Equal(originalImageBytes, await File.ReadAllBytesAsync(sourceImagePath));
            using var convertedImage = new MagickImage(outputImagePath);
            Assert.Equal(MagickFormat.WebP, convertedImage.Format);
            Assert.Equal(16U, convertedImage.Width);
            Assert.Equal(12U, convertedImage.Height);
            if (lossless)
            {
                using var convertedPixels = convertedImage.GetPixels();
                Assert.Equal(new MagickColor("#1e5aa0"), convertedPixels.GetPixel(0, 0).ToColor());
            }

            Assert.Empty(Directory.GetFiles(temporaryDirectory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Fact]
    public async Task ConversionDoesNotOverwriteAnExistingImage()
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-image-test-").FullName;
        try
        {
            string existingImagePath = Path.Combine(temporaryDirectory, "existing.webp");
            await File.WriteAllTextAsync(existingImagePath, "existing content");
            await Assert.ThrowsAsync<IOException>(() =>
                CommonHelper.ConvertImageToWebpAsync(existingImagePath, existingImagePath));
            Assert.Equal("existing content", await File.ReadAllTextAsync(existingImagePath));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Theory]
    [InlineData(16384, 1)]
    [InlineData(6400, 6400)]
    public async Task ConversionRejectsOversizedImages(int imageWidth, int imageHeight)
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-image-test-").FullName;
        try
        {
            string sourceImagePath = Path.Combine(temporaryDirectory, "source.png");
            string outputImagePath = Path.Combine(temporaryDirectory, "converted.webp");
            using (var sourceImage = new MagickImage(MagickColors.Black, (uint)imageWidth, (uint)imageHeight))
            {
                await sourceImage.WriteAsync(sourceImagePath, MagickFormat.Png);
            }

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                CommonHelper.ConvertImageToWebpAsync(sourceImagePath, outputImagePath));
            Assert.False(File.Exists(outputImagePath));
            Assert.True(File.Exists(sourceImagePath));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Theory]
    [InlineData(MagickFormat.Gif)]
    [InlineData(MagickFormat.WebP)]
    [InlineData(MagickFormat.Tiff)]
    public async Task ConversionRejectsAnimationsAndCancelledRequests(MagickFormat sourceImageFormat)
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-image-test-").FullName;
        try
        {
            string sourceImagePath = Path.Combine(temporaryDirectory, "source.gif");
            string outputImagePath = Path.Combine(temporaryDirectory, "converted.webp");
            using (var sourceImages = new MagickImageCollection())
            {
                sourceImages.Add(new MagickImage(MagickColors.Black, 8, 8));
                sourceImages.Add(new MagickImage(MagickColors.White, 8, 8));
                sourceImages[0].AnimationDelay = 10;
                sourceImages[1].AnimationDelay = 10;
                await sourceImages.WriteAsync(sourceImagePath, sourceImageFormat);
            }

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                CommonHelper.ConvertImageToWebpAsync(sourceImagePath, outputImagePath));
            using var cancellationSource = new CancellationTokenSource();
            await cancellationSource.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                CommonHelper.ConvertImageToWebpAsync(sourceImagePath, outputImagePath, cancellationToken: cancellationSource.Token));
            Assert.False(File.Exists(outputImagePath));
            Assert.Empty(Directory.GetFiles(temporaryDirectory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Fact]
    public async Task ConversionRejectsPngAnimationDeclarations()
    {
        string temporaryDirectory = Directory.CreateTempSubdirectory("benny-image-test-").FullName;
        try
        {
            string sourceImagePath = Path.Combine(temporaryDirectory, "source.png");
            string outputImagePath = Path.Combine(temporaryDirectory, "converted.webp");
            using var sourceImage = new MagickImage(MagickColors.Coral, 16, 12);
            byte[] sourceImageBytes = sourceImage.ToByteArray(MagickFormat.Png);
            byte[] animationChunk = [0, 0, 0, 8, 97, 99, 84, 76, 0, 0, 0, 2, 0, 0, 0, 0, 243, 141, 147, 112];
            byte[] animatedImageBytes = [.. sourceImageBytes[..33], .. animationChunk, .. sourceImageBytes[33..]];
            await File.WriteAllBytesAsync(sourceImagePath, animatedImageBytes);

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                CommonHelper.ConvertImageToWebpAsync(sourceImagePath, outputImagePath));
            Assert.False(File.Exists(outputImagePath));
            Assert.Empty(Directory.GetFiles(temporaryDirectory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }
}