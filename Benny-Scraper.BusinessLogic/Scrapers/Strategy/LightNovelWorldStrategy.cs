using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Collections.Specialized;
using System.Web;

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
                    Attr.Status,
                    Attr.Description,
                    Attr.ThumbnailUrl,
                    Attr.Genres,
                    Attr.LatestChapter
                };
                foreach (var attribute in attributesToFetch)
                {
                    if (attribute == Attr.ThumbnailUrl) // always get a 403 forbidden error when trying to get the thumbnail image from lightnovelworld
                    {
                        HttpClient client = new HttpClient();
                        var response = client.GetAsync($"https://webnovelpub.com{scraperData.SiteTableOfContents.AbsolutePath}").Result;
                        HtmlDocument htmlDocumentForThumbnail = new HtmlDocument();
                        htmlDocumentForThumbnail.LoadHtml(response.Content.ReadAsStringAsync().Result);
                        FetchContentByAttribute(attribute, novelDataBuffer, htmlDocumentForThumbnail, scraperData);
                    }
                    else
                    {
                        FetchContentByAttribute(attribute, novelDataBuffer, htmlDocument, scraperData);
                    }
                }
            }
        }
    }
    public class LightNovelWorldStrategy : ScraperStrategy
    {
        private Uri? _chaptersUri;
        private readonly string _latestChapterXpath = "//*[@id='chapter-list-page']/header/p[2]/a";

        public override async Task<NovelDataBuffer> ScrapeAsync()
        {
            Logger.Info($"Starting scraper for {this.GetType().Name}");

            SetBaseUri(_scraperData.SiteTableOfContents);

            var htmlDocument = await LoadHtmlAsync(_scraperData.SiteTableOfContents);
            var novelDataBuffer = FetchNovelDataFromTableOfContents(htmlDocument);

            _chaptersUri = new Uri(_scraperData.SiteTableOfContents + "/chapters");

            htmlDocument = await LoadHtmlAsync(_chaptersUri);

            var decodedHtmlDocument = DecodeHtml(htmlDocument);

            int pageToStopAt = GetLastTableOfContentsPageNumber(decodedHtmlDocument);
            SetCurrentChapterUrl(htmlDocument, novelDataBuffer); // buffer is passed by reference so this will update the novelDataBuffer object

            var (chapterUrls, lastTableOfContentsUrl) = await GetPaginatedChapterUrlsAsync(_chaptersUri, true, pageToStopAt);
            novelDataBuffer.ChapterUrls = chapterUrls;
            novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsUrl;

            return novelDataBuffer;
        }

        public override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
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
            }

            return novelDataBuffer;
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
            HtmlNodeCollection paginationNodes = htmlDocument.DocumentNode.SelectNodes(_scraperData.SiteConfig.Selectors.TableOfContnetsPaginationListItems);
            int paginationCount = paginationNodes.Count;

            int pageToStopAt = 1;
            if (paginationCount > 1)
            {
                HtmlNode lastPageNode;
                if (paginationCount == TotalPossiblePaginationTabs)
                {
                    lastPageNode = htmlDocument.DocumentNode.SelectSingleNode(_scraperData.SiteConfig.Selectors.LastTableOfContentsPage);
                }
                else
                {
                    lastPageNode = paginationNodes[paginationCount - 2]; // Get the second last node which is the last page number
                    lastPageNode = lastPageNode.SelectSingleNode("a");
                }

                var lastPageUrl = lastPageNode.Attributes["href"].Value;
                var lastPageUri = new Uri(lastPageUrl, UriKind.RelativeOrAbsolute);

                // If the URL is relative, make sure to add a scheme and host
                if (!lastPageUri.IsAbsoluteUri) // like this: /novel/the-authors-pov-14051336/chapters?page=9
                {
                    lastPageUri = new Uri(_scraperData.BaseUri + lastPageUrl);
                }

                NameValueCollection query = HttpUtility.ParseQueryString(lastPageUri.Query);

                var pageNumber = query["page"];
                int.TryParse(pageNumber, out pageToStopAt);
            }

            return pageToStopAt;
        }
    }
}
