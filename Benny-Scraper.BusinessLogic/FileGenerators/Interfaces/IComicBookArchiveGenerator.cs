using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;
public interface IComicBookArchiveGenerator
{
    public string CreateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration);
    public string UpdateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration);
}
