using BennyScraper.BusinessLogic.Services.Interfaces;
using BennyScraper.DataAccess.Repository.IRepository;
using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.Services;

internal sealed class ChapterService(IUnitOfWork unitOfWork) : IChapterService
{
    public async Task<Chapter> GetLastSavedChapterByNovelIdAsync(Guid novelId) =>
        await unitOfWork.Chapter.GetLastSavedChapterAsyncByNovelId(novelId).ConfigureAwait(false);
}