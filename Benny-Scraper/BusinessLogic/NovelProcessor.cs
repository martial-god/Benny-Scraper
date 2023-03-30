using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.Interfaces;
using Benny_Scraper.Models;
using Benny_Scraper.Services.Interface;
using OpenQA.Selenium;

namespace Benny_Scraper.BusinessLogic
{
    public class NovelProcessor : INovelProcessor
    {
        private readonly INovelService _novelService;

        public NovelProcessor(INovelService novelService)
        {
            _novelService = novelService;
        }

        public async Task ProcessNovelAsync(Uri novelTableOfContentsUri)
        {
            Novel novel = await _novelService.GetByUrlAsync(novelTableOfContentsUri);

            if (novel == null) // Novel is not in database so add it
            {
                await AddNewNovelAsync(novelTableOfContentsUri);
            }
            else // make changes or update novelToAdd and newChapters
            {
                await UpdateExistingNovelAsync(novel, novelTableOfContentsUri);
            }

        }

        public async Task<object> CreateEpubAsync(Novel novel, List<Models.Chapter> chapters, string outputPath)
        {
            // Reasearch Epub.net on how to make epubs, ex: https://github.com/Mitch528/WebNovelConverter/blob/master/WebNovelConverter/MainForm.cs
            return null;
        }

        private async Task AddNewNovelAsync(Uri novelTableOfContentsUri)
        {
            IDriverFactory driverFactory = new DriverFactory(); // Instantiating an interface https://softwareengineering.stackexchange.com/questions/167808/instantiating-interfaces-in-c
            Task<IWebDriver> driver = driverFactory.CreateDriverAsync(1, true, "https://google.com");
            NovelPage novelPage = new NovelPage(driver.Result);
            Novel novelToAdd = await novelPage.BuildNovelAsync(novelTableOfContentsUri);
            await _novelService.CreateAsync(novelToAdd);
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

            List<Models.Chapter> newChapters = chapterData.Select(data => new Models.Chapter
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
            await _novelService.UpdateAndAddChapters(novel, newChapters);
        }
    }
}
