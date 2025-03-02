using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.Services;
public class ChapterService(IUnitOfWork unitOfWork) : IChapterService
{
    public async Task<Chapter> GetLastSavedChapterByNovelIdAsync(Guid novelId)
    {
        return await unitOfWork.Chapter.GetLastSavedChapterAsyncByNovelId(novelId);
    }
}
