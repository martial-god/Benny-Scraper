using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.Services.Interfaces;

public interface IChapterService
{
    Task<Chapter> GetLastSavedChapterByNovelIdAsync(Guid novelId);
}