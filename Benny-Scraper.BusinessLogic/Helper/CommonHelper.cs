using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Benny_Scraper.Models;
using MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Benny_Scraper.BusinessLogic.Helper;
public static class CommonHelper
{
    /// <summary>
    /// Removes invalid characters from a file name and optionally capitalizes the first letter of each word.
    /// </summary>
    /// <param name="fileName">The input name to be processed.</param>
    /// <param name="capitalize">Whether to capitalize the first letter of each word. Default is false.</param>
    /// <param name="culture">The culture to be used for text transformation if capitalizing. Default is the current culture.</param>
    /// <returns>A file-safe name that is title-cased.</returns>
    public static string SanitizeFileName(string fileName, bool capitalize = false, CultureInfo? culture = null)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new string(fileName.Where(ch => !invalidChars.Contains(ch)).ToArray());

        if (capitalize)
        {
            culture ??= CultureInfo.CurrentCulture;
            TextInfo textInfo = culture.TextInfo;
            sanitized = textInfo.ToTitleCase(sanitized.ToLowerInvariant());
        }

        return sanitized;
    }

    public static void DeleteTempFolder(string tempFile)
    {
        string directory = string.Empty;

        if (string.IsNullOrEmpty(tempFile))
            return;

        FileAttributes attr = File.GetAttributes(tempFile);

        if (!attr.HasFlag(FileAttributes.Directory))
            directory = Path.GetDirectoryName(tempFile);
        else
            directory = tempFile;

        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, true);
                Console.WriteLine($"Deleted temp folder {directory}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to delete temp folder {directory}. Reason: {ex.Message}");
                Console.ResetColor();
            }
        }
    }


    public static string GetOutputDirectoryForTitle(string title, string? outputDirectory = null)
    {
        if (!string.IsNullOrEmpty(outputDirectory))
            return Path.Combine(outputDirectory, CommonHelper.SanitizeFileName(title, true));
        string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var novelFileSafeTitle = CommonHelper.SanitizeFileName(title, true);
        return Path.Combine(documentsFolder, "BennyScrapedNovels", novelFileSafeTitle);
    }

    /// <summary>
    /// Creates a temporary file in the user's temp directory
    /// </summary>
    /// <returns></returns>
    public static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    public static ICollection<Chapter> SortNovelChaptersByDateCreated(ICollection<Chapter> chapters) =>
        chapters.OrderBy(chapter => chapter.DateCreated).ToList();
}

public static class MyExtensions
{
    // https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods
    /// <summary>
    /// Extension method for ICollection to add a range of items. Make
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <param name="items"></param>
    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
    {
        if (collection == null || items == null)
            return;

        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}

/// <summary>
/// Resolves errors when using PdfSharpCore with ImageSharp.
/// From https://github.com/ststeiger/PdfSharpCore/issues/426#issuecomment-2129168283
/// </summary>
/// <typeparam name="TPixel"></typeparam>
public class ImageSharp3CompatibleImageSource<TPixel> : ImageSource where TPixel : unmanaged, IPixel<TPixel>
{
    public static IImageSource FromImageSharpImage(
        Image<TPixel> image,
        IImageFormat imgFormat,
        int? quality = 75) =>
        new ImageSharpImageSourceImpl<TPixel>("*" + Guid.NewGuid().ToString("B"), image, quality ?? 75, imgFormat is PngFormat);

    protected override IImageSource FromBinaryImpl(
        string name,
        Func<byte[]> imageSource,
        int? quality = 75)
    {
        Image<TPixel> image = Image.Load<TPixel>(imageSource());
        return new ImageSharpImageSourceImpl<TPixel>(name, image, quality ?? 75, image.Metadata.DecodedImageFormat is PngFormat);
    }

    protected override IImageSource FromFileImpl(string path, int? quality = 75)
    {
        Image<TPixel> image = Image.Load<TPixel>(path);
        return new ImageSharpImageSourceImpl<TPixel>(path, image, quality ?? 75, image.Metadata.DecodedImageFormat is PngFormat);
    }

    protected override IImageSource FromStreamImpl(
        string name,
        Func<Stream> imageStream,
        int? quality = 75)
    {
        using (Stream stream = imageStream())
        {
            Image<TPixel> image = Image.Load<TPixel>(stream);
            return new ImageSharpImageSourceImpl<TPixel>(name, image, quality ?? 75, image.Metadata.DecodedImageFormat is PngFormat);
        }
    }

    private class ImageSharpImageSourceImpl<TPixel2>(
        string name,
        Image<TPixel2> image,
        int quality,
        bool isTransparent)
        : IImageSource
        where TPixel2 : unmanaged, IPixel<TPixel2>
    {
        private Image<TPixel2> Image { get; } = image;

        public int Width => Image.Width;

        public int Height => Image.Height;

        public string Name { get; } = name;

        public bool Transparent { get; internal set; } = isTransparent;

        public void SaveAsJpeg(MemoryStream ms) =>
            Image.SaveAsJpeg(ms, new JpegEncoder()
            {
                Quality = quality
            });

        public void SaveAsPdfBitmap(MemoryStream ms)
        {
            BmpEncoder encoder = new BmpEncoder()
            {
                BitsPerPixel = BmpBitsPerPixel.Pixel32
            };
            Image.Save(ms, encoder);
        }
    }
}

public static class CommandExecutor
{
    public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public static string ExecuteCommand(string command)
    {
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;

        // Detect OS and configure process start info accordingly
        if (IsWindows())
        {
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/c {command}";
        }
        else if (IsMacOS() | IsLinux())
        {
            string shellPath = GetDefaultShell();

            if (string.IsNullOrEmpty(shellPath))
            {
                throw new Exception("Unable to determine default shell.");
            }

            startInfo.FileName = shellPath;
            startInfo.Arguments = $"-c \"{command}\"";
        }
        else
        {
            throw new Exception("Unsupported OS platform.");
        }

        process.StartInfo = startInfo;

        // Set your output and error (asynchronous) handlers
        process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
        process.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode.ToString();
    }

    private static string GetDefaultShell()
    {
        var shellPath = Environment.GetEnvironmentVariable("SHELL");

        if (string.IsNullOrEmpty(shellPath))
        {
            return null;
        }

        if (shellPath.EndsWith("/zsh"))
        {
            return "/bin/zsh";
        }
        else if (shellPath.EndsWith("/bash"))
        {
            return "/bin/bash";
        }
        else
        {
            return null;
        }
    }

    private static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
    {
        if (!string.IsNullOrEmpty(outLine.Data))
        {
            Console.WriteLine(outLine.Data);
        }
    }

}
