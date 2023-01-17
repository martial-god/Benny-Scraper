using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper
{
    internal class StartUp : IStartUp
    {
        private readonly IUnitOfWork _unitOfWork;

        public StartUp(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // Create new novel with a passed in novel
        public void CreateNovel(Novel novel)
        {
            _unitOfWork.Novel.Add(novel);
            _unitOfWork.SaveAsync();
        }

        public void ReportServiceLifetimeDetails(string lifetimeDetails)
        {
            Console.WriteLine(lifetimeDetails);
            Console.WriteLine("Changes only with lifetime");
        }
        
    }
}
