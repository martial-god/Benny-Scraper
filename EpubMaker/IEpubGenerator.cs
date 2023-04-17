using Benny_Scraper.Models;

namespace Benny_Scraper.EpubMaker
{
    public interface IEpubGenerator
    {
        void CreateEpub(Novel novel, IEnumerable<Chapter> chapters, string outputFilePath);
    }
}