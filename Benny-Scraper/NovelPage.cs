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

        public List<string> GetChapterUrls(string selector)
        {
            var chapters = new List<String>();
            try
            {
                
                new WebDriverWait(_driver, TimeSpan.FromSeconds(10)).Until(ExpectedConditions.ElementToBeClickable(By.ClassName(selector)));
                var chapterListSection = _driver.FindElements(By.ClassName(selector));
                if (chapterListSection.Count == 0)
                    return null;
                if (chapterListSection.Count > 0)
                {
                    foreach (var chapterList in chapterListSection)
                    {
                        var chapterLinks = chapterList.FindElements(By.TagName("a"));
                        
                        foreach (var chapterLink in chapterLinks)
                        {
                            chapters.Add(chapterLink.GetAttribute("href"));
                        }
                        
                    }
                }

                return chapters;
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
            else if (titleElements.Count > 1) // title will be the second
                return titleElements[1].Text;
            else
                return titleElements[0].Text;
        }

        // Gets the total chapters of a novel
    }
}
