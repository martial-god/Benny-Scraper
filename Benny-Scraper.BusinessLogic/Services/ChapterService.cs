using BennyScraper.BusinessLogic.Services.Interface;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.Services;

public class ChapterService : IChapterService
{
    private readonly IUnitOfWork _unitOfWork;

    public ChapterService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Chapter> GetLastSavedChapterByNovelIdAsync(Guid novelId)
    {
        return await _unitOfWork.Chapter.GetLastSavedChapterAsyncByNovelId(novelId);
    }
}