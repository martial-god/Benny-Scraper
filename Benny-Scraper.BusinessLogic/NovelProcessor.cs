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
            using NovelDataBuffer novelData = await scraperStrategy.ScrapeAsync();

            if (novelData == null)
            {
                Logger.Error($"Failed to retrieve novel data from {novelTableOfContentsUri}");
                return;
            }

            Novel newNovel = CreateNovel(novelData, novelTableOfContentsUri);
            Logger.Info("Finished populating Novel data for {0}", newNovel.Title);

            IEnumerable<ChapterDataBuffer> chapterDatas = await scraperStrategy.GetChaptersDataAsync(novelData.ChapterUrls);
            newNovel.Chapters = CreateChapters(chapterDatas, newNovel.Id);

            string documentsFolder = GetDocumentsFolder(newNovel.Title);

            if (newNovel.Chapters.Any(chapter => chapter?.Pages != null))
            {
                CreatePdf(newNovel, chapterDatas, documentsFolder);
                foreach (var chapterData in chapterDatas)
                {
                    chapterData.Dispose();
                }
            }
            else
            {
                newNovel.SaveLocation = CreateEpub(newNovel, novelData.ThumbnailImage, documentsFolder);
            }

            await _novelService.CreateAsync(newNovel);
            Logger.Info("Finished adding novel {0} to database", newNovel.Title);
        }

        private async Task UpdateExistingNovelAsync(Novel novel, Uri novelTableOfContentsUri, ScraperStrategy scraperStrategy)
        {
            using NovelDataBuffer novelData = await scraperStrategy.ScrapeAsync();

            if (novelData == null)
            {
                Logger.Error($"Failed to retrieve novel data from {novelTableOfContentsUri}");
                return;
            }

            IEnumerable<ChapterDataBuffer> chapterDatas = await scraperStrategy.GetChaptersDataAsync(novelData.ChapterUrls);
            List<Models.Chapter> newChapters = CreateChapters(chapterDatas, novel.Id);

            UpdateNovel(novel, novelData, newChapters);

            string documentsFolder = GetDocumentsFolder(novel.Title);

            if (newChapters.Any(chapter => chapter?.Pages != null))
            {
                CreatePdf(novel, chapterDatas, documentsFolder);
                foreach (var chapterData in chapterDatas)
                {
                    chapterData.Dispose();
                }
            }
            else
            {
                novel.SaveLocation = CreateEpub(novel, novelData.ThumbnailImage, documentsFolder);
            }


            await _novelService.UpdateAndAddChapters(novel, newChapters);
        }

        private string GetDocumentsFolder(string title)
        {
            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.Equals(Environment.UserName, "emiya", StringComparison.OrdinalIgnoreCase))
                documentsFolder = DriveInfo.GetDrives().FirstOrDefault(drive => drive.Name == @"H:\")?.Name ?? documentsFolder;
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

        private void CreatePdf(Novel novel, IEnumerable<ChapterDataBuffer> chapterDatas, string documentsFolder)
        {
            Logger.Info("Creating PDFs for {0}", novel.Title);
            int? totalPages = novel.Chapters.Where(chapter => chapter.Pages != null).SelectMany(chapter => chapter.Pages).Count();
            int totalMissingChapters = novel.Chapters.Count(chapter => chapter.Pages == null || !chapter.Pages.Any());
            var missingChapterUrls = novel.Chapters.Where(chapter => chapter.Pages == null).Select(chapter => chapter.Url);

            Logger.Info(new string('=', 50));
            Console.ForegroundColor = ConsoleColor.Blue;
            CreateSinglePdf(novel, chapterDatas, documentsFolder);

            Console.Write($"Total chapters: {novel.Chapters.Count()}\nTotal pages {totalPages}:\n\nPDF files created at: {documentsFolder}\n");
            if (totalMissingChapters > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Theere were {totalMissingChapters} chapters with no pages");
                Console.WriteLine($"Missing chapter urls: {string.Join("\n", missingChapterUrls)}");
            }
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"Adding PDFs to Calibre database");
            var result = _epubGenerator.ExecuteCommand($"calibredb add \"{documentsFolder}\" --series \"{novel.Title}\"");
            Logger.Info($"Command executed with code: {result}");
            Console.ResetColor();
            Logger.Info(new string('=', 50));
            Logger.Info($"Total chapters: {novel.Chapters.Count()}\nTotal pages {totalPages}:\n\nPDF files created at: {documentsFolder}\n");

        }

        private void DeleteTempFolder(string tempFile)
        {
            string directory = Path.GetDirectoryName(tempFile);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, false);
                Logger.Info($"Deleted temp folder {directory}");
            }

        }

        private void CreatePdfByChapter(Novel novel, IEnumerable<ChapterDataBuffer> chapterData, string pdfDirectoryPath)
        {
            Directory.CreateDirectory(pdfDirectoryPath);

            foreach (var chapter in chapterData)
            {
                if (chapter.Pages == null)
                    continue;

                var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList(); // only Page from PageData has ImagePath as a member variable
                Console.WriteLine($"Total images in chapter {chapter.Title}: {imagePaths.Count}");

                PdfDocument document = new PdfDocument();

                document.Info.Title = $"{novel.Title} - {chapter.Title}";
                document.Info.Author = !string.IsNullOrEmpty(novel.Author) ? novel.Author : null;
                document.Info.Subject = novel.Genre;
                document.Info.Keywords = novel.Genre;
                document.Info.CreationDate = DateTime.Now;

                foreach (var imagePath in imagePaths)
                {
                    XImage img = XImage.FromFile(imagePath);

                    PdfPage page = document.AddPage();
                    page.Width = XUnit.FromPoint(img.PixelWidth);
                    page.Height = XUnit.FromPoint(img.PixelHeight);

                    XGraphics gfx = XGraphics.FromPdfPage(page);

                    gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                }
                // to avoid the System.NotSupportedException: No data is available for encoding 1252. we have to install the Nugget package System.Text.Encoding.CodePages
                //https://stackoverflow.com/questions/50858209/system-notsupportedexception-no-data-is-available-for-encoding-1252

                var sanitizedTitle = SanitizeFileName($"{novel.Title} - {chapter.Title}");
                var pdfFilePath = Path.Combine(pdfDirectoryPath, $"{sanitizedTitle}.pdf");
                document.Save(pdfFilePath);
            }
        }

        private void CreateSinglePdf(Novel novel, IEnumerable<ChapterDataBuffer> chapterData, string pdfDirectoryPath)
        {
            Directory.CreateDirectory(pdfDirectoryPath);

            PdfDocument document = new PdfDocument();

            document.Info.Title = $"{novel.Title}";
            document.Info.Author = !string.IsNullOrEmpty(novel.Author) ? novel.Author : null;
            document.Info.Subject = novel.Genre;
            document.Info.Keywords = novel.Genre;
            document.Info.CreationDate = DateTime.Now;

            foreach (var chapter in chapterData)
            {
                if (chapter.Pages == null)
                    continue;

                var imagePaths = chapter.Pages.Select(page => page.ImagePath).ToList(); // only Page from PageData has ImagePath as a member variable
                Console.WriteLine($"Total images in chapter {chapter.Title}: {imagePaths.Count}");

                foreach (var imagePath in imagePaths)
                {
                    XImage img;
                    using (var imageStream = File.OpenRead(imagePath))
                    {
                        img = XImage.FromStream(imageStream);
                        PdfPage page = document.AddPage();
                        page.Width = XUnit.FromPoint(img.PixelWidth);
                        page.Height = XUnit.FromPoint(img.PixelHeight);

                        XGraphics gfx = XGraphics.FromPdfPage(page);

                        gfx.DrawImage(img, 0, 0, page.Width, page.Height);
                    }

                    File.Delete(imagePath);
                }
            }
            DeleteTempFolder(chapterData.First().Pages.First().ImagePath);

            var sanitizedTitle = SanitizeFileName($"{novel.Title}");
            var pdfFilePath = Path.Combine(pdfDirectoryPath, $"{sanitizedTitle}.pdf");
            document.Save(pdfFilePath);
        }


        public static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return new string(fileName.Where(ch => !invalidChars.Contains(ch)).ToArray());
        }


        private void UpdateNovel(Novel novel, NovelDataBuffer novelData, List<Models.Chapter> newChapters)
        {
            novel.Chapters.AddRange(newChapters);
            novel.LastTableOfContentsUrl = (!string.IsNullOrEmpty(novelData.LastTableOfContentsPageUrl)) ? novelData.LastTableOfContentsPageUrl : novel.LastTableOfContentsUrl;
            novel.Status = (!string.IsNullOrEmpty(novelData.NovelStatus)) ? novelData.NovelStatus : novel.Status;
            novel.LastChapter = novelData.IsNovelCompleted;
            novel.DateLastModified = DateTime.Now;
            novel.TotalChapters = novel.Chapters.Count;
            novel.CurrentChapter = novel.Chapters.LastOrDefault()?.Title;
        }


        private Novel CreateNovel(NovelDataBuffer novelData, Uri novelTableOfContentsUri)
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

        private List<Chapter> CreateChapters(IEnumerable<ChapterDataBuffer> chapterDatas, Guid novelId)
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
