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
                await AddNewNovelAsync(novelTableOfContentsUri, scraperStrategy);
                Logger.Info($"Added novel with url {novelTableOfContentsUri} to database.");
            }
            else // make changes or update novelToAdd and newChapters
            {
                ValidateObject validator = new ValidateObject();
                var errors = validator.Validate(novel);
                Logger.Info($"Novel {novel.Title} found with url {novelTableOfContentsUri} is in database, updating it now. Novel Id: {novel.Id}");
                await UpdateExistingNovelAsync(novel, novelTableOfContentsUri, scraperStrategy);
            }

        }

        private async Task AddNewNovelAsync(Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy)
        {
            NovelData novelData = await scraperStrategy.ScrapeAsync();

            if (novelData == null)
            {
                Logger.Error($"Failed to retrieve novel data from {novelTableOfContentsUri}");
                return;
            }

            // Create a new Novel object and populate its properties
            Novel novelToAdd = new Novel
            {
                Title = novelData.Title ?? string.Empty,
                Author = novelData.Author,
                Url = novelTableOfContentsUri.ToString() ?? string.Empty,
                Genre = string.Join(", ", novelData.Genres),
                Description = string.Join(" ", novelData.Description),
                DateCreated = DateTime.Now,
                DateLastModified = DateTime.Now,
                Status = novelData.NovelStatus,
                LastTableOfContentsUrl = novelData.LastTableOfContentsPageUrl,
                LastChapter = novelData.IsNovelCompleted,
                CurrentChapter = novelData.MostRecentChapterTitle ?? string.Empty,
                SiteName = novelTableOfContentsUri.Host ?? string.Empty,
                FirstChapter = novelData.FirstChapter ?? string.Empty,
                CurrentChapterUrl = novelData.CurrentChapterUrl ?? string.Empty
            };
            Logger.Info("Finished populating Novel date for {0}", novelToAdd.Title);
            IEnumerable<ChapterData> chapterDatas = await scraperStrategy.GetChaptersDataAsync(novelData.ChapterUrls);

            // Create Chapter objects and populate their properties
            List<Chapter> chaptersToAdd = chapterDatas.Select(data => new Chapter
            {
                NovelId = novelToAdd.Id,
                Url = data.Url ?? string.Empty,
                Content = data.Content ?? string.Empty,
                Title = data.Title ?? string.Empty,
                Number = data.Number,
                DateCreated = DateTime.Now,
                DateLastModified = data.DateLastModified
            }).ToList();

            // Add chapters to the novel
            novelToAdd.Chapters = chaptersToAdd;

            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string fileRegex = @"[^a-zA-Z0-9-\s]";
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            var novelFileSafeTitle = textInfo.ToTitleCase(Regex.Replace(novelToAdd.Title, fileRegex, string.Empty).ToLower().ToLowerInvariant());
            documentsFolder = Path.Combine(documentsFolder, "BennyScrapedNovels", novelFileSafeTitle);
            Directory.CreateDirectory(documentsFolder);

            string epubFile = Path.Combine(documentsFolder, $"{novelFileSafeTitle}.epub");
            novelToAdd.SaveLocation = epubFile;

            _epubGenerator.CreateEpub(novelToAdd, novelToAdd.Chapters, epubFile, novelData.ThumbnailImage);

            await _novelService.CreateAsync(novelToAdd);
        }

        private async Task UpdateExistingNovelAsync(Novel novel, Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy)
        {
            NovelData novelData = await scraperStrategy.ScrapeAsync();

            if (novelData == null)
            {
                Logger.Error($"Failed to retrieve novel data from {novelTableOfContentsUri}");
                return;
            }

            IEnumerable<ChapterData> chapterDatas = await scraperStrategy.GetChaptersDataAsync(novelData.ChapterUrls);

            List<Models.Chapter> newChapters = chapterDatas.Select(data => new Models.Chapter
            {
                Url = data.Url ?? string.Empty,
                Content = data.Content ?? string.Empty,
                Title = data.Title ?? string.Empty,
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
            var novelFileSafeTitle = textInfo.ToTitleCase(Regex.Replace(novel.Title, fileRegex, " ").ToLower().ToLowerInvariant());
            documentsFolder = Path.Combine(documentsFolder, "BennyScrapedNovels", novelFileSafeTitle);
            Directory.CreateDirectory(documentsFolder);

            string epubFile = Path.Combine(documentsFolder, $"{novelFileSafeTitle}.epub");

            _epubGenerator.CreateEpub(novel, newChapters, epubFile, novelData.ThumbnailImage);

            await _novelService.UpdateAndAddChapters(novel, newChapters);
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
    }
}
