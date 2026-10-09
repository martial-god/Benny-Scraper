using System.Buffers.Binary;
using ImageMagick;

namespace BennyScraper.BusinessLogic.Helper;

internal static class RasterImageReader
{
    private static readonly Dictionary<MagickFormat, string> _imageExtensions = new()
    {
        [MagickFormat.Png] = "png",
        [MagickFormat.Jpeg] = "jpg",
        [MagickFormat.Gif] = "gif",
        [MagickFormat.WebP] = "webp",
        [MagickFormat.Bmp] = "bmp",
        [MagickFormat.Tiff] = "tiff",
        [MagickFormat.Ico] = "ico",
        [MagickFormat.Qoi] = "qoi"
    };

    public static MagickReadSettings CreateReadSettings(Stream sourceImageStream, uint frameCount = 1)
    {
        Span<byte> imageHeader = stackalloc byte[12];
        int headerLength = sourceImageStream.ReadAtLeast(imageHeader, imageHeader.Length, throwOnEndOfStream: false);
        sourceImageStream.Position = 0;
        var availableHeader = imageHeader[..headerLength];
        MagickFormat imageFormat;

        if (availableHeader.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }))
        {
            imageFormat = MagickFormat.Png;
        }
        else if (availableHeader.StartsWith(new byte[] { 0xff, 0xd8, 0xff }))
        {
            imageFormat = MagickFormat.Jpeg;
        }
        else if (availableHeader.StartsWith("GIF87a"u8) || availableHeader.StartsWith("GIF89a"u8))
        {
            imageFormat = MagickFormat.Gif;
        }
        else if (headerLength == 12 && availableHeader.StartsWith("RIFF"u8) && availableHeader[8..].SequenceEqual("WEBP"u8))
        {
            imageFormat = MagickFormat.WebP;
        }
        else if (availableHeader.StartsWith("BM"u8))
        {
            imageFormat = MagickFormat.Bmp;
        }
        else if (availableHeader.StartsWith("II\x2a\0"u8) || availableHeader.StartsWith("MM\0\x2a"u8))
        {
            imageFormat = MagickFormat.Tiff;
        }
        else if (availableHeader.StartsWith(new byte[] { 0, 0, 1, 0 }))
        {
            imageFormat = MagickFormat.Ico;
        }
        else if (availableHeader.StartsWith("qoif"u8))
        {
            imageFormat = MagickFormat.Qoi;
        }
        else
        {
            throw new NotSupportedException("Expected a PNG, JPEG, GIF, WebP, BMP, TIFF, ICO or QOI raster image.");
        }

        return new MagickReadSettings { Format = imageFormat, FrameIndex = 0, FrameCount = frameCount };
    }

    public static string GetFileExtension(MagickFormat imageFormat)
    {
        if (_imageExtensions.TryGetValue(imageFormat, out string? imageExtension))
        {
            return imageExtension;
        }

        throw new NotSupportedException($"Unsupported raster image format: {imageFormat}.");
    }

    public static void RejectAnimatedPng(Stream sourceImageStream)
    {
        try
        {
            sourceImageStream.Position = 8;
            Span<byte> chunkHeader = stackalloc byte[8];
            while (sourceImageStream.Position < sourceImageStream.Length)
            {
                sourceImageStream.ReadExactly(chunkHeader);
                uint chunkLength = BinaryPrimitives.ReadUInt32BigEndian(chunkHeader);
                if (chunkLength > sourceImageStream.Length - sourceImageStream.Position - 4)
                {
                    throw new InvalidDataException("The PNG image contains an incomplete chunk.");
                }

                if (chunkHeader[4..].SequenceEqual("acTL"u8))
                {
                    throw new NotSupportedException("Only single-frame images can be converted.");
                }

                if (chunkHeader[4..].SequenceEqual("IDAT"u8) || chunkHeader[4..].SequenceEqual("IEND"u8))
                {
                    return;
                }

                sourceImageStream.Seek(chunkLength + 4L, SeekOrigin.Current);
            }
        }
        finally
        {
            sourceImageStream.Position = 0;
        }
    }
}