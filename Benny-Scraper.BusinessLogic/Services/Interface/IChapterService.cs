using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.Services.Interface
{
    public interface IChapterService
    {
        Task<Chapter> GetLastSavedChapterByNovelIdAsync(Guid novelId);
    }

}
