using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.Interfaces
{
    public interface IEpubGenerator
    {
        void CreateEpub(Novel novel, IEnumerable<Chapter> chapters, string outputFilePath, byte[]? coverImage);
        string ExecuteCommand(string command);
        void ValidateEpub(string epubFilePath);
    }
}