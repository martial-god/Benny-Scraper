using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.Services
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

        // CreateScraper new novel with a passed in novel
        public async Task<Guid> CreateAsync(Novel novel)
        {
            novel.DateLastModified = DateTime.Now;
            novel.TotalChapters = novel.Chapters.Count;
            await _unitOfWork.Novel.AddAsync(novel);
            
            //await _unitOfWork.Chapter.AddAsync(novel.Chapters.FirstOrDefault());
            await _unitOfWork.SaveAsync();
            return novel.Id;
        }

        /// <summary>
        /// Updates existing novel and adds collection of chapters
        /// </summary>
        /// <param name="novel"></param>
        /// <param name="newChapters"></param>
        /// <returns></returns>
        public async Task UpdateAndAddChaptersAsync(Novel novel, IEnumerable<Chapter> newChapters)
        {            
            _unitOfWork.Novel.Update(novel); //update existing

            _unitOfWork.Chapter.AddRange(newChapters);
            await _unitOfWork.SaveAsync();
        }

        /// <summary>
        /// Updates existing novel
        /// </summary>
        /// <param name="novel"></param>
        /// <returns></returns>
        public async Task UpdateAsync(Novel novel)
        {
            _unitOfWork.Novel.Update(novel);
            await _unitOfWork.SaveAsync();
        }

        public async Task<IEnumerable<Novel>> GetAllAsync()
        {
            return await _unitOfWork.Novel.GetAllAsync();
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

        public async Task<Novel> GetByIdAsync(Guid id)
        {
            var context = await _unitOfWork.Novel.GetByIdAsync(id);
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

        public async Task<bool> IsNovelInDatabaseAsync(Guid id)
        {
            var context = await _unitOfWork.Novel.GetFirstOrDefaultAsync(filter: c => c.Id == id);
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
            //if (pages != null)
            //    _unitOfWork.Page.RemoveRange(pages);
            _unitOfWork.Chapter.RemoveRange(chapters);
            _unitOfWork.Novel.Remove(novel);
            await _unitOfWork.SaveAsync();
        }
    }
}
