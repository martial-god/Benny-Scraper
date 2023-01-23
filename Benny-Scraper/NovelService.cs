﻿using Benny_Scraper.DataAccess.Repository.IRepository;
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
            if (novel.Chapters == null)
            {
                throw new ArgumentNullException(nameof(novel));
            }
            await _unitOfWork.Novel.AddAsync(novel);
            await _unitOfWork.Chapter.AddAsync(novel.Chapters.FirstOrDefault());
            await _unitOfWork.SaveAsync();
        }

        /// <summary>
        /// Check for novel in database
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