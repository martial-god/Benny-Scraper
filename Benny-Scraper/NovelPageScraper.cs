using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;

namespace Benny_Scraper
{
    /// <summary>
    /// Scraper that scrapes the novel page for the information we need. Does not use Selenium.
    /// </summary>
    public class NovelPageScraper : INovelPageScraper
    {

        private string _fileSavePath = @"H:\Projects\Novels\{0}\Read {1} - {2}.html";
        private string _pdfFileSavePath = @"H:\Projects\Novels\{0}\Read {1} - {2}.pdf";
        private string _fileSaveFolder = @"H:\Projects\Novels\{0}\";

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
            using (var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var htmlDocument = new HtmlDocument();
                    htmlDocument.LoadHtml(responseBody);

                    var title = htmlDocument.DocumentNode.SelectSingleNode(titleXPathSelector)?.InnerText ?? string.Empty;
                    var contentHtml = htmlDocument.DocumentNode.SelectSingleNode(contentXPathSelector)?.OuterHtml;
                    var content = htmlDocument.DocumentNode.SelectNodes("//p").Select(x => x.InnerText).ToList();

                    // save content to file
                    string fileRegex = @"[^a-zA-Z0-9-\s]";
                    var fileSafeTitle = Regex.Replace(title, fileRegex, " ");
                    var novelTitleFileSafe = Regex.Replace(novelTitle, fileRegex, " ");
                    string filePath = string.Format(_fileSavePath, novelTitleFileSafe, novelTitleFileSafe, fileSafeTitle);
                    string directory = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.WriteAllText(filePath, contentHtml);

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

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xPathSelector"></param>
        /// <param name="uri">uri</param>
        /// <returns></returns>
        public async Task<string> GetLatestChapterAsync(string xPathSelector, Uri uri)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(uri);
                    response.EnsureSuccessStatusCode();
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var htmlDocument = new HtmlDocument();
                    htmlDocument.LoadHtml(responseBody);

                    var latestChapterElements = htmlDocument.DocumentNode.SelectNodes(xPathSelector);
                    var latestChapters = latestChapterElements.First().InnerText ?? string.Empty;
                    return latestChapters;
                }
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
