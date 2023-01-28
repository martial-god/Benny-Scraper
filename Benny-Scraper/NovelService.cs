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
        public async Task CreateAsync(Novel novel)
        {
            novel.DateLastModified = DateTime.UtcNow;
            await _unitOfWork.Novel.AddAsync(novel);
            await _unitOfWork.Chapter.AddAsync(novel.Chapters.FirstOrDefault());
            await _unitOfWork.SaveAsync();
        }

        public async Task UpdateAsync(Novel novel)
        {
            novel.DateLastModified = DateTime.UtcNow;            
            _unitOfWork.Novel.Update(novel);
            _unitOfWork.Chapter.Update(novel.Chapters.FirstOrDefault());
            await _unitOfWork.SaveAsync();
        }
        public async Task<Novel> GetByUrlAsync(Uri uri)
        {
            var context = await _unitOfWork.Novel.GetFirstOrDefaultAsync(filter: c => c.Url == uri.OriginalString);
            if (context != null)
            {
                var chapterContext = await _unitOfWork.Chapter.GetAllAsync(filter: c => c.NovelId == context.Id);
                context.Chapters = chapterContext.ToList();
            }
            return context;
        }

        /// <summary>
        /// Check for novel in database by url
        /// </summary>
        /// <param name="tableOfContentsUrl">url of the table of contents page of the novel</param>
        /// <returns></returns>
        public async Task<bool> IsNovelInDatabaseAsync(string tableOfContentsUrl)
        {
            var context = await _unitOfWork.Novel.GetFirstOrDefaultAsync(filter: c => c.Url == tableOfContentsUrl);
            if (context == null)
                return false;
            return true;
        }

        public void ReportServiceLifetimeDetails(string lifetimeDetails)
        {
            Console.WriteLine(lifetimeDetails);
            Console.WriteLine("Changes only with lifetime");
        }
        
    }
}
