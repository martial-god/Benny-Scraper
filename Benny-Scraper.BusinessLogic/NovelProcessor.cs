using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.BusinessLogic.Validators;
using Benny_Scraper.Models;
using Microsoft.Extensions.Options;
using NLog;

namespace Benny_Scraper.BusinessLogic
{
    public class NovelProcessor : INovelProcessor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly INovelService _novelService;
        private readonly IChapterService _chapterService;
        private readonly INovelScraperFactory _novelScraper;
        private readonly NovelScraperSettings _novelScraperSettings;

        public NovelProcessor(INovelService novelService, IChapterService chapterService, INovelScraperFactory novelScraper, IOptions<NovelScraperSettings> novelScraperSettings)
        {
            _novelService = novelService;
            _chapterService = chapterService;
            _novelScraper = novelScraper;
            _novelScraperSettings = novelScraperSettings.Value;
        }

        public async Task ProcessNovelAsync(Uri novelTableOfContentsUri)
        {
            if (!IsThereConfigurationForSite(novelTableOfContentsUri))
            {
                Logger.Error($"There is no configuration for site {novelTableOfContentsUri.Host}. Stopping application.");
                return;
            }

            Novel novel = await _novelService.GetByUrlAsync(novelTableOfContentsUri);

            ValidateObject validator = new ValidateObject();
            var errors = validator.Validate(novel);

            INovelScraper scraper = _novelScraper.CreateSeleniumOrHttpScraper(novelTableOfContentsUri);

            if (novel == null) // Novel is not in database so add it
            {
                Logger.Info($"Novel with url {novelTableOfContentsUri} is not in database, adding it now.");
                await AddNewNovelAsync(novelTableOfContentsUri, scraper);
            }
            else // make changes or update novelToAdd and newChapters
            {
                Logger.Info($"Novel {novel.Title} found with url {novelTableOfContentsUri} is in database, updating it now. Novel Id: {novel.Id}");
                await UpdateExistingNovelAsync(novel, novelTableOfContentsUri, scraper);
            }

        }



        public async Task<object> CreateEpubAsync(Novel novel, List<Models.Chapter> chapters, string outputPath)
        {
            // Reasearch Epub.net on how to make epubs, ex: https://github.com/Mitch528/WebNovelConverter/blob/master/WebNovelConverter/MainForm.cs
            return null;
        }

        private async Task AddNewNovelAsync(Uri novelTableOfContentsUri, INovelScraper scraper)
        {
            //IDriverFactory driverFactory = new DriverFactory(); // Instantiating an interface https://softwareengineering.stackexchange.com/questions/167808/instantiating-interfaces-in-c
            //Task<IWebDriver> driver = driverFactory.CreateDriverAsync(1, true, "https://google.com");
            //NovelPage novelPage = new NovelPage(driver.Result);
            //Novel novelToAdd = await novelPage.BuildNovelAsync(novelTableOfContentsUri);
            //await _novelService.CreateAsync(novelToAdd);
            //driverFactory.DisposeAllDrivers();
            return;
        }

