using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Collections.Specialized;
using System.Web;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    public class WebNovelPubStrategy : ScraperStrategy
    {
        private Uri? _chaptersUri;

        public override async Task<NovelData> ScrapeAsync()
        {
            Logger.Info("Starting scraper for Web");

            SetBaseUri(SiteTableOfContents);

            HtmlDocument htmlDocument = await LoadHtmlDocumentFromUrlAsync(SiteTableOfContents);

            if (htmlDocument == null)
            {
                Logger.Debug($"Error while trying to load HtmlDocument. \n");
                return null;
            }

            NovelData novelData = GetNovelDataFromTableOfContent(htmlDocument);

            _chaptersUri = new Uri(SiteTableOfContents.ToString() + "/chapters");

            htmlDocument = await LoadHtmlDocumentFromUrlAsync(_chaptersUri);

            HtmlDocument decodedHtmlDocument = DecodeHtml(htmlDocument);

            int pageToStopAt = GetLastTableOfContentsPageNumber(decodedHtmlDocument);

            var (chapterUrls, lastTableOfContentsUrl) = await GetPaginatedChapterUrlsAsync(_chaptersUri, true, pageToStopAt);

            novelData.ChapterUrls = chapterUrls;
            novelData.LastTableOfContentsPageUrl = lastTableOfContentsUrl;
            novelData.Genres = new List<string>();

            return novelData;
        }


        public override NovelData GetNovelDataFromTableOfContent(HtmlDocument htmlDocument)
        {
            NovelData novelData = new NovelData();

            HtmlNodeCollection novelTitleNodes = htmlDocument.DocumentNode.SelectNodes(SiteConfig.Selectors.NovelTitle);
            if (novelTitleNodes.Any())
            {
                novelData.Title = novelTitleNodes.First().InnerText.Trim();
            }

            return novelData;
        }

        List<string> GetGenres(HtmlDocument htmlDocument, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }

        private int GetLastTableOfContentsPageNumber(HtmlDocument htmlDocument)
        {
            HtmlNodeCollection paginationNodes = htmlDocument.DocumentNode.SelectNodes(SiteConfig.Selectors.TableOfContnetsPaginationListItems);
            int paginationCount = paginationNodes.Count;

            int pageToStopAt = 1;
            if (paginationCount > 1)
            {
                HtmlNode lastPageNode = null;
                if (paginationCount == TotalPossiblePaginationTabs)
                {
                    lastPageNode = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.LastTableOfContentsPage);
                }
                else
                {
                    lastPageNode = paginationNodes[paginationCount - 2]; // Get the second last node which is the last page number
                    lastPageNode = lastPageNode.SelectSingleNode("a");
                }

                string lastPageUrl = lastPageNode.Attributes["href"].Value;

                Uri lastPageUri = new Uri(lastPageUrl, UriKind.RelativeOrAbsolute);

                // If the URL is relative, make sure to add a scheme and host
                if (!lastPageUri.IsAbsoluteUri) // like this: /novel/the-authors-pov-14051336/chapters?page=9
                {
                    lastPageUri = new Uri(this.BaseUri.ToString() + lastPageUrl);
                }

                NameValueCollection query = HttpUtility.ParseQueryString(lastPageUri.Query);

                string pageNumber = query["page"];
                int.TryParse(pageNumber, out pageToStopAt);
            }

            return pageToStopAt;
        }
    }
}
