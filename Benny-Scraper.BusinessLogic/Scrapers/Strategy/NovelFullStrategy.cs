using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    namespace Impl
    {
        public class NovelFullInitializer : NovelDataInitializer
        {
            //Brad: Ideally this method would be pure virtual and we would get a forcible reminder to implement it on each
            //child class, but C# doesn't allow static virtual methods or mixing of abstract and non-abstract methods and
            //the implementation would require both.
            public static void FetchNovelContent(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData)
            {
                var tableOfContents = scraperData.SiteTableOfContents;
                var attributesToFetch = new List<Attr>()
                {
                    Attr.Author,
                    Attr.Title,
                    Attr.NovelRating,
                    Attr.TotalRatings,
                    Attr.Description,
                    Attr.Genres,
                    Attr.AlternativeNames,
                    Attr.NovelStatus,
                    Attr.ThumbnailUrl,
                    Attr.LastTableOfContentsPage,
                    Attr.FirstChapterUrl,
                    Attr.CurrentChapter
                };

                foreach (var attribute in attributesToFetch)
                {
                    FetchContentByAttribute(attribute, novelDataBuffer, htmlDocument, scraperData);
                }

                var fullCurrentChapterUrl = new Uri(tableOfContents, novelDataBuffer.CurrentChapterUrl?.TrimStart('/')).ToString();
                var fullThumbnailUrl = new Uri(tableOfContents, novelDataBuffer.ThumbnailUrl?.TrimStart('/')).ToString();
                var fullLastTableOfContentUrl = new Uri(tableOfContents, novelDataBuffer.LastTableOfContentsPageUrl?.TrimStart('/')).ToString();

                novelDataBuffer.ThumbnailUrl = fullThumbnailUrl;
                novelDataBuffer.LastTableOfContentsPageUrl = fullCurrentChapterUrl;
                novelDataBuffer.LastTableOfContentsPageUrl = fullLastTableOfContentUrl;
            }
        }
    }

    public class NovelFullStrategy : ScraperStrategy
    {
        public override async Task<NovelDataBuffer> ScrapeAsync()
        {
            Logger.Info($"Getting novel data for {this.GetType().Name}");

            SetBaseUri(_scraperData.SiteTableOfContents);
            var (htmlDocument, uri) = await LoadHtmlAsync(_scraperData.SiteTableOfContents);

            try
            {
                NovelDataBuffer novelDataBuffer = await BuildNovelDataAsync(htmlDocument);
                novelDataBuffer.NovelUrl = uri.ToString();

                return novelDataBuffer;
            }
            catch (Exception e)
            {
                Logger.Error($"Error while getting novel data. {e}");
                throw;
            }
        }

        private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = FetchNovelDataFromTableOfContents(htmlDocument);
            int pageToStopAt = GetPageNumberFromUrlQuery(novelDataBuffer.LastTableOfContentsPageUrl, _scraperData.BaseUri);

            var (chapterUrls, lastTableOfContentsUrl) = await GetPaginatedChapterUrlsAsync(_scraperData.SiteTableOfContents, true, pageToStopAt);

            novelDataBuffer.ChapterUrls = chapterUrls;
            novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsUrl;

            return novelDataBuffer;
        }

        public override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = new NovelDataBuffer();
            try
            {
                NovelFullInitializer.FetchNovelContent(novelDataBuffer, htmlDocument, _scraperData);
                return novelDataBuffer;
            }
            catch (Exception e)
            {
                Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
            }

            return novelDataBuffer;
        }
    }
}