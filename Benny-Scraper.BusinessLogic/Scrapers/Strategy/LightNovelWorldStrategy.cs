using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Collections.Specialized;
using System.Web;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    namespace Impl
    {
        public abstract class LightNovelWorldInitializer : NovelDataInitializer
        {
            public static async Task FetchNovelContent(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData)
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
                    if (attribute == Attr.ThumbnailUrl) // always get a 403 forbidden error when trying to get the thumbnail image from lightnovelworld
                    {
                        using var client = scraperData.HttpClientFactory?.CreateClient() ?? new HttpClient();
                        var response = client.GetAsync($"https://webnovelworld.org{scraperData.SiteTableOfContents.AbsolutePath}").Result;
                        HtmlDocument htmlDocumentForThumbnail = new HtmlDocument();
                        htmlDocumentForThumbnail.LoadHtml(response.Content.ReadAsStringAsync().Result);
                        await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocumentForThumbnail, scraperData);
                    }
                    else
                    {
                        await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData);
                    }
                }
            }
        }
    }
    public class LightNovelWorldStrategy : ScraperStrategy
    {
        private Uri? _chaptersUri; // the url of the chapters pages are different from the table of contents page
        private readonly string _latestChapterXpath = "//*[@id='chapter-list-page']/header/p[2]/a";

        public override async Task<NovelDataBuffer> ScrapeAsync()
        {
            Logger.Info($"Starting scraper for {this.GetType().Name}");

            SetBaseUri(ScraperData.SiteTableOfContents);

            var (htmlDocument, uri) = await LoadHtmlAsync(ScraperData.SiteTableOfContents);
            var novelDataBuffer = FetchNovelDataFromTableOfContents(htmlDocument);
            novelDataBuffer.NovelUrl = uri.ToString();

            _chaptersUri = new Uri(uri + "/chapters");

            (htmlDocument, uri) = await LoadHtmlAsync(_chaptersUri);

            var decodedHtmlDocument = DecodeHtml(htmlDocument);

            int pageToStopAt = GetLastTableOfContentsPageNumber(decodedHtmlDocument);
            SetCurrentChapterUrl(htmlDocument, novelDataBuffer); // buffer is passed by reference so this will update the novelDataBuffer object

            var (chapterLinks, lastTableOfContentsUrl) = await GetPaginatedChapterLinksAsync(_chaptersUri, true, pageToStopAt);
            novelDataBuffer.ChapterLinks = chapterLinks;
            novelDataBuffer.ChapterTitles = chapterLinks.Select(cl => cl.Title ?? string.Empty).ToList();
            novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsUrl;


            // Sort chapters based on site configuration
            SortChapters(novelDataBuffer);

            return novelDataBuffer;
        }

        protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        {
            throw new NotImplementedException();
        }

        protected override async Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = new NovelDataBuffer();
            try
            {
                await LightNovelWorldInitializer.FetchNovelContent(novelDataBuffer, htmlDocument, ScraperData);
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
                currentChapterUrl = new Uri(ScraperData.BaseUri, currentChapterUrl).ToString();
                novelDataBuffer.CurrentChapterUrl = currentChapterUrl;
            }
        }

        private int GetLastTableOfContentsPageNumber(HtmlDocument htmlDocument)
        {
            HtmlNodeCollection paginationNodes = htmlDocument.DocumentNode.SelectNodes(ScraperData.SiteConfig.Selectors.TableOfContents.TableOfContentsPaginationListItems);
            int paginationCount = paginationNodes.Count;

            // Guard: Single page or no pagination
            if (paginationCount <= 1)
                return 1;

            // Determine which node contains the last page number
            HtmlNode lastPageNode;
            if (paginationCount == TotalPossiblePaginationTabs)
            {
                lastPageNode = htmlDocument.DocumentNode.SelectSingleNode(ScraperData.SiteConfig.Selectors.TableOfContents.LastTableOfContentsPage);
            }
            else
            {
                // Get the second last node which is the last page number
                lastPageNode = paginationNodes[paginationCount - 2];
                lastPageNode = lastPageNode.SelectSingleNode("a");
            }

            var lastPageUrl = lastPageNode.Attributes["href"].Value;
            var lastPageUri = new Uri(lastPageUrl, UriKind.RelativeOrAbsolute);

            // Convert relative URL to absolute if needed
            if (!lastPageUri.IsAbsoluteUri)
            {
                lastPageUri = new Uri(ScraperData.BaseUri + lastPageUrl);
            }

            NameValueCollection query = HttpUtility.ParseQueryString(lastPageUri.Query);
            var pageNumber = query["page"];

            int pageToStopAt = 1;
            int.TryParse(pageNumber, out pageToStopAt);

            return pageToStopAt;
        }
    }
}
