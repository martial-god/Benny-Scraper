using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic
{
    public interface IEpubGenerator
    {
        void CreateEpub(Novel novel, IEnumerable<Chapter> chapters, string outputFilePath);
        void ValidateEpub(string epubFilePath);
    }
}