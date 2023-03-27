using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Interfaces;
using Benny_Scraper.Models;
using Benny_Scraper.Services.Interface;
using OpenQA.Selenium;
using System.Drawing.Text;

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

        public async Task ProcessNovelAsync(Uri novelTableOfContentsUri)
        {
            Novel novel = await GetByUrlAsync(novelTableOfContentsUri);

            if (novel == null) // Novel is not in database so add it
            {
                await AddNewNovelAsync(novelTableOfContentsUri);
            }
            else // make changes or update novelToAdd and newChapters
            {
                await UpdateExistingNovelAsync(novel, novelTableOfContentsUri);
            }            
            
        }
        
        private async Task AddNewNovelAsync(Uri novelTableOfContentsUri)
        {
            IDriverFactory driverFactory = new DriverFactory(); // Instantiating an interface https://softwareengineering.stackexchange.com/questions/167808/instantiating-interfaces-in-c
            Task<IWebDriver> driver = driverFactory.CreateDriverAsync(1, true, "https://google.com");
            NovelPage novelPage = new NovelPage(driver.Result);
            Novel novelToAdd = await novelPage.BuildNovelAsync(novelTableOfContentsUri);
            await CreateAsync(novelToAdd);
            driverFactory.DisposeAllDrivers();
        }
        
        private async Task UpdateExistingNovelAsync(Novel novel, Uri novelTableOfContentsUri)
        {
            var currentChapter = novel?.CurrentChapter;
            var chapterContext = novel?.Chapters;

            if (currentChapter == null || chapterContext == null)
                return;


            INovelPageScraper novelPageScraper = new NovelPageScraper();
            var latestChapter = await novelPageScraper.GetLatestChapterAsync("//ul[@class='l-chapters']//a", novelTableOfContentsUri);
            bool isCurrentChapterNewest = string.Equals(currentChapter, latestChapter, comparisonType: StringComparison.OrdinalIgnoreCase);

            if (isCurrentChapterNewest)
            {
                Logger.Log.Info($"{novel.Title} is currently at the latest chapter.\nCurrent Saved: {novel.CurrentChapter}");
                return;
            }


            // get all newChapters after the current chapter up to the latest
            if (string.IsNullOrEmpty(novel.LastTableOfContentsUrl))
            {
                Logger.Log.Info($"{novel.Title} does not have a LastTableOfContentsUrl.\nCurrent Saved: {novel.LastTableOfContentsUrl}");
                return;
            }

            Uri lastTableOfContentsUrl = new Uri(novel.LastTableOfContentsUrl);
            var latestChapterData = await novelPageScraper.GetChaptersFromCheckPointAsync("//ul[@class='list-chapter']//a/@href", lastTableOfContentsUrl, novel.CurrentChapter);
            IEnumerable<ChapterData> chapterData = await novelPageScraper.GetChaptersDataAsync(latestChapterData.LatestChapterUrls, "//span[@class='chapter-text']", "//div[@id='chapter']", novel.Title);

            List<Chapter> newChapters = chapterData.Select(data => new Chapter
            {
                Url = data.Url ?? "",
                Content = data.Content ?? "",
                Title = data.Title ?? "",
                DateCreated = DateTime.UtcNow,
                DateLastModified = DateTime.UtcNow,
                Number = data.Number,

            }).ToList();
            novel.LastTableOfContentsUrl = latestChapterData.LastTableOfContentsUrl;
            novel.Status = latestChapterData.Status;

            novel.Chapters.AddRange(newChapters);
            await UpdateAndAddChapters(novel, newChapters);
        }

    }
}
