using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;


namespace Benny_Scraper
{
    internal class NovelPage
    {
        private readonly IWebDriver _driver;
        
        public NovelPage(IWebDriver driver)
        {
            _driver = driver;
            Logger.Setup();
        }

        /// <summary>
        /// Gets the urls of the chapters, will include the https://
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

            return null;
        }
        
        public void GoToContentPageUrl(int lastChapterNumber)
        {
            string baseTableOfContentUrl = "https://novelfull.com/paragon-of-sin.html?page={0}";

            for (int i = 2; i <= lastChapterNumber; i++)
            {
                string tableOfContentUrl = string.Format(baseTableOfContentUrl, i);
                _driver.Navigate().GoToUrl(tableOfContentUrl);
                new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.UrlContains(tableOfContentUrl));
                List<string> chapters = GetChapterUrls("list-chapter");
            }
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
            return null;
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
