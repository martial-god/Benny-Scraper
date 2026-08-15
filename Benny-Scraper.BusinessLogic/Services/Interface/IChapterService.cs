using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.Services.Interfaces;

internal interface IChapterService
{
    Task<Chapter> GetLastSavedChapterByNovelIdAsync(Guid novelId);
}