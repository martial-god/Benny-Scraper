using Benny_Scraper.Interfaces;
using Benny_Scraper.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Text.RegularExpressions;


namespace Benny_Scraper
{
    public class NovelPage
    {
        private string _fileSavePath = @"H:\Projects\Novels\{0}\Read {1} - {2}.html";
        private string _pdfFileSavePath = @"H:\Projects\Novels\{0}\Read {1} - {2}.pdf";
        private string _fileSaveFolder = @"H:\Projects\Novels\{0}\";
        private readonly IWebDriver _driver;

        public NovelPage(IWebDriver driver)
        {
            _driver = driver;
            Logger.Setup();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri">uri that contains the table of contents</param>
        /// <returns></returns>
        public async Task<Novel> BuildNovelAsync(Uri uri)
        {
            try
            {
                _driver.Navigate().GoToUrl(uri);
                new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.UrlContains(uri.OriginalString));

                var title = GetTitle(".title");
                var latestChapter = GetLatestChapterUsingSelenium(".l-chapters a span.chapter-text");
                IEnumerable<string> info = new List<string>();
                string author = string.Empty;
                string genre = string.Empty;
                string status = string.Empty;
                bool lastChapter = false;
                string siteName = string.Empty;
                string saveFolder = string.Empty;
                if (uri.Host == "novelfull.com")
                {
                    info = GetNovelFullNovelInfo();
                    author = info.First();
                    genre = string.Join(",", info.Skip(1).Take(info.Count() - 2)); // get the middle values skipping the first then stepping back 2 to skip the last
                    status = info.Last().ToUpper();
                    lastChapter = (status.ToLower() == "completed") ? true : false;
                    siteName = "Novelfull";
                    saveFolder = string.Format(_fileSaveFolder, title);
                }

                INovelPageScraper novelPageScraper = new NovelPageScraper();
                string lastPageUrl = GetLastTableOfContentPageUrl("last");
                int lastPage = Regex.Match(lastPageUrl, @"\d+").Success ? Convert.ToInt32(Regex.Match(lastPageUrl, @"\d+").Value) : 0;
                // use List<string, string> and have the GetChapters... return the content as well.
                List<string> chapterUrls = GetChaptersUsingPagitation(1, lastPage, uri.OriginalString);
                IEnumerable<ChapterData> chapterData = await novelPageScraper.GetChaptersDataAsync(chapterUrls, "//span[@class='chapter-text']", "//div[@id='chapter']", title);
                var firstChapterUrl = chapterUrls.First();
                var lastChapterUrl = chapterUrls.Last();

                List<Chapter> chapters = chapterData.Select(data =>
                new Chapter
                {
                    Url = data.Url ?? "",
                    Content = data.Content ?? "",
                    Title = data.Title ?? "",
                    DateCreated = DateTime.Now,
                    DateLastModified = DateTime.Now,
                    Number = data.Number,

                }).ToList();

                Novel novel = new Novel
                {
                    Title = title,
                    SiteName = siteName,
                    Url = uri.OriginalString,
                    Author = author,
                    Genre = genre,
                    FirstChapter = firstChapterUrl,
                    CurrentChapter = latestChapter,
                    TotalChapters = chapterUrls.Count,
                    SaveLocation = saveFolder,
                    Chapters = chapters,
                    LastChapter = lastChapter,
                    DateCreated = DateTime.Now,
                    Status = status,
                };
                return novel;
            }
            catch (Exception e)
            {
                Logger.Log.Error(e);
                throw;
            }
        }

        #region Get Novel Info
        /// <summary>
        /// Assumes that we are already on the table of contents page of a Novelfull novel
        /// </summary>
        /// <returns></returns>
        public List<string> GetNovelFullNovelInfo()
        {
            try
            {
                new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.ElementToBeClickable(By.ClassName("info")));
                var info = _driver.FindElements(By.CssSelector(".info a"));
                List<string> novelInfo = info.Select(u => u.Text).ToList(); //first is Author, Genre, and the last will be the status
                return novelInfo;
            }
            catch (Exception e)
            {
                Logger.Log.Debug(e);
                throw;
            }
        }

