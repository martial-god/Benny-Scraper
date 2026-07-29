using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.FileGenerators.Interfaces;

public interface IComicBookArchiveGenerator
{
    public string CreateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration, string filenameSuffix = "");

    public string UpdateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration);
}