        private async Task UpdateExistingNovelAsync(Novel novel, Uri novelTableOfContentsUri, INovelScraper scraper)
        {
            SiteConfiguration siteConfig = GetSiteConfiguration(novelTableOfContentsUri); // nullability check is done in IsThereConfigurationForSite.
            string lastSavedChapterUrl = string.Empty;

            string latestChapter = await scraper.GetLatestChapterNameAsync(novelTableOfContentsUri, siteConfig);
            bool isCurrentChapterNewest = string.Equals(novel.CurrentChapter, latestChapter, comparisonType: StringComparison.OrdinalIgnoreCase);

            if (isCurrentChapterNewest)
            {
                Logger.Info($"{novel.Title} is currently at the latest chapter. Current Saved: {novel.CurrentChapter}. Novel Id: {novel.Id}");
                return;
            }

            // get all newChapters after the current chapter up to the latest
            if (string.IsNullOrEmpty(novel.LastTableOfContentsUrl))
            {
                Logger.Warn($"{novel.Title} does not have a LastTableOfContentsUrl. Novel Id: {novel.Id}");

                Chapter lastSavedChapter = await _chapterService.GetLastSavedChapterByNovelIdAsync(novel.Id);

                if (lastSavedChapter != null)
                {
                    lastSavedChapterUrl = novel.CurrentChapterUrl ?? string.Empty;
                    int.TryParse(lastSavedChapter.Number, out int chapterNumber);
                    var lazyTableOfContentsPage = chapterNumber / siteConfig.ChaptersPerPage;
                    var newPageQuery = string.Format(siteConfig.PaginationType, lazyTableOfContentsPage.ToString());

                    novel.LastTableOfContentsUrl = novelTableOfContentsUri.AbsoluteUri + newPageQuery;
                }
                else // No novel found should still be okay to proceed, just get all chapters from beginning. Might need to remove older chapters.
                {
                    Logger.Warn($"{novel.Title} does not have a LastTableOfContentsUrl and no chapter was found in database. Novel Id: {novel.Id}");
                }
            }
            else
            {
                lastSavedChapterUrl = novel.CurrentChapterUrl;
            }

            List<string> newChapterUrls = new List<string>();

            if (siteConfig.HasPagination)
            {
                Uri lastTableOfContentsUrl = new Uri(novel.LastTableOfContentsUrl);

                int pageToStartAt = GetTableOfContentsPageToStartAt(lastTableOfContentsUrl, novel, siteConfig);
                newChapterUrls = await scraper.BuildNovelDataFromTableOfContentUsingPaginationAsync(pageToStartAt, novelTableOfContentsUri, siteConfig, lastSavedChapterUrl);
            }

            
            //var latestChapterData = await novelPageScraper.GetChaptersFromCheckPointAsync("//ul[@class='list-chapter']//a/@href", lastTableOfContentsUrl, novel.CurrentChapter);
            //IEnumerable<ChapterData> chapterData = await novelPageScraper.GetChaptersDataAsync(latestChapterData.LatestChapterUrls, "//span[@class='chapter-text']", "//div[@id='chapter']", novel.Title);

            //List<Models.Chapter> newChapters = chapterData.Select(data => new Models.Chapter
            //{
            //    Url = data.Url ?? "",
            //    Content = data.Content ?? "",
            //    Title = data.Title ?? "",
            //    DateCreated = DateTime.UtcNow,
            //    DateLastModified = DateTime.UtcNow,
            //    Number = data.Number,

            //}).ToList();
            //novel.LastTableOfContentsUrl = latestChapterData.LastTableOfContentsUrl;
            //novel.Status = latestChapterData.Status;

            //novel.Chapters.AddRange(newChapters);
            //await _novelService.UpdateAndAddChapters(novel, newChapters);
        }

        private bool IsThereConfigurationForSite(Uri novelTableOfContentsUri)
        {
            List<SiteConfiguration> siteConfigurations = _novelScraperSettings.SiteConfigurations;
            return siteConfigurations.Any(config => novelTableOfContentsUri.Host.Contains(config.UrlPattern));
        }

        private SiteConfiguration GetSiteConfiguration(Uri novelTableOfContentsUri)
        {
            List<SiteConfiguration> siteConfigurations = _novelScraperSettings.SiteConfigurations;
            return siteConfigurations.FirstOrDefault(config => novelTableOfContentsUri.Host.Contains(config.UrlPattern));
        }

        /// <summary>
        /// Gets the page number to start at when scraping chapters from a table of contents.
        /// </summary>
        /// <param name="novelTableOfContentsUrl"></param>
        /// <param name="novel"></param>
        /// <param name="siteConfig"></param>
        /// <returns>int of the page number</returns>
        private int GetTableOfContentsPageToStartAt(Uri novelTableOfContentsUrl, Novel novel, SiteConfiguration siteConfig)
        {
            if (novelTableOfContentsUrl == null || string.IsNullOrEmpty(novelTableOfContentsUrl.ToString()))
            {
                Logger.Info($"{novel.Title} does not have a LastTableOfContentsUrl.\nCurrent Saved: {novel.LastTableOfContentsUrl}");
                return 1;
            }


            string pageString = novelTableOfContentsUrl.Query.Replace(siteConfig.PaginationQueryPartial, ""); //ex: page="2" so we remove page=
            int.TryParse(pageString, out int page);
            Logger.Info($"Table of content page to start at is {page}");
            return page;
        }
    }
}
