using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper
{
    public class NovelService : INovelService
    {
        #region Dependency Injection
        private readonly IUnitOfWork _unitOfWork;

        public NovelService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        #endregion
        // Create new novel with a passed in novel
        public async Task CreateNovelAsync(Novel novel)
        {
            var foo =  _unitOfWork.Novel.GetAll();
            await _unitOfWork.Novel.AddAsync(novel);
            await _unitOfWork.Chapter.AddAsync(novel.Chapters.First());
            await _unitOfWork.SaveAsync();
        }

        

        public void ReportServiceLifetimeDetails(string lifetimeDetails)
        {
            Console.WriteLine(lifetimeDetails);
            Console.WriteLine("Changes only with lifetime");
        }
        
    }
}
