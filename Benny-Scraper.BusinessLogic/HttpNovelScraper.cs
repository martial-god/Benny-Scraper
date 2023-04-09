using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Fluent;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Benny_Scraper.BusinessLogic
{
    /// <summary>
    /// A http implementation of the INovelScraper interface. Use this for sites that don't require login-in to get the chapter contents like novelupdates.com
    /// </summary>
    public class HttpNovelScraper : INovelScraper
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static readonly HttpClient _client = new HttpClient(); // better to keep one instance through the life of the method
        private static readonly SemaphoreSlim _semaphonreSlim = new SemaphoreSlim(5); // limit the number of concurrent requests, prevent posssible rate limiting
        private readonly NovelScraperSettings _novelScraperSettings; // IOptions will get an instnace of NovelScraperSettings

        public HttpNovelScraper(IOptions<NovelScraperSettings> novelScraperSettings)
        {
            _novelScraperSettings = novelScraperSettings.Value;
        }

        public async Task GoToTableOfContentsPageAsync(Uri novelTableOfContentsUri)
        {
            await _client.GetAsync(novelTableOfContentsUri);
        }

        #region Http Requests
        /// <summary>
        /// Creates a collection of chapter urls by incrementing the page number in the url. ex: https://novelfull.com/paragon-of-sin.html?page=2 to ?page=3
        /// </summary>
        /// <param name="pageToStartAt"></param>
        /// <param name="siteUrl"></param>
        /// <param name="siteConfig"></param>
        /// <returns>collection of urls</returns>
        public async Task<List<string>> BuildChaptersUrlsFromTableOfContentUsingPaginationAsync(int pageToStartAt, Uri siteUrl, SiteConfiguration siteConfig, string lastSavedChaptersName)
        {
            
            string baseTableOfContentUrl = siteUrl + siteConfig.PaginationType;
            int lastPage = await GetCurrentLastTableOfContentsUrl(siteUrl, siteConfig);
            
            List<string> chapterUrls = new List<string>();

            for (int i = pageToStartAt; i <= lastPage; i++)
            {
                string tableOfContentUrl = string.Format(baseTableOfContentUrl, i);
                try
                {
                    Logger.Info($"Navigating to {tableOfContentUrl}");
                    var htmlDocument = await LoadHtmlDocumentFromUrlAsync(new Uri(tableOfContentUrl));
                    var chapterUrlsOnContentPage = GetChapterUrls(htmlDocument, siteConfig, lastSavedChaptersName);
                    if (chapterUrlsOnContentPage != null)
                    {
                        chapterUrls.AddRange(chapterUrlsOnContentPage);
                    }
                        
                }
                catch (HttpRequestException e)
                {
                    Logger.Error($"Error occurred while navigating to {tableOfContentUrl}. Error: {e}");
                }
            }
            return chapterUrls;
        }        
        
        /// <summary>
        /// Gets chapter from the collection of chapters
        /// </summary>
        /// <param name="chapterUrls"></param>
        /// <param name="titleXPathSelector">selector in the form of an XPath</param>
        /// <param name="contentXPathSelector">selector in the form of an XPath</param>
        /// <param name="novelTitle">Title of the novel will be used to create a folder for the novel to save chapters</param>
        /// <returns>Task that contains a collection of all chapters of type ChapterData</returns>
        public async Task<List<ChapterData>> GetChaptersDataAsync(List<string> chapterUrls, string titleXPathSelector, string contentXPathSelector, string novelTitle)
        {
            try
            {
                List<Task<ChapterData>> tasks = new List<Task<ChapterData>>();
                foreach (var url in chapterUrls)
                {
                    await _semaphonreSlim.WaitAsync();
                    tasks.Add(GetChapterDataAsync(url, titleXPathSelector, contentXPathSelector, novelTitle));
                }

                var chapterData = await Task.WhenAll(tasks);

                return chapterData.ToList();
            }
            catch (Exception e)
            {
                Logger.Debug(e);
                throw;
            }
        }

        private async Task<int> GetCurrentLastTableOfContentsUrl(Uri siteUrl, SiteConfiguration siteConfig)
        {
            var htmlDocument = await LoadHtmlDocumentFromUrlAsync(siteUrl);
            int lastPageNumber = GetLastTableOfContentsPageNumber(htmlDocument, siteConfig);
            return lastPageNumber;
        }

        private int GetLastTableOfContentsPageNumber(HtmlDocument htmlDocument, SiteConfiguration siteConfig)
        {
            Logger.Info($"Getting last table of contents page number at {siteConfig.Selectors.LastTableOfContentsPage}");
            try
            {
                HtmlNode lastPageNode = htmlDocument.DocumentNode.SelectSingleNode(siteConfig.Selectors.LastTableOfContentsPage);
                string lastPage = lastPageNode.Attributes[siteConfig.Selectors.LastTableOfContentPageNumberAttribute].Value;

                int lastPageNumber = int.Parse(lastPage, NumberStyles.AllowThousands);
                
                if (siteConfig.PageOffSet > 0)
                {
                    lastPageNumber += siteConfig.PageOffSet;
                }
                
                Logger.Info($"Last table of contents page number is {lastPage}");
                return lastPageNumber;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error when getting last page table of contents page number. {ex}");
                throw;
            }
            
            

            
        }

        /// <summary>
        /// Gets chapter data and creates html files
        /// </summary>
        /// <param name="url"></param>
        /// <param name="titleXPathSelector"></param>
        /// <param name="contentXPathSelector"></param>
        /// <param name="novelTitle"></param>
        /// <returns></returns>
        private async Task<ChapterData> GetChapterDataAsync(string url, string titleXPathSelector, string contentXPathSelector, string novelTitle)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            try
            {
                var response = await _client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(responseBody);

                var title = htmlDocument.DocumentNode.SelectSingleNode(titleXPathSelector)?.InnerText ?? string.Empty;
                var contentHtml = htmlDocument.DocumentNode.SelectSingleNode(contentXPathSelector)?.OuterHtml;
                var content = htmlDocument.DocumentNode.SelectNodes("//p").Select(x => x.InnerText).ToList();

                SaveAndWriteNovelToMyDocuments(title, novelTitle, contentHtml);

                return new ChapterData
                {
                    Title = title,
                    Content = contentHtml,
                    Url = url,
                };
            }
            catch (Exception e)
            {
                Logger.Debug(e);

                // return what we have so far
                return new ChapterData
                {
                    Title = string.Empty,
                    Content = string.Empty,
                    Url = url,
                };
            }
            finally
            {
                _semaphonreSlim.Release();
            }


        }

        private void SaveAndWriteNovelToMyDocuments(string title, string novelTitle, string? contentHtml)
        {
            // save content to file
            string fileRegex = @"[^a-zA-Z0-9-\s]";
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            var chapterFileSafeTitle = textInfo.ToTitleCase(Regex.Replace(title, fileRegex, " ").ToLower());
            var novelTitleFileSafe = textInfo.ToTitleCase(Regex.Replace(novelTitle, fileRegex, " ").ToLower());
            string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string _fileSavePath = Path.Combine(documentsFolder, "Novel", novelTitleFileSafe, $"Read {novelTitleFileSafe} - {chapterFileSafeTitle}.html");

            if (!Directory.Exists(_fileSavePath))
            {
                Directory.CreateDirectory(_fileSavePath);
            }

            File.WriteAllText(_fileSavePath, contentHtml);
        }

        /// <summary>
        /// Retrieves the latest chapter's name from the provided URL using the site configuration.
        /// </summary>
        /// <param name="uri">The URL of the web page to scrape.</param>
        /// <param name="siteConfig">The site configuration containing the selectors for scraping.</param>
        /// <returns>The latest chapter's name, or an empty string if an error occurs.</returns>
        public async Task<string> GetLatestChapterNameAsync(Uri uri, SiteConfiguration siteConfig)
        {
            Logger.Info("Getting latest chapter name");
            HtmlDocument htmlDocument = await LoadHtmlDocumentFromUrlAsync(uri);
            
            if (htmlDocument == null)
            {
                Logger.Debug($"Error while trying to get the latest chapter. \n");
                return string.Empty;
            }


            try
            {
                HtmlNode latestChapterNode = htmlDocument.DocumentNode.SelectSingleNode(siteConfig.Selectors.LatestChapterLink);

                if (latestChapterNode == null)
                {
                    Logger.Debug($"Error while trying to get the latest chapter node. \n");
                    return string.Empty;
                }

                string latestChapterName = latestChapterNode.InnerText;
                if (latestChapterName == null)
                {
                    Logger.Debug($"Error while trying to get the latest chapter name form the node. \n");
                    return string.Empty;
                }
                return latestChapterName;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting latest chapter name. {ex}");
                throw;
            }
            
            
            
        }

        private static async Task<HtmlDocument> LoadHtmlDocumentFromUrlAsync(Uri uri)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            var response = await _client.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);
            return htmlDocument;
        }

        private List<string> GetChapterUrls(HtmlDocument htmlDocument, SiteConfiguration siteConfig, string lastSavedChaptersName)
        {
            Logger.Info($"Getting chapter urls from table of contents");
            try
            {
                HtmlNodeCollection chapterLinks = htmlDocument.DocumentNode.SelectNodes(siteConfig.Selectors.ChapterLinks);

                if (chapterLinks == null)
                {
                    Logger.Info("Chapter links Node Collection on table of contents page was null.");
                    return new List<string>();
                }

                List<string> chapterUrls = new List<string>();

                bool foundLastSavedChapter = string.IsNullOrEmpty(lastSavedChaptersName);
                foreach (var link in chapterLinks)
                {
                    string chapterUrl = link.Attributes["href"]?.Value;
                    
                    if (!foundLastSavedChapter && chapterUrl == lastSavedChaptersName) // only add chapters after last saved chapter
                    {
                        foundLastSavedChapter = true;
                    }
                    else if (foundLastSavedChapter && !string.IsNullOrEmpty(chapterUrl))
                    {
                        chapterUrls.Add(chapterUrl);
                    }
                    else
                    {
                        chapterUrls.Add(chapterUrl);
                    }
                }

                return chapterUrls;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting chapter urls from table of contents. {ex}");
                throw;
            }
            
        }


        /// <summary>
        /// Gets the most recent chapters using the last page in the table of contents as a starting point, will only get chapters greater than the last
        /// saved chapter
        /// </summary>
        /// <param name="xPathSelector"></param>
        /// <param name="novelTableOfContentLatestUri">uri for the table of contents page</param>
        /// <param name="currentChapter">last chapter saved in the database for the novel</param>
        /// <returns></returns>
        public async Task<NovelData> GetNewChaptersFromLastSavedAsync(string xPathSelector, Uri novelTableOfContentLatestUri, string currentChapter)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                var response = await _client.GetAsync(novelTableOfContentLatestUri);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(responseBody);
                // how to get last element using XPath https://stackoverflow.com/questions/1459132/xslt-getting-last-element
                var novelInfo = htmlDocument.DocumentNode.SelectSingleNode("(//div[@class='info']//a)[last()]").InnerText;
                var lastContentPage = htmlDocument.DocumentNode.SelectSingleNode("//li[@class='last']//a/@href")?.Attributes["href"].Value;


                var latestChapterElements = htmlDocument.DocumentNode.SelectNodes(xPathSelector);
                var latestChapters = latestChapterElements.Select(x => x.Attributes["href"].Value).Where(c =>
                {
                    var currentMatch = Regex.Match(currentChapter, @"\d+");
                    var siteMatch = Regex.Match(c, @"\d+");
                    var chapterNumberOnSite = int.Parse(siteMatch.Success ? siteMatch.Groups[0].Value : "0");
                    var currentChap = int.Parse(currentMatch.Success ? currentMatch.Groups[0].Value : "0");
                    return chapterNumberOnSite > currentChap; // only get chapters new than the ones we have saved

                });

                // make this a full url with https/ http for the scheme
                List<string> lastestChapterUrlsToAdd = latestChapters.Select(x =>
                {
                    return $"{novelTableOfContentLatestUri.Scheme}://{novelTableOfContentLatestUri.Host}{x}";
                }).ToList();

                NovelData novelData = new NovelData()
                {
                    LatestChapterUrls = lastestChapterUrlsToAdd,
                    Status = novelInfo,
                    LastTableOfContentsUrl = (!string.IsNullOrEmpty(lastContentPage) ?
                        $"{novelTableOfContentLatestUri.Scheme}://{novelTableOfContentLatestUri.Host}{lastContentPage}" : novelTableOfContentLatestUri.ToString())
                };

                return novelData;

            }
            catch (Exception e)
            {
                Logger.Debug($"Error while trying to get the latest chapter. \n{e.Message}");
                throw;
            }
        }
        #endregion

        //private async Task UpdateExistingNovelAsync(Novel novel, Uri novelTableOfContentsUri)
        //{
        //    var currentChapter = novel?.CurrentChapter;
        //    var chapterContext = novel?.Chapters;

        //    if (currentChapter == null || chapterContext == null)
        //        return;


        //    INovelPageScraper novelPageScraper = new NovelPageScraper();
        //    var latestChapterName = await novelPageScraper.GetLatestChapterNameAsync("//ul[@class='l-chapters']//a", novelTableOfContentsUri);
        //    bool isCurrentChapterNewest = string.Equals(currentChapter, latestChapterName, comparisonType: StringComparison.OrdinalIgnoreCase);

        //    if (isCurrentChapterNewest)
        //    {
        //        Logger.Info($"{novel.Title} is currently at the latest chapter.\nCurrent Saved: {novel.CurrentChapter}");
        //        return;
        //    }


        //    // get all newChapters after the current chapter up to the latest
        //    if (string.IsNullOrEmpty(novel.LastTableOfContentsUrl))
        //    {
        //        Logger.Info($"{novel.Title} does not have a LastTableOfContentsUrl.\nCurrent Saved: {novel.LastTableOfContentsUrl}");
        //        return;
        //    }

        //    Uri lastTableOfContentsUrl = new Uri(novel.LastTableOfContentsUrl);
        //    var latestChapterData = await novelPageScraper.GetChaptersFromCheckPointAsync("//ul[@class='list-chapter']//a/@href", lastTableOfContentsUrl, novel.CurrentChapter);
        //    IEnumerable<ChapterData> chapterData = await novelPageScraper.GetChaptersDataAsync(latestChapterData.LatestChapterUrls, "//span[@class='chapter-text']", "//div[@id='chapter']", novel.Title);

        //    List<Models.Chapter> newChapters = chapterData.Select(data => new Models.Chapter
        //    {
        //        Url = data.Url ?? "",
        //        Content = data.Content ?? "",
        //        Title = data.Title ?? "",
        //        DateCreated = DateTime.UtcNow,
        //        DateLastModified = DateTime.UtcNow,
        //        Number = data.Number,

        //    }).ToList();
        //    novel.LastTableOfContentsUrl = latestChapterData.LastTableOfContentsUrl;
        //    novel.Status = latestChapterData.Status;

        //    novel.Chapters.AddRange(newChapters);
        //    await _novelService.UpdateAndAddChapters(novel, newChapters);
        //}
    }
}
