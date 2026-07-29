using BennyScraper.BusinessLogic.Services.Interface;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.Services;

public class ChapterService(IUnitOfWork unitOfWork) : IChapterService
{
    public async Task<Chapter> GetLastSavedChapterByNovelIdAsync(Guid novelId) =>
        await unitOfWork.Chapter.GetLastSavedChapterAsyncByNovelId(novelId);
}