﻿using System.Collections.Specialized;
using System.Web;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using OpenQA.Selenium;
using SeleniumExtras.WaitHelpers;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    namespace Impl
    {
        public class LightNovelWorldInitializer : NovelDataInitializer
        {
            public static void FetchNovelContent(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData)
            {
                var attributesToFetch = new List<Attr>()
                {
                    Attr.Title,
                    Attr.Author,
                    Attr.NovelStatus,
                    Attr.Description,
                    Attr.ThumbnailUrl,
                    Attr.Genres,
                    Attr.CurrentChapter
                };
                foreach (var attribute in attributesToFetch)
                {
                    try
                    {
                        if (attribute ==
                            Attr.ThumbnailUrl) // always get a 403 forbidden error when trying to get the thumbnail image from lightnovelworld
                        {
                            HttpClient client = new HttpClient();
                            var response = client
                                .GetAsync($"https://webnovelworld.org{scraperData.SiteTableOfContents.AbsolutePath}")
                                .Result;
                            HtmlDocument htmlDocumentForThumbnail = new HtmlDocument();
                            htmlDocumentForThumbnail.LoadHtml(response.Content.ReadAsStringAsync().Result);
                            FetchContentByAttribute(attribute, novelDataBuffer, htmlDocumentForThumbnail, scraperData);
                        }
                        else
                        {
                            FetchContentByAttribute(attribute, novelDataBuffer, htmlDocument, scraperData);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error when getting attribute {attribute}: {ex.Message}");
                    }
                }
            }
        }
    }

    public class LightNovelWorldStrategy(IDriverFactory driverFactory) : ScraperStrategy(driverFactory)
    {
        private readonly IDriverFactory _driverFactory = driverFactory;
        protected override bool RequiresBrowser => true;
        private readonly string _latestChapterXpath = "//*[@id='chapter-list-page']/header/p[2]/a";

        public override async Task<NovelDataBuffer> ScrapeAsync()
        {
            Logger.Info($"Starting scraper for {this.GetType().Name}");

            SetBaseUri(_scraperData.SiteTableOfContents);

            // Get table of contents
            try
            {
                var driverObj = await _driverFactory.CreateDriverAsync(_scraperData.SiteTableOfContents.ToString(), isHeadless: false);

                HtmlDocument htmlDocument = _driverFactory.GetPageContent(driverObj.Id);

                var novelDataBuffer = FetchNovelDataFromTableOfContents(htmlDocument);
                novelDataBuffer.NovelUrl = driverObj.Driver.Url;

                // the url of the chapters pages are different from the table of contents page
                Uri chaptersUri = new Uri(driverObj.Driver.Url + "/chapters");

                _driverFactory.GoToAndWait<IWebElement>(driverObj.Id, chaptersUri?.ToString(),
                    drv =>
                    {
                        // First wait until document.readyState is complete.
                        var readyState = ((IJavaScriptExecutor)drv).ExecuteScript("return document.readyState").ToString();
                        if (!readyState.Equals("complete"))
                            return null;

                        // Then wait until the target element is visible.
                        return ExpectedConditions.ElementIsVisible(By.XPath(_scraperData.SiteConfig?.Selectors.NovelTitle))(drv);
                    }
                    );

                htmlDocument = _driverFactory.GetPageContent(driverObj.Id);

                int pageToStopAt = GetLastTableOfContentsPageNumber(htmlDocument);
                SetCurrentChapterUrl(htmlDocument, novelDataBuffer); // buffer is passed by reference so this will update the novelDataBuffer object

                var (chapterUrls, lastTableOfContentsUrl) =
                    await GetPaginatedChapterUrlsAsync(chaptersUri, true, pageToStopAt, driverObj.Id);
                novelDataBuffer.ChapterUrls = chapterUrls;
                novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsUrl;

                return novelDataBuffer;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error when getting novel info from title page. {ex}");
                _driverFactory.DisposeAllDrivers();
                throw;
            }
            finally
            {
                Console.WriteLine($"Got Novel Data");
            }
        }

        protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = new NovelDataBuffer();
            try
            {
                LightNovelWorldInitializer.FetchNovelContent(novelDataBuffer, htmlDocument, _scraperData);
                return novelDataBuffer;
            }
            catch (Exception e)
            {
                Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
                throw new Exception($"Error occurred while getting novel data from table of contents. Error: {e}");
            }
        }

        private void SetCurrentChapterUrl(HtmlDocument htmlDocument, NovelDataBuffer novelDataBuffer)
        {
            var currentChapterNode = htmlDocument.DocumentNode.SelectSingleNode(_latestChapterXpath);
            var currentChapterUrl = currentChapterNode.Attributes["href"].Value;
            if (!NovelDataInitializer.IsValidHttpUrl(currentChapterUrl))
            {
                currentChapterUrl = new Uri(_scraperData.BaseUri, currentChapterUrl).ToString();
                novelDataBuffer.CurrentChapterUrl = currentChapterUrl;
            }
        }

        private int GetLastTableOfContentsPageNumber(HtmlDocument htmlDocument)
        {
            HtmlNodeCollection paginationNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig?.Selectors.TableOfContentsPaginationListItems);
            int paginationCount = paginationNodes.Count;

            int pageToStopAt = 1;
            if (paginationCount > 1)
            {
                HtmlNode lastPageNode;
                if (paginationCount == TotalPossiblePaginationTabs)
                    lastPageNode = htmlDocument.DocumentNode.SelectSingleNode(_scraperData.SiteConfig?.Selectors.LastTableOfContentsPage);
                else
                {
                    lastPageNode = paginationNodes[paginationCount - 2]; // Get the second last node which is the last page number
                    lastPageNode = lastPageNode.SelectSingleNode("a");
                }

                var lastPageUrl = lastPageNode.Attributes["href"].Value;
                var lastPageUri = new Uri(lastPageUrl, UriKind.RelativeOrAbsolute);

                // If the URL is relative, make sure to add a scheme and host
                if (!lastPageUri.IsAbsoluteUri) // like this: /novel/the-authors-pov-14051336/chapters?page=9
                    lastPageUri = new Uri(_scraperData.BaseUri + lastPageUrl);

                NameValueCollection query = HttpUtility.ParseQueryString(lastPageUri.Query);

                var pageNumber = query["page"];
                int.TryParse(pageNumber, out pageToStopAt);
            }

            return pageToStopAt;
        }
    }
}
