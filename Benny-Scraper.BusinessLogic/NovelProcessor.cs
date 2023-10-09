using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.FileGenerators;
using Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;
using Benny_Scraper.BusinessLogic.Helper;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.BusinessLogic.Validators;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using NLog;
using Configuration = Benny_Scraper.Models.Configuration;

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
        private readonly PdfGenerator _pdfGenerator;
        private readonly IConfigurationRepository _configurationRepository;

        public NovelProcessor(INovelService novelService,
            IChapterService chapterService,
            INovelScraperFactory novelScraper,
            IOptions<NovelScraperSettings> novelScraperSettings,
            IEpubGenerator epubGenerator,
            PdfGenerator pdfGenerator,
            IConfigurationRepository configurationRepository)
        {
            _novelService = novelService;
            _chapterService = chapterService;
            _novelScraper = novelScraper;
            _novelScraperSettings = novelScraperSettings.Value;
            _epubGenerator = epubGenerator;
            _pdfGenerator = pdfGenerator;
            _configurationRepository = configurationRepository;
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
            Configuration configuration = await _configurationRepository.GetByIdAsync(1);
            ScraperStrategy scraperStrategy = scraper.GetScraperStrategy(novelTableOfContentsUri, siteConfig);

            scraperStrategy.SetVariables(siteConfig, novelTableOfContentsUri, configuration);

            if (novel == null) // Novel is not in database so add it
            {
                Logger.Info($"Novel with url {novelTableOfContentsUri} is not in database, adding it now.");
                await AddNewNovelAsync(novelTableOfContentsUri, scraperStrategy, configuration);
                Logger.Info($"Added novel with url {novelTableOfContentsUri} to database.");
            }
            else // make changes or update novelToAdd and newChapters
            {
                ValidateObject validator = new ValidateObject();
                var errors = validator.Validate(novel);
                Logger.Info($"Novel {novel.Title} found with url {novelTableOfContentsUri} is in database, updating it now. Novel Id: {novel.Id}");
                await UpdateExistingNovelAsync(novel, novelTableOfContentsUri, scraperStrategy, configuration);
            }

        }

        #region Private Methods
        private async Task AddNewNovelAsync(Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy, Configuration configuration)
        {
            using NovelDataBuffer novelDataBuffer = await scraperStrategy.ScrapeAsync();

            if (novelDataBuffer == null)
            {
                Logger.Error($"Failed to retrieve novel data from {novelTableOfContentsUri}");
                return;
            }

            Novel newNovel = CreateNovel(novelDataBuffer, novelTableOfContentsUri);
            Logger.Info("Finished populating Novel data for {0}", newNovel.Title);

            IEnumerable<ChapterDataBuffer> chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(novelDataBuffer.ChapterUrls);
            newNovel.Chapters = CreateChapters(chapterDataBuffers, newNovel.Id);

            var userOutputDirectory = configuration.DetermineSaveLocation((bool)(scraperStrategy.GetSiteConfiguration()?.HasImagesForChapterContent));
            string outputDirectory = CommonHelper.GetOutputDirectoryForTitle(newNovel.Title, outputDirectory = userOutputDirectory);

            var novelId = await _novelService.CreateAsync(newNovel);
            Logger.Info("Finished adding novel {0} to database", newNovel.Title);
            Novel novel = await GetNovelFromDataBase(novelId);

            Logger.Info($"Novel {novel.Title} found with url {novelTableOfContentsUri} is in database, updating it now. Novel Id: {novel.Id}");
            if (novel.Chapters.Any(chapter => chapter?.Pages != null))
            {
                if (string.IsNullOrEmpty(novel.SaveLocation))
                    novel.SaveLocation = Path.Combine(outputDirectory, CommonHelper.SanitizeFileName(novel.Title) + PdfGenerator.PdfFileExtension);
                _pdfGenerator.CreatePdf(novel, chapterDataBuffers, outputDirectory, configuration);
                foreach (var chapterDataBuffer in chapterDataBuffers)
                {
                    chapterDataBuffer.Dispose();
                }
            }
            else
            {
                novel.SaveLocation = CreateEpub(novel, novelDataBuffer.ThumbnailImage, outputDirectory);
            }
            await _novelService.UpdateAsync(novel);
        }

        private async Task UpdateExistingNovelAsync(Novel novel, Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy, Configuration configuration)
        {
            using NovelDataBuffer novelDataBuffer = await scraperStrategy.ScrapeAsync();

            if (novelDataBuffer == null)
            {
                Logger.Error($"Failed to retrieve novel data from {novelTableOfContentsUri}");
                return;
            }

            if (novel.CurrentChapterUrl == novelDataBuffer.CurrentChapterUrl && novel.CurrentChapter == novelDataBuffer.MostRecentChapterTitle)
            {
                Logger.Warn($"Novel {novel.Title} with url {novelTableOfContentsUri} is up to date.\n\t\tCurrent chapter: {novelDataBuffer.MostRecentChapterTitle} Novel Id: {novel.Id}");
                return;
            }

            novel.Chapters = novel.Chapters.OrderBy(chapter => chapter.Number).ToList(); //order chapters by number
            var indexOfLastChapter = novelDataBuffer.ChapterUrls.IndexOf(novel.CurrentChapterUrl);
            if (indexOfLastChapter == -1)
                indexOfLastChapter = novelDataBuffer.ChapterUrls.IndexOf(novel.Chapters.Last().Url);
            if (indexOfLastChapter == -1)
            {
                Logger.Error($"A case where the last chapter is not in the database and the current chapter is not in the database has been found. Novel Id: {novel.Id}");
                var getDllLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var getDllDir = System.IO.Path.GetDirectoryName(getDllLocation);
                var mainDll = System.IO.Path.Combine(getDllDir, "Benny-Scraper.dll");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                if (System.IO.File.Exists(mainDll))
                {
                    Console.Write($"Please delete the novel from the database using\n\t\t{mainDll} delete_novel_by_id {novel.Id} and try again.");
                }
                else
                {
                    Console.Write($"Please delete the novel from the database using\n\t\tBenny-Scraper delete_novel_by_id {novel.Id} and try again.");
                }
                Console.ResetColor();

            }
            var newChapterUrls = novelDataBuffer.ChapterUrls.Skip(indexOfLastChapter + 1).ToList();

            IEnumerable<ChapterDataBuffer> chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(newChapterUrls);
            List<Models.Chapter> newChapters = CreateChapters(chapterDataBuffers, novel.Id);

            UpdateNovel(novel, novelDataBuffer, newChapters);

            var userOutputDirectory = configuration.DetermineSaveLocation((bool)(scraperStrategy.GetSiteConfiguration()?.HasImagesForChapterContent));
            string outputDirectory = CommonHelper.GetOutputDirectoryForTitle(novel.Title, userOutputDirectory);

            if (newChapters.Any(chapter => chapter?.Pages != null))
            {
                if (string.IsNullOrEmpty(novel.SaveLocation))
                    novel.SaveLocation = Path.Combine(outputDirectory, CommonHelper.SanitizeFileName(novel.Title) + PdfGenerator.PdfFileExtension);
                _pdfGenerator.UpdatePdf(novel, chapterDataBuffers, configuration);
                foreach (var chapterDataBuffer in chapterDataBuffers)
                {
                    chapterDataBuffer.Dispose();
                }
                await _novelService.UpdateAndAddChapters(novel, newChapters); //to avoid issues where the database is updated but the pdf is not
            }
            else
            {
                novel.SaveLocation = CreateEpub(novel, novelDataBuffer.ThumbnailImage, outputDirectory);
                await _novelService.UpdateAndAddChapters(novel, newChapters);
            }
        }

        private async Task<Novel> GetNovelFromDataBase(Guid id)
        {
            Novel novel = await _novelService.GetByIdAsync(id);
            if (novel != null)
                novel.Chapters = novel.Chapters.OrderBy(chapter => chapter.Number).ToList();
            return novel;
        }

        private string CreateEpub(Novel novel, byte[]? thumbnailImage, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            string epubFile = Path.Combine(outputDirectory, $"{CommonHelper.SanitizeFileName(novel.Title, true)}.epub");
            _epubGenerator.CreateEpub(novel, novel.Chapters, epubFile, thumbnailImage);
            return epubFile;
        }

        private void UpdateNovel(Novel novel, NovelDataBuffer novelDataBuffer, List<Models.Chapter> newChapters)
        {
            novel.Chapters.AddRange(newChapters);
            novel.LastTableOfContentsUrl = (!string.IsNullOrEmpty(novelDataBuffer.LastTableOfContentsPageUrl)) ? novelDataBuffer.LastTableOfContentsPageUrl : novel.LastTableOfContentsUrl;
            novel.Status = (!string.IsNullOrEmpty(novelDataBuffer.NovelStatus)) ? novelDataBuffer.NovelStatus : novel.Status;
            novel.LastChapter = novelDataBuffer.IsNovelCompleted;
            novel.DateLastModified = DateTime.Now;
            novel.TotalChapters = novel.Chapters.Count;
            novel.CurrentChapter = novel.Chapters.LastOrDefault()?.Title;
            novel.CurrentChapterUrl = novel.Chapters.LastOrDefault()?.Url;
        }

        private Novel CreateNovel(NovelDataBuffer novelDataBuffer, Uri novelTableOfContentsUri)
        {
            return new Novel
            {
                Title = novelDataBuffer.Title ?? string.Empty,
                Author = novelDataBuffer.Author,
                Url = novelTableOfContentsUri.ToString(),
                Genre = string.Join(", ", novelDataBuffer.Genres),
                Description = string.Join(" ", novelDataBuffer.Description),
                DateCreated = DateTime.Now,
                DateLastModified = DateTime.Now,
                Status = novelDataBuffer.NovelStatus,
                LastTableOfContentsUrl = novelDataBuffer.LastTableOfContentsPageUrl,
                LastChapter = novelDataBuffer.IsNovelCompleted,
                CurrentChapter = novelDataBuffer.MostRecentChapterTitle ?? string.Empty,
                SiteName = novelTableOfContentsUri.Host ?? string.Empty,
                FirstChapter = novelDataBuffer.FirstChapter ?? string.Empty,
                CurrentChapterUrl = novelDataBuffer.CurrentChapterUrl ?? string.Empty
            };
        }

        private List<Chapter> CreateChapters(IEnumerable<ChapterDataBuffer> chapterDataBuffers, Guid novelId)
        {
            return chapterDataBuffers.Select(data => new Chapter
            {
                NovelId = novelId,
                Url = data.Url ?? string.Empty,
                Content = HtmlEntity.DeEntitize(data.Content),
                Title = HtmlEntity.DeEntitize(data.Title) ?? string.Empty,
                Number = data.Number,
                Pages = data.Pages?.Select(p => new Page
                {
                    Url = p.Url,
                    Image = null,
                }).ToList(),
                DateCreated = DateTime.Now,
                DateLastModified = data.DateLastModified
            }).ToList();
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
        #endregion
    }
}
