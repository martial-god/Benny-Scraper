using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Globalization;
using NLog;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;

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
                    Attr.Status,
                    Attr.ThumbnailUrl,
                    Attr.LastTableOfContentsPage,
                    Attr.FirstChapterUrl,
                    Attr.LatestChapter
                };
                foreach (var attribute in attributesToFetch)
                {
                    FetchContentByAttribute(attribute, novelDataBuffer, htmlDocument, scraperData);
                }

                //TODO: Brad: I notice that the name LatestChapter and CurrentChapter are both used to refer to the same thing.
                //  As is, FetchContentByAttribute(Attr.LatestChapter ...) sets the NovelDataBuffer's CurrentChapterUrl property.
                //  It is probably best if the two naming schemes are unified, but I don't want to change the data members
                //  of NovelDataBuffer without consulting you first.
                var fullLatestChapterUrl = new Uri(tableOfContents, novelDataBuffer.CurrentChapterUrl?.TrimStart('/')).ToString();
                var fullThumbnailUrl = new Uri(tableOfContents, novelDataBuffer.ThumbnailUrl?.TrimStart('/')).ToString();
                var fullLastTableOfContentUrl = new Uri(tableOfContents, novelDataBuffer.LastTableOfContentsPageUrl?.TrimStart('/')).ToString();

                novelDataBuffer.ThumbnailUrl = fullThumbnailUrl;
                novelDataBuffer.LastTableOfContentsPageUrl = fullLatestChapterUrl;
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
            var htmlDocument = await LoadHtmlAsync(_scraperData.SiteTableOfContents);
            
            try
            {
                NovelDataBuffer novelDataBuffer = await BuildNovelDataAsync(htmlDocument);

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

            int pageToStopAt = FetchLastTableOfContentsPageNumber(htmlDocument);
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

        private int FetchLastTableOfContentsPageNumber(HtmlDocument htmlDocument)
        {
            Logger.Info($"Getting last table of contents page number at {_scraperData.SiteConfig?.Selectors.LastTableOfContentsPage}");
            try
            {
                HtmlNode lastPageNode = htmlDocument.DocumentNode.SelectSingleNode(_scraperData.SiteConfig?.Selectors.LastTableOfContentsPage);
                string lastPage = lastPageNode.Attributes[_scraperData.SiteConfig?.Selectors.LastTableOfContentPageNumberAttribute].Value;

                int lastPageNumber = int.Parse(lastPage, NumberStyles.AllowThousands);

                if (_scraperData.SiteConfig?.PageOffSet > 0)
                {
                    lastPageNumber += _scraperData.SiteConfig.PageOffSet;
                }

                Logger.Info($"Last table of contents page number is {lastPage}");
                return lastPageNumber;
            }
            catch (Exception e)
            {
                Logger.Error($"Error when getting last page table of contents page number. {e}");
                throw;
            }
        }

    }
}
