using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper
{
    public class StartUpService : IStartUpService
    {
        private readonly IUnitOfWork _unitOfWork;

        public StartUpService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // Create new novel with a passed in novel
        public async Task CreateNovelAsync(Novel novel)
        {
            var foo =  _unitOfWork.Novel.GetAll();
            await _unitOfWork.Novel.AddAsync(novel);
            await _unitOfWork.SaveAsync();
        }

        

        public void ReportServiceLifetimeDetails(string lifetimeDetails)
        {
            Console.WriteLine(lifetimeDetails);
            Console.WriteLine("Changes only with lifetime");
        }
        
    }
}
