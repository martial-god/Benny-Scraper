using Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.FileGenerators
{
    public class ComicBookArchiveGenerator : IComicBookArchiveGenerator
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public void CreateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory)
        {
            throw new System.NotImplementedException();
        }
    }
}
