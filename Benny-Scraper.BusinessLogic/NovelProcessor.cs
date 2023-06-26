using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.BusinessLogic.Validators;
using Benny_Scraper.Models;
using Microsoft.Extensions.Options;
using NLog;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
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

        #region Private Methods
        private async Task AddNewNovelAsync(Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy)
        {
            NovelData novelData = await scraperStrategy.ScrapeAsync();

            if (novelData == null)
            {
                Logger.Error($"Failed to retrieve novel data from {novelTableOfContentsUri}");
                return;
            }

            Novel newNovel = CreateNovel(novelData, novelTableOfContentsUri);
            Logger.Info("Finished populating Novel data for {0}", newNovel.Title);

            IEnumerable<ChapterData> chapterDatas = await scraperStrategy.GetChaptersDataAsync(novelData.ChapterUrls);
            newNovel.Chapters = CreateChapters(chapterDatas, newNovel.Id);

            string documentsFolder = GetDocumentsFolder(newNovel.Title);

            if (newNovel.Chapters.Any(chapter => chapter.Pages != null))
            {
                CreatePdf(newNovel, chapterDatas, documentsFolder);
            }
            else
            {
                newNovel.SaveLocation = CreateEpub(newNovel, novelData.ThumbnailImage, documentsFolder);
            }

            await _novelService.CreateAsync(newNovel);
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
            List<Models.Chapter> newChapters = CreateChapters(chapterDatas, novel.Id);

            UpdateNovel(novel, novelData, newChapters);

            string documentsFolder = GetDocumentsFolder(novel.Title);
            novel.SaveLocation = CreateEpub(novel, novelData.ThumbnailImage, documentsFolder);

            await _novelService.UpdateAndAddChapters(novel, newChapters);
        }

        private string GetDocumentsFolder(string title)
        {
            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string fileRegex = @"[^a-zA-Z0-9-\s]";
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            var novelFileSafeTitle = textInfo.ToTitleCase(Regex.Replace(title, fileRegex, string.Empty).ToLower().ToLowerInvariant());
            return Path.Combine(documentsFolder, "BennyScrapedNovels", novelFileSafeTitle);
        }

        private string CreateEpub(Novel novel, byte[]? thumbnailImage, string documentsFolder)
        {
            Directory.CreateDirectory(documentsFolder);
            string epubFile = Path.Combine(documentsFolder, $"{novel.Title}.epub");
            _epubGenerator.CreateEpub(novel, novel.Chapters, epubFile, thumbnailImage);
            return epubFile;
        }

        private void CreatePdf(Novel novel, IEnumerable<ChapterData> chapterDatas, string documentsFolder)
        {
            string pdfFile = Path.Combine(documentsFolder, $"{novel.Title}.pdf");
            Logger.Info(new string('=', 50));
            Console.ForegroundColor = ConsoleColor.Blue;
            CreatePdfs(novel, chapterDatas, pdfFile);
            Console.Write($"Total chapters: {chapterDatas.Count()}\nPDF file created at: {pdfFile}\n");
            var result = _epubGenerator.ExecuteCommand($"calibredb add \"{pdfFile}\"");
            Logger.Info($"Command executed with code: {result}");
            Console.ResetColor();
            Logger.Info(new string('=', 50));
        }

        private void CreatePdfs(Novel novel, IEnumerable<ChapterData> chapterData, string pdfFilePath)
        {
            var images = chapterData.Where(chapter => chapter.Pages != null).SelectMany(chapter => chapter.Pages).Select(page => page.Image).ToList();
            Console.WriteLine($"Total images: {images.Count}");
            // Create a new PDF document
            PdfDocument document = new PdfDocument();

            document.Info.Title = novel.Title;
            document.Info.Author = !string.IsNullOrEmpty(novel.Author) ? novel.Author : null;
            document.Info.Subject = novel.Genre;
            document.Info.Keywords = novel.Genre;


            foreach (var imageBytes in images)
            {
                // Create an empty page in this document
                PdfPage page = document.AddPage();

                // Create an XImage object from the byte array
                using (MemoryStream ms = new MemoryStream(imageBytes))
                {
                    XImage img = XImage.FromStream(ms);

                    // Get an XGraphics object for drawing
                    XGraphics gfx = XGraphics.FromPdfPage(page);

                    // Draw the image centered on the page
                    gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                }
            }
            string directoryPath = Path.GetDirectoryName(pdfFilePath);
            Directory.CreateDirectory(directoryPath);
            // to avoid the System.NotSupportedException: No data is available for encoding 1252. we have to install the Nugget package System.Text.Encoding.CodePages
            //https://stackoverflow.com/questions/50858209/system-notsupportedexception-no-data-is-available-for-encoding-1252

            // Save the document
            document.Save(pdfFilePath);
        }        

        private void UpdateNovel(Novel novel, NovelData novelData, List<Models.Chapter> newChapters)
        {
            novel.Chapters.AddRange(newChapters);
            novel.LastTableOfContentsUrl = (!string.IsNullOrEmpty(novelData.LastTableOfContentsPageUrl)) ? novelData.LastTableOfContentsPageUrl : novel.LastTableOfContentsUrl;
            novel.Status = (!string.IsNullOrEmpty(novelData.NovelStatus)) ? novelData.NovelStatus : novel.Status;
            novel.LastChapter = novelData.IsNovelCompleted;
            novel.DateLastModified = DateTime.Now;
            novel.TotalChapters = novel.Chapters.Count;
            novel.CurrentChapter = novel.Chapters.LastOrDefault()?.Title;
        }


        private Novel CreateNovel(NovelData novelData, Uri novelTableOfContentsUri)
        {
            return new Novel
            {
                Title = novelData.Title ?? string.Empty,
                Author = novelData.Author,
                Url = novelTableOfContentsUri.ToString(),
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
        }

        private List<Chapter> CreateChapters(IEnumerable<ChapterData> chapterDatas, Guid novelId)
        {
            return chapterDatas.Select(data => new Chapter
            {
                NovelId = novelId,
                Url = data.Url ?? string.Empty,
                Content = data.Content,
                Title = data.Title ?? string.Empty,
                Number = data.Number,
                Pages = data.Pages?.Select(p => new Page
                {
                    Url = p.Url,
                    Image = p.Image
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
