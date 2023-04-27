using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.Services
{
    public class ChapterService : IChapterService
    {
        #region Dependency Injection
        private readonly IUnitOfWork _unitOfWork;

        public ChapterService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion

        public async Task<Chapter> GetLastSavedChapterByNovelIdAsync(Guid novelId)
        {
            return await _unitOfWork.Chapter.GetLastSavedChapterAsyncByNovelId(novelId);
        }
    }
}
