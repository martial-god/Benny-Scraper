using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.Services.Interface;

public interface IChapterService
{
    Task<Chapter> GetLastSavedChapterByNovelIdAsync(Guid novelId);
}