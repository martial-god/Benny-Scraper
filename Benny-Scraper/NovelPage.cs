using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Benny_Scraper.Models;
using Microsoft.IdentityModel.Tokens;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;


namespace Benny_Scraper
{
    internal class NovelPage
    {
        private readonly IWebDriver _driver;
        public class List<T1, T2>
        {
        }

        public NovelPage(IWebDriver driver)
        {
            _driver = driver;
            Logger.Setup();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="url">url that contains the table of contents</param>
        /// <returns></returns>
        public  async Task<Novel> BuildNovelAsync(string url)
        {
            try
            {
                _driver.Navigate().GoToUrl(url);
                new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.UrlContains(url));

                var title = GetTitle(".title");
                var latestChapter = GetLatestChapter(".l-chapters a span.chapter-text");
                string lastPageUrl = GetLastTableOfContentPageUrl("last");
                int lastPage = Regex.Match(lastPageUrl, @"\d+").Success ? Convert.ToInt32(Regex.Match(lastPageUrl, @"\d+").Value) : 0;
                // use List<string, string> and have the GetChapters... return the content as well.
                List<string> chapterUrls = GetChaptersUsingPagitation(1, lastPage);
                IEnumerable<ChapterData> chapterData = await GetChapterDatasAsync(chapterUrls, "chapter-text");
                var firstChapterUrl = chapterUrls.First();
                var lastChapterUrl = chapterUrls.Last();
                
                List<Chapter> chapters = chapterData.Select(data => new Chapter
                {
                    Url = data.Url ?? "",
                    Content = data.Content ?? "",
                    Title = data.Title ?? "",
                    DateCreated = DateTime.UtcNow                    

                }).ToList();

                Novel novel = new Novel
                {
                    Title = title,
                    SiteName = "Novelfull",
                    Url = url,
                    FirstChapter = firstChapterUrl,
                    CurrentChapter = latestChapter,
                    TotalChapters = chapterUrls.Count,
                    Chapters = chapters,
                    DateCreated = DateTime.UtcNow,
                    Status = "ONGOING",
                };
                return novel;
            }
            catch (Exception e)
            {
                Logger.Log.Error(e);
                throw;
            }
            return new Novel();
        }

        public async Task<List<ChapterData>> GetChapterDatasAsync(List<string> chapterUrls, string titleSelector)
        {
            try
            {
                List<ChapterData> chapterData = new List<ChapterData>();
                foreach (var url in chapterUrls)
                {
                    await Task.Run(() => {
                        _driver.Navigate().GoToUrl(url);
                        new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.UrlContains(url));
                        
                        var title = _driver.FindElement(By.ClassName(titleSelector)).Text ?? string.Empty;
                        var content = _driver.FindElements(By.TagName("p")).Select(x => x.Text).ToString() ?? string.Empty;
                        chapterData.Add(new ChapterData
                        {
                            Title = title,
                            Content = content,
                            Url = url
                        });
                    });
                }
                return chapterData;
            }
            catch (Exception e)
            {
                Logger.Log.Error(e);
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
        /// Goes to the table of contents and gets chapters urls listed by pagiation, url should be something that can be incremented
        /// </summary>
        /// <param name="startPagitation"></param>
        /// <param name="lastPagitation"></param>
        /// <returns></returns>
        public List<string> GetChaptersUsingPagitation(int startPagitation,int lastPagitation)
        {
            string baseTableOfContentUrl = "https://novelfull.com/paragon-of-sin.html?page={0}";
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
        public string GetLatestChapter(string selector)
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
            var titleElements =  _driver.FindElements(By.CssSelector(titleSelector));
            if (titleElements.Count == 0)
                throw new Exception("No title elements found.");
            else if (titleElements.Count > 1 && titleElements[1].Displayed) // title will be the second
                return titleElements[1].Text;
            else
                return titleElements[0].Text;
        }

        // Gets the total chapters of a novel
    }

    
}
