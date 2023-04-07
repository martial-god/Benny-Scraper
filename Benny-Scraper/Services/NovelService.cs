using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;
using Benny_Scraper.Services.Interface;

namespace Benny_Scraper.Services
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
            //await _unitOfWork.Chapter.AddAsync(novel.Chapters.FirstOrDefault());
            await _unitOfWork.SaveAsync();
        }

        /// <summary>
        /// Updates existing novel and adds collection of chapters
        /// </summary>
        /// <param name="novel"></param>
        /// <param name="newChapters"></param>
        /// <returns></returns>
        public async Task UpdateAndAddChapters(Novel novel, IEnumerable<Chapter> newChapters)
        {
            novel.DateLastModified = DateTime.UtcNow;
            novel.TotalChapters = novel.Chapters.Count;
            novel.CurrentChapter = novel.Chapters.LastOrDefault().Title;
            _unitOfWork.Novel.Update(novel); //update existing

            _unitOfWork.Chapter.AddRange(newChapters);
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

        public async Task RemoveAllAsync()
        {
            var allNovels = await _unitOfWork.Novel.GetAllAsync();
            var allChapters = await _unitOfWork.Chapter.GetAllAsync();

            _unitOfWork.Novel.RemoveRange(allNovels);
            _unitOfWork.Chapter.RemoveRange(allChapters);

            await _unitOfWork.SaveAsync();
        }

        public async Task RemoveByIdAsync(Guid id)
        {
            var novel = await _unitOfWork.Novel.GetByIdAsync(id);
            if (novel == null)
            {
                throw new InvalidOperationException("Novel not found.");
            }

            var chapters = await _unitOfWork.Chapter.GetAllAsync(filter: c => c.NovelId == id);
            _unitOfWork.Chapter.RemoveRange(chapters);
            _unitOfWork.Novel.Remove(novel);
            await _unitOfWork.SaveAsync();
        }


        public void ReportServiceLifetimeDetails(string lifetimeDetails)
        {
            Console.WriteLine(lifetimeDetails);
            Console.WriteLine("Changes only with lifetime");
        }
    }
}
