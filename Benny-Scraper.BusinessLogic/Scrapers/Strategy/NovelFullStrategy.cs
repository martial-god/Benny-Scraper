using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using HtmlAgilityPack;
using System.Diagnostics;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy
{
    namespace Impl
    {
        public abstract class NovelFullInitializer : NovelDataInitializer
        {
            // Brad: Ideally this method would be pure virtual and we would get a forcible reminder to implement it on each
            // child class, but C# doesn't allow static virtual methods or mixing of abstract and non-abstract methods and
            // the implementation would require both.
            public static async Task FetchNovelContentAsync(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData)
            {
                Debug.Assert(scraperData.SiteTableOfContents != null, "scraperData.SiteTableOfContents != null");
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
                    await FetchContentByAttributeAsync(attribute, novelDataBuffer, htmlDocument, scraperData);
                }

                var fullCurrentChapterUrl = new Uri(scraperData.SiteTableOfContents, novelDataBuffer.CurrentChapterUrl?.TrimStart('/')).ToString();
                var fullThumbnailUrl = new Uri(scraperData.SiteTableOfContents, novelDataBuffer.ThumbnailUrl?.TrimStart('/')).ToString();
                var fullLastTableOfContentUrl = new Uri(scraperData.SiteTableOfContents, novelDataBuffer.LastTableOfContentsPageUrl?.TrimStart('/')).ToString();

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
            if (ScraperData.SiteTableOfContents == null)
            {
                throw new ArgumentNullException(nameof(ScraperData.SiteTableOfContents), "SiteTableOfContents cannot be null.");
            }

            SetBaseUri(ScraperData.SiteTableOfContents);
            var (htmlDocument, uri) = await LoadHtmlAsync(ScraperData.SiteTableOfContents);

            try
            {
                var novelDataBuffer = await BuildNovelDataAsync(htmlDocument);
                novelDataBuffer.NovelUrl = uri.ToString();

                return novelDataBuffer;
            }
            catch (Exception e)
            {
                Logger.Error($"Error while getting novel data. {e}");
                throw;
            }
        }

        protected override async Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = new NovelDataBuffer();
            try
            {
                await NovelFullInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, ScraperData);
                return novelDataBuffer;
            }
            catch (Exception e)
            {
                Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
            }

            return novelDataBuffer;
        }

        protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        {
            throw new NotImplementedException();
        }

        private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument);
            var pageToStopAt = GetPageNumberFromUrlQuery(novelDataBuffer.LastTableOfContentsPageUrl, ScraperData.BaseUri);

            var (chapterLinks, lastTableOfContentsUrl) = await GetPaginatedChapterLinksAsync(ScraperData.SiteTableOfContents, true, pageToStopAt);

            novelDataBuffer.ChapterLinks.ReplaceWith(chapterLinks);
            novelDataBuffer.ChapterTitles.ReplaceWith(chapterLinks.Select(cl => cl.Title ?? string.Empty));
            novelDataBuffer.LastTableOfContentsPageUrl = lastTableOfContentsUrl;

            // Sort chapters based on site configuration
            SortChapters(novelDataBuffer);

            return novelDataBuffer;
        }
    }
}