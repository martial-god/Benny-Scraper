using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.Helper;

/// <summary>
/// Simple class that allows us to delete the temporary chapter files and folders once we leave our scope.
/// </summary>
internal sealed class ChapterDownloadScope : IDisposable
{
    private readonly ChapterDataBuffer[] _chapterDataBuffers;
    private readonly string[] _temporaryDirectories;

    public ChapterDownloadScope(IEnumerable<ChapterDataBuffer> chapterDataBuffers)
    {
        _chapterDataBuffers = chapterDataBuffers.ToArray();
        _temporaryDirectories = _chapterDataBuffers
            .Select(chapterDataBuffer => chapterDataBuffer.TempDirectory)
            .Where(temporaryDirectory => !string.IsNullOrWhiteSpace(temporaryDirectory))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
    }

    public void Dispose()
    {
        foreach (string temporaryDirectory in _temporaryDirectories)
        {
            try
            {
                string directoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(temporaryDirectory));
                string temporaryRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (!string.Equals(Path.GetDirectoryName(directoryPath), temporaryRootPath, pathComparison) ||
                    !Guid.TryParse(Path.GetFileName(directoryPath), out _))
                {
                    Console.WriteLine($"Skipped cleanup of unrecognized chapter temporary directory: {temporaryDirectory}");
                    continue;
                }

                if (Directory.Exists(directoryPath) &&
                    (File.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) != 0)
                {
                    Console.WriteLine($"Skipped cleanup of linked chapter temporary directory: {temporaryDirectory}");
                    continue;
                }

                CommonHelper.DeleteTempFolder(directoryPath);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Failed to clean chapter temporary directory {temporaryDirectory}. Reason: {exception.Message}");
            }
        }

        foreach (var chapterDataBuffer in _chapterDataBuffers)
        {
            chapterDataBuffer.Dispose();
        }
    }
}