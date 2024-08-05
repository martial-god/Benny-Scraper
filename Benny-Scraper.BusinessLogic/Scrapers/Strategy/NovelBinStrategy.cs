using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    namespace Impl
    {
        public class NovelBinInitializer : NovelDataInitializer
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
                    Attr.NovelStatus,
                    Attr.ThumbnailUrl,
                    Attr.CurrentChapter
                };

                foreach (var attribute in attributesToFetch)
                {
                    FetchContentByAttribute(attribute, novelDataBuffer, htmlDocument, scraperData);
                }
            }
        }
    }
    public class NovelBinStrategy : ScraperStrategy
    {
        private Uri? _chaptersUri; // the url of the chapters pages are different from the table of contents page
        public override async Task<NovelDataBuffer> ScrapeAsync()
        {
            Logger.Info($"Getting novel data for {this.GetType().Name}");

            SetBaseUri(_scraperData.SiteTableOfContents);
            var (htmlDocument, uri) = await LoadHtmlAsync(_scraperData.SiteTableOfContents);

            try
            {
                NovelDataBuffer novelDataBuffer = await BuildNovelDataAsync(htmlDocument);
                novelDataBuffer.NovelUrl = uri.ToString();

                _chaptersUri = new Uri(novelDataBuffer.NovelUrl + "#tab-chapters-title");
                (htmlDocument, uri) = await LoadHtmlAsync(_chaptersUri); // will need to suse this to get the chapetrs

                return novelDataBuffer;
            }
            catch (Exception e)
            {
                Logger.Error($"Error while getting novel data. {e}");
                throw;
            }
        }

        public override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = new NovelDataBuffer();
            try
            {
                NovelBinInitializer.FetchNovelContent(novelDataBuffer, htmlDocument, _scraperData);
                return novelDataBuffer;
            }
            catch (Exception e)
            {
                Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
            }

            return novelDataBuffer;
        }

        private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument);
            return novelDataBuffer;
        }
    }
}
