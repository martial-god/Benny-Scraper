namespace Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;

using Benny_Scraper.Models;

public interface IEpubGenerator
{
    void CreateEpub(Novel novel, IEnumerable<Chapter> chapters, string outputFilePath, byte[]? coverImage);
}
