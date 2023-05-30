using Benny_Scraper.Interfaces;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Benny_Scraper
{
    /// <summary>
    /// Scraper that scrapes the novel page for the information we need. Does not use Selenium.
    /// </summary>
    public class NovelPageScraper : INovelPageScraper
    {
        private static readonly HttpClient _client = new HttpClient(); // better to keept one instance through the life of the method
        private static readonly SemaphoreSlim _semaphonreSlim = new SemaphoreSlim(5); // limit the number of concurrent requests, prevent posssible rate limiting

        public NovelPageScraper()
        {
        }

        #region Http Requests
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
                Logger.Log.Debug(e);
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
                Logger.Log.Debug(e);

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
        /// 
        /// </summary>
        /// <param name="xPathSelector"></param>
        /// <param name="uri">uri</param>
        /// <returns></returns>
        public async Task<string> GetLatestChapterAsync(string xPathSelector, Uri uri)
        {
            var htmlDocument = await LoadHtmlDocumentFromUrlAsync(uri);
            if (htmlDocument == null)
            {
                Logger.Log.Debug($"Error while trying to get the latest chapter. \n");
                return string.Empty;
            }

            var latestChapterElements = htmlDocument.DocumentNode.SelectNodes(xPathSelector);
            var latestChapters = latestChapterElements.First().InnerText ?? string.Empty;
            return latestChapters;
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

        /// <summary>
        /// Gets the most recent chapters using the last page in the table of contents as a starting point, will only get chapters greater than the last
        /// saved chapter
        /// </summary>
        /// <param name="xPathSelector"></param>
        /// <param name="novelTableOfContentLatestUri">uri for the table of contents page</param>
        /// <param name="currentChapter">last chapter saved in the database for the novel</param>
        /// <returns></returns>
        public async Task<NovelData> GetChaptersFromCheckPointAsync(string xPathSelector, Uri novelTableOfContentLatestUri, string currentChapter)
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
                    ChapterUrls = lastestChapterUrlsToAdd,
                    NovelStatus = novelInfo,
                    LastTableOfContentsPageUrl = (!string.IsNullOrEmpty(lastContentPage) ?
                        $"{novelTableOfContentLatestUri.Scheme}://{novelTableOfContentLatestUri.Host}{lastContentPage}" : novelTableOfContentLatestUri.ToString())
                };

                return novelData;

            }
            catch (Exception e)
            {
                Logger.Log.Debug($"Error while trying to get the latest chapter. \n{e.Message}");
                throw;
            }
        }
        #endregion
    }
}
