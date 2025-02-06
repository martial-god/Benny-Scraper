using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;
using OpenQA.Selenium;

namespace Benny_Scraper.BusinessLogic.Services
{
    public class NovelService(IUnitOfWork unitOfWork) : INovelService
    {
        public async Task<Guid> CreateAsync(Novel novel)
        {
            novel.DateLastModified = DateTime.Now;
            novel.TotalChapters = novel.Chapters.Count;
            await unitOfWork.Novel.AddAsync(novel);
            
            await unitOfWork.SaveAsync();
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
            unitOfWork.Novel.Update(novel); //update existing
            await unitOfWork.SaveAsync();
        }

        /// <summary>
        /// Updates existing novel
        /// </summary>
        /// <param name="novel"></param>
        /// <returns></returns>
        public async Task UpdateAsync(Novel novel)
        {
            novel.DateLastModified = DateTime.Now;
            unitOfWork.Novel.Update(novel);
            await unitOfWork.SaveAsync();
        }

        public async Task<IEnumerable<Novel>> GetAllAsync()
        {
            return await unitOfWork.Novel.GetAllAsync();
        }

        public async Task<Novel?> GetByUrlAsync(Uri uri)
        {
            var context = await unitOfWork.Novel.GetFirstOrDefaultAsync(filter: c => c.Url == uri.OriginalString);
            if (context is null)
                return null;
            
            var chapterContext = await unitOfWork.Chapter.GetAllAsync(filter: c => c.NovelId == context.Id);
            context.Chapters = chapterContext.ToList();
            return context;
        }

        public async Task<Novel?> GetByIdAsync(Guid id)
        {
            var context = await unitOfWork.Novel.GetByIdAsync(id);
            if (context != null)
            {
                var chapterContext = await unitOfWork.Chapter.GetAllAsync(filter: c => c.NovelId == context.Id);
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
            var context = await unitOfWork.Novel.GetFirstOrDefaultAsync(filter: c => c.Url == tableOfContentsUrl);
            if (context == null)
                return false;
            return true;
        }

        public async Task<bool> IsNovelInDatabaseAsync(Guid id)
        {
            var context = await unitOfWork.Novel.GetFirstOrDefaultAsync(filter: c => c.Id == id);
            if (context == null)
                return false;
            return true;
        }

        public async Task RemoveAllAsync()
        {
            var allNovels = await unitOfWork.Novel.GetAllAsync();
            var allChapters = await unitOfWork.Chapter.GetAllAsync();

            unitOfWork.Novel.RemoveRange(allNovels);
            unitOfWork.Chapter.RemoveRange(allChapters);

            await unitOfWork.SaveAsync();
        }

        public async Task RemoveByIdAsync(Guid id)
        {
            var novel = await unitOfWork.Novel.GetByIdAsync(id);
            if (novel == null)
                throw new NotFoundException("Novel not found.");

            var chapters = await unitOfWork.Chapter.GetAllAsync(filter: c => c.NovelId == id);
            unitOfWork.Chapter.RemoveRange(chapters);
            unitOfWork.Novel.Remove(novel);
            await unitOfWork.SaveAsync();
        }
    }
}
