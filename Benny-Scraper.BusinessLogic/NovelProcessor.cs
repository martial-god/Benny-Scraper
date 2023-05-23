using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.BusinessLogic.Validators;
using Benny_Scraper.Models;
using Microsoft.Extensions.Options;
using NLog;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Benny_Scraper.BusinessLogic
{
    public class NovelProcessor : INovelProcessor
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private readonly INovelService _novelService;
        private readonly IChapterService _chapterService;
        private readonly INovelScraperFactory _novelScraper;
        private readonly NovelScraperSettings _novelScraperSettings;
        private readonly IEpubGenerator _epubGenerator;

        public NovelProcessor(INovelService novelService,
            IChapterService chapterService,
            INovelScraperFactory novelScraper,
            IOptions<NovelScraperSettings> novelScraperSettings,
            IEpubGenerator epubGenerator)
        {
            _novelService = novelService;
            _chapterService = chapterService;
            _novelScraper = novelScraper;
            _novelScraperSettings = novelScraperSettings.Value;
            _epubGenerator = epubGenerator;
        }        

        public async Task ProcessNovelAsync(Uri novelTableOfContentsUri)
        {

            if (!IsThereConfigurationForSite(novelTableOfContentsUri))
            {
                Logger.Error($"There is no configuration for site {novelTableOfContentsUri.Host}. Please check appsettings.json. an Stopping application.");
                return;
            }

            Novel novel = await _novelService.GetByUrlAsync(novelTableOfContentsUri);

            SiteConfiguration siteConfig = GetSiteConfiguration(novelTableOfContentsUri); // nullability check is done in IsThereConfigurationForSite.
                                                                                          // Retrieve novel information
            INovelScraper scraper = _novelScraper.CreateScraper(novelTableOfContentsUri, siteConfig);
            ScraperStrategy scraperStrategy = scraper.GetScraperStrategy(novelTableOfContentsUri, siteConfig);

            scraperStrategy.SetVariables(siteConfig, novelTableOfContentsUri);

            if (novel == null) // Novel is not in database so add it
            {
                Logger.Info($"Novel with url {novelTableOfContentsUri} is not in database, adding it now.");
                await AddNewNovelAsync(novelTableOfContentsUri, scraper, scraperStrategy, siteConfig);
                Logger.Info($"Added novel with url {novelTableOfContentsUri} to database.");
            }
            else // make changes or update novelToAdd and newChapters
            {
                ValidateObject validator = new ValidateObject();
                var errors = validator.Validate(novel);
                Logger.Info($"Novel {novel.Title} found with url {novelTableOfContentsUri} is in database, updating it now. Novel Id: {novel.Id}");
                await UpdateExistingNovelAsync(novel, novelTableOfContentsUri, scraper, siteConfig);
            }

        }



        public async Task<object> CreateEpubAsync(Novel novel, List<Models.Chapter> chapters, string outputPath)
        {
            // Reasearch Epub.net on how to make epubs, ex: https://github.com/Mitch528/WebNovelConverter/blob/master/WebNovelConverter/MainForm.cs
            return null;
        }

        private async Task AddNewNovelAsync(Uri novelTableOfContentsUri, INovelScraper scraper, ScraperStrategy scraperStrategy, SiteConfiguration siteConfig)
        {
            NovelData novelData = await scraperStrategy.ScrapeAsync();

            //NovelData novelData = await scraper.GetNovelDataAsync(novelTableOfContentsUri, siteConfig);
            if (novelData == null)
            {
                Logger.Error($"Failed to retrieve novel data from {novelTableOfContentsUri}");
                return;
            }

            //NovelData paginatedData = await scraper.RequestPaginatedDataAsync(novelTableOfContentsUri, siteConfig, lastSavedChapterUrl: null, true);
            //novelData.RecentChapterUrls = paginatedData.RecentChapterUrls;

            // Create a new Novel object and populate its properties
            Novel novelToAdd = new Novel
            {
                Title = novelData.Title,
                Author = novelData.Author,
                Url = novelTableOfContentsUri.ToString(),
                Genre = string.Join(", ", novelData.Genres),
                Description = string.Join(" ", novelData.Description),
                DateCreated = DateTime.Now,
                DateLastModified = DateTime.Now,
                Status = novelData.NovelStatus,
                LastTableOfContentsUrl = novelData.LastTableOfContentsPageUrl,
                LastChapter = novelData.IsNovelCompleted,
                CurrentChapter = novelData.MostRecentChapterTitle,
                SiteName = novelTableOfContentsUri.Host,
                FirstChapter = novelData.FirstChapter,
                CurrentChapterUrl = novelData.CurrentChapterUrl
            };

            IEnumerable<ChapterData> chapterDatas = await scraperStrategy.GetChaptersDataAsync(novelData.RecentChapterUrls);
            // Retrieve chapter information
            //IEnumerable<ChapterData> chapterDatas = await scraper.GetChaptersDataAsync(novelData.RecentChapterUrls, siteConfig);

            // Create Chapter objects and populate their properties
            List<Chapter> chaptersToAdd = chapterDatas.Select(data => new Chapter
            {
                NovelId = novelToAdd.Id,
                Url = data.Url ?? "",
                Content = data.Content ?? "",
                Title = data.Title ?? "",
                Number = data.Number,
                DateCreated = DateTime.Now,
                DateLastModified = data.DateLastModified
            }).ToList();

            // Add chapters to the novel
            novelToAdd.Chapters = chaptersToAdd;

            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            //create directory BennyScrapedNovels that will contain all the novels, then a folder for the novel title

            string fileRegex = @"[^a-zA-Z0-9-\s]";
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            var novelFileSafeTitle = textInfo.ToTitleCase(Regex.Replace(novelToAdd.Title, fileRegex, "").ToLower()).ToLowerInvariant();
            documentsFolder = Path.Combine(documentsFolder, "BennyScrapedNovels", novelFileSafeTitle, $"Read {novelFileSafeTitle}");
            Directory.CreateDirectory(documentsFolder);

            string epubFile = Path.Combine(documentsFolder, $"{novelFileSafeTitle}.epub");
            novelToAdd.SaveLocation = epubFile;

            // call createepub, but only pass newest
            _epubGenerator.CreateEpub(novelToAdd, novelToAdd.Chapters, epubFile);

            // Add the novel and its chapters to the database
            //await _novelService.CreateAsync(novelToAdd);
        }




        private async Task UpdateExistingNovelAsync(Novel novel, Uri novelTableOfContentsUri, INovelScraper scraper, SiteConfiguration siteConfig)
        {
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
                Logger.Warn($"{novel.Title} does not have a LastTableOfContentsPageUrl. Novel Id: {novel.Id}");

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
                    Logger.Warn($"{novel.Title} does not have a LastTableOfContentsPageUrl and no chapter was found in database. Novel Id: {novel.Id}");
                }
            }
            else
            {
                lastSavedChapterUrl = novel.CurrentChapterUrl;
            }

            NovelData novelData = new NovelData();

            if (siteConfig.HasPagination)
            {
                Uri lastTableOfContentsUrl = new Uri(novel.LastTableOfContentsUrl);

                int pageToStartAt = GetTableOfContentsPageToStartAt(lastTableOfContentsUrl, novel, siteConfig);
                novelData = await scraper.RequestPaginatedDataAsync(novelTableOfContentsUri, siteConfig, lastSavedChapterUrl, false, pageToStartAt);
            }

            IEnumerable<ChapterData> chapterDatas = await scraper.GetChaptersDataAsync(novelData.RecentChapterUrls, siteConfig);

            List<Models.Chapter> newChapters = chapterDatas.Select(data => new Models.Chapter
            {
                Url = data.Url ?? "",
                Content = data.Content ?? "",
                Title = data.Title ?? "",
                DateCreated = DateTime.Now,
                DateLastModified = DateTime.Now,
                Number = data.Number,

            }).ToList();

            novel.Chapters.AddRange(newChapters);
            novel.LastTableOfContentsUrl = (!string.IsNullOrEmpty(novelData.LastTableOfContentsPageUrl)) ? novelData.LastTableOfContentsPageUrl : novel.LastTableOfContentsUrl;
            novel.Status = (!string.IsNullOrEmpty(novelData.NovelStatus)) ? novelData.NovelStatus : novel.Status;
            novel.LastChapter = novelData.IsNovelCompleted;
            novel.DateLastModified = DateTime.Now;
            novel.TotalChapters = novel.Chapters.Count;
            novel.CurrentChapter = novel.Chapters.LastOrDefault().Title;

            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            //create directory BennyScrapedNovels that will contain all the novels, then a folder for the novel title

            string fileRegex = @"[^a-zA-Z0-9-\s]";
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            var novelFileSafeTitle = textInfo.ToTitleCase(Regex.Replace(novel.Title, fileRegex, " ").ToLower());
            documentsFolder = Path.Combine(documentsFolder, "BennyScrapedNovels", novelFileSafeTitle, $"Read {novelFileSafeTitle}");
            Directory.CreateDirectory(documentsFolder);

            string epubFile = Path.Combine(documentsFolder, $"{novelFileSafeTitle}.epub");

            //_epubGenerator.CreateEpub(novel, novel.Chapters, epubFile);

            // call createepub, but only pass newest
            _epubGenerator.CreateEpub(novel, newChapters, epubFile);

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
                Logger.Info($"{novel.Title} does not have a LastTableOfContentsPageUrl.\nCurrent Saved: {novel.LastTableOfContentsUrl}");
                return 1;
            }

            string pageString = novelTableOfContentsUrl.Query.Replace(siteConfig.PaginationQueryPartial, ""); //ex: page="2" so we remove page=
            int.TryParse(pageString, out int page);
            Logger.Info($"Table of content page to start at is {page}");
            return page;
        }
    }
}
