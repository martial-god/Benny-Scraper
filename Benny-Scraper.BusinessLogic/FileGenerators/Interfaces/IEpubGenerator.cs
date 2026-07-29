namespace BennyScraper.BusinessLogic.FileGenerators.Interfaces;

using BennyScraper.Models;

public interface IEpubGenerator
{
    void CreateEpub(Novel novel, ICollection<Chapter> chapters, string outputFilePath, byte[]? coverImage);
}