        public async Task<List<ChapterData>> GetChaptersDataUsingSeleniumAsync(List<string> chapterUrls, string titleSelector, string novelTitle)
        {
            try
            {
                List<ChapterData> chapterData = new List<ChapterData>();
                foreach (var url in chapterUrls)
                {
                    _driver.Navigate().GoToUrl(url);
                    new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.UrlContains(url));

                    string fileRegex = @"[^a-zA-Z0-9-\s]";
                    var title = _driver.FindElement(By.ClassName(titleSelector)).Text ?? string.Empty;
                    var fileSafeTitle = Regex.Replace(title, fileRegex, " ");
                    var novelTitleFileSafe = Regex.Replace(novelTitle, fileRegex, " ");
                    //var contents = _driver.FindElements(By.TagName("p")).Select(x => x.Text).ToList();
                    var contentHtml = _driver.FindElement(By.CssSelector("#chapter")).GetAttribute("outerHTML");

                    string filePath = string.Format(_fileSavePath, novelTitleFileSafe, novelTitleFileSafe, fileSafeTitle);
                    string pdfFilePath = string.Format(_pdfFileSavePath, novelTitleFileSafe, novelTitleFileSafe, fileSafeTitle);
                    string directory = Path.GetDirectoryName(filePath);

                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }


                    //var renderer = new IronPdf.HtmlToPdf();
                    //var pdf = await renderer.RenderHtmlAsPdfAsync(contentHtml);
                    //pdf.SaveAs(pdfFilePath);
                    File.WriteAllText(filePath, contentHtml);

                    chapterData.Add(new ChapterData
                    {
                        Title = title,
                        Content = contentHtml,
                        Url = url
                    });

                }
                return chapterData;
            }
            catch (Exception e)
            {
                Logger.Log.Debug(e);
                throw;
            }
        }



        /// <summary>
        /// Gets the urls of the chapters from the table of contents, will include https://
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        public List<string> GetChapterUrls(string selector)
        {
            try
            {
                new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.ElementToBeClickable(By.ClassName(selector)));
                var chapterListSection = _driver.FindElements(By.ClassName(selector));

                //Similar to using two nested foreach loops, will flatten out all the IWebElements thanks to SelectMany()
                List<string> chapters = chapterListSection.SelectMany(chapterListElement => chapterListElement.FindElements(By.TagName("a")))
                    .Select(linkElement => linkElement.GetAttribute("href")).ToList();
                return chapters;
            }
            catch (NoSuchElementException)
            {
                Logger.Log.Error($"No such element exception in GetChapterUrls. \n Selector: {selector}\n Url: {_driver.Url}");
            }
            catch (StaleElementReferenceException) { }

            return new List<string>();
        }

        /// <summary>
        /// Goes to the table of contents and gets chapters urls listed by pagiation, uri should be something that can be incremented
        /// </summary>
        /// <param name="startPagitation"></param>
        /// <param name="lastPagitation"></param>
        /// <returns></returns>
        public List<string> GetChaptersUsingPagitation(int startPagitation, int lastPagitation, string siteUrl)
        {
            string baseTableOfContentUrl = siteUrl + "?page={0}";
            List<string> chapterUrls = new List<string>();

            for (int i = startPagitation; i <= lastPagitation; i++)
            {

                string tableOfContentUrl = string.Format(baseTableOfContentUrl, i);
                try
                {
                    _driver.Navigate().GoToUrl(tableOfContentUrl);
                    new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.UrlContains(tableOfContentUrl));
                    var chapterUrlsOnContentPage = GetChapterUrls("list-chapter");
                    if (chapterUrlsOnContentPage != null)
                        chapterUrls.AddRange(chapterUrlsOnContentPage);
                }
                catch (WebDriverException e)
                {
                    Logger.Log.Error($"Error occured while navigating to {tableOfContentUrl}. Error: {e}");
                }

            }
            return chapterUrls;
        }


        public string GetLastTableOfContentPageUrl(string selector)
        {
            try
            {
                new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.ElementToBeClickable(By.ClassName(selector)));
                var lastChapter = _driver.FindElement(By.ClassName(selector)).FindElement(By.TagName("a")).GetAttribute("href");
                return lastChapter;
            }
            catch (NoSuchElementException)
            {
                Logger.Log.Error($"No such element exception in GetChapterUrls. \n Selector: {selector}\n Url: {_driver.Url}");
            }
            catch (StaleElementReferenceException) { }
            return string.Empty;
        }


        /// <summary>
        /// Gets the title of the novel
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        public string GetLatestChapterUsingSelenium(string selector)
        {
            try
            {
                new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.CssSelector(selector)));
                var chapters = _driver.FindElement(By.CssSelector(selector));
                return chapters.Text;
            }
            catch (NoSuchElementException)
            {

            }
            catch (StaleElementReferenceException) { }

            return string.Empty;
        }

        /// <summary>
        /// Gets the title of the novel
        /// </summary>
        /// <param name="titleSelector">element selector to get the title</param>
        /// <returns></returns>
        public string GetTitle(string titleSelector)
        {
            new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.CssSelector(titleSelector)));
            var titleElements = _driver.FindElements(By.CssSelector(titleSelector));
            if (titleElements.Count == 0)
                throw new Exception("No title elements found.");
            else if (titleElements.Count > 1 && titleElements[1].Displayed) // title will be the second
                return titleElements[1].Text;
            else
                return titleElements[0].Text;
        }
        #endregion
    }


}
