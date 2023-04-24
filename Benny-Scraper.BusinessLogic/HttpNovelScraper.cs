using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using NLog;
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

        #region Public Methods
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


        /// <summary>
        /// Collects information about a novel from the table of contents and getting most recent chapters urls that are not saved locally
        /// </summary>
        /// <param name="pageToStartAt">Page number of the table of contents</param>
        /// <param name="siteUri"></param>
        /// <param name="siteConfig"></param>
        /// <param name="lastSavedChapterUrl"></param>
        /// <returns></returns>
        public async Task<NovelData> RequestPaginatedDataAsync(int pageToStartAt, Uri siteUri, SiteConfiguration siteConfig, string lastSavedChapterUrl)
        {
            List<string> chapterUrls = new List<string>();

            string baseTableOfContentUrl = siteUri + siteConfig.PaginationType;

            int lastPage = await GetCurrentLastTableOfContentsPageNumber(siteUri, siteConfig);
            string lastTableOfContentsUrl = string.Format(baseTableOfContentUrl, lastPage);

            for (int i = pageToStartAt; i <= lastPage; i++)
            {
                string tableOfContentUrl = string.Format(baseTableOfContentUrl, i);
                bool isPageNew = i > pageToStartAt;
                try
                {
                    Logger.Info($"Navigating to {tableOfContentUrl}");
                    HtmlDocument htmlDocument = await LoadHtmlDocumentFromUrlAsync(new Uri(tableOfContentUrl));

                    List<string> chapterUrlsOnContentPage = GetLatestChapterUrls(htmlDocument, siteConfig, lastSavedChapterUrl, siteUri, isPageNew);
                    if (chapterUrlsOnContentPage.Any())
                    {
                        chapterUrls.AddRange(chapterUrlsOnContentPage);
                    }

                }
                catch (HttpRequestException e)
                {
                    Logger.Error($"Error occurred while navigating to {tableOfContentUrl}. Error: {e}");
                }
            }

            HtmlDocument html = await LoadHtmlDocumentFromUrlAsync(new Uri(baseTableOfContentUrl));
            NovelData novelData = GetNovelDataFromTableOfContent(html, siteConfig);
            novelData.LastTableOfContentsPageUrl = lastTableOfContentsUrl;
            novelData.RecentChapterUrls = chapterUrls;
            novelData.ThumbnailUrl = new Uri(siteUri, novelData.ThumbnailUrl.TrimStart('/')).ToString();

            return novelData;
        }

        public async Task<List<ChapterData>> GetChaptersDataAsync(List<string> chapterUrls, SiteConfiguration siteConfig)
        {
            try
            {
                List<Task<ChapterData>> tasks = new List<Task<ChapterData>>();
                foreach (var url in chapterUrls)
                {
                    await _semaphonreSlim.WaitAsync();
                    tasks.Add(GetChapterDataAsync(url, siteConfig));
                }

                ChapterData[] chapterData = await Task.WhenAll(tasks);
                return chapterData.ToList();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting chapters data. {ex}");
                throw;
            }
        }

        public async Task<NovelData> GetNovelDataAsync(Uri uri, SiteConfiguration siteConfig)
        {
            Logger.Info("Getting novel data");
            HtmlDocument htmlDocument = await LoadHtmlDocumentFromUrlAsync(uri);

            if (htmlDocument == null)
            {
                Logger.Debug($"Error while trying to get the novel data. \n");
                return null;
            }

            try
            {
                NovelData novelData = GetNovelDataFromTableOfContent(htmlDocument, siteConfig);

                HtmlNode latestChapterNode = htmlDocument.DocumentNode.SelectSingleNode(siteConfig.Selectors.LatestChapterLink);

                if (latestChapterNode == null)
                {
                    Logger.Debug($"Error while trying to get the latest chapter node. \n");
                    return null;
                }

                string latestChapterUrl = latestChapterNode.Attributes["href"].Value;
                string fullUrl = new Uri(uri, latestChapterUrl.TrimStart('/')).ToString();
                novelData.RecentChapterUrls.Add(fullUrl);

                return novelData;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting novel data. {ex}");
                throw;
            }
        }
        #endregion

        #region Private Methods
        private NovelData GetNovelDataFromTableOfContent(HtmlDocument htmlDocument, SiteConfiguration siteConfig)
        {
            NovelData novelData = new NovelData();

            try
            {
                HtmlNode authorNode = htmlDocument.DocumentNode.SelectSingleNode(siteConfig.Selectors.NovelAuthor);
                novelData.Author = authorNode.InnerText.Trim();

                HtmlNode novelRatingNode = htmlDocument.DocumentNode.SelectSingleNode(siteConfig.Selectors.NovelRating);
                novelData.Rating = double.Parse(novelRatingNode.InnerText.Trim());

                HtmlNode totalRatingsNode = htmlDocument.DocumentNode.SelectSingleNode(siteConfig.Selectors.TotalRatings);
                novelData.TotalRatings = int.Parse(totalRatingsNode.InnerText.Trim());

                HtmlNodeCollection descriptionNode = htmlDocument.DocumentNode.SelectNodes(siteConfig.Selectors.NovelDescription);
                var foo = descriptionNode[0].InnerText.Trim();
                novelData.Description = descriptionNode.Select(description => description.InnerText.Trim()).ToList();

                HtmlNodeCollection genreNodes = htmlDocument.DocumentNode.SelectNodes(siteConfig.Selectors.NovelGenres);
                novelData.Genres = genreNodes.Select(genre => genre.InnerText.Trim()).ToList();

                HtmlNodeCollection alternateNameNodes = htmlDocument.DocumentNode.SelectNodes(siteConfig.Selectors.NovelAlternativeNames);
                novelData.AlternativeNames = alternateNameNodes.Select(alternateName => alternateName.InnerText.Trim()).ToList();

                HtmlNode novelStatusNode = htmlDocument.DocumentNode.SelectSingleNode(siteConfig.Selectors.NovelStatus);
                novelData.NovelStatus = novelStatusNode.InnerText.Trim();

                HtmlNode thumbnailUrlNode = htmlDocument.DocumentNode.SelectSingleNode(siteConfig.Selectors.NovelThumbnailUrl);
                novelData.ThumbnailUrl = thumbnailUrlNode.Attributes["src"].Value;

                novelData.IsNovelCompleted = novelData.NovelStatus.ToLower().Contains(siteConfig.CompletedStatus);
            }
            catch (Exception e)
            {
                Logger.Error($"Error occurred while getting novel data from table of content. Error: {e}");
            }

            return novelData;
        }

        private async Task<ChapterData> GetChapterDataAsync(string url, SiteConfiguration siteConfig)
        {
            ChapterData chapterData = new ChapterData();
            HtmlDocument htmlDocument = await LoadHtmlDocumentFromUrlAsync(new Uri(url));
            try
            {
                HtmlNode titleNode = htmlDocument.DocumentNode.SelectSingleNode(siteConfig.Selectors.ChapterTitle);
                chapterData.Title = titleNode.InnerText.Trim();

                HtmlNodeCollection paragraphNodes = htmlDocument.DocumentNode.SelectNodes(siteConfig.Selectors.ChapterContent);
                List<string> paragraphs = paragraphNodes.Select(paragraph => paragraph.InnerText.Trim()).ToList();
                chapterData.Content = string.Join("\n", paragraphs);

                chapterData.Url = url;
                chapterData.DateLastModified = DateTime.Now;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                _semaphonreSlim.Release();
            }

            return chapterData;

        }

        private async Task<int> GetCurrentLastTableOfContentsPageNumber(Uri siteUrl, SiteConfiguration siteConfig)
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

        private string GetNovelStatus(HtmlDocument htmlDocument, SiteConfiguration siteConfig)
        {
            Logger.Info($"Getting novel status");
            try
            {
                HtmlNode novelStatusNode = htmlDocument.DocumentNode.SelectSingleNode(siteConfig.Selectors.NovelStatus);

                if (novelStatusNode == null)
                {
                    Logger.Info("Novel status Node on table of contents page was null.");
                    return string.Empty;
                }

                string novelStatus = novelStatusNode.InnerText;
                if (novelStatus == null)
                {
                    Logger.Info("Novel status inner text on table of contents page was null.");
                    return string.Empty;
                }
                return novelStatus;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting novel status. {ex}");
                throw;
            }
        }

        private List<string> GetLatestChapterUrls(HtmlDocument htmlDocument, SiteConfiguration siteConfig, string lastSavedChapterUrl, Uri siteUri, bool isPageNew)
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

                bool foundLastSavedChapter = string.IsNullOrEmpty(lastSavedChapterUrl);
                foreach (var link in chapterLinks)
                {
                    string chapterUrl = link.Attributes["href"]?.Value;

                    if (!foundLastSavedChapter && chapterUrl == lastSavedChapterUrl) // only add chapters after last saved chapter, new page means all new chapters
                    {
                        foundLastSavedChapter = true;
                    }
                    else if ((foundLastSavedChapter || isPageNew) && !string.IsNullOrEmpty(chapterUrl))
                    {
                        string fullUrl = new Uri(siteUri, chapterUrl.TrimStart('/')).ToString();
                        chapterUrls.Add(fullUrl);
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
        #endregion
    }
}
