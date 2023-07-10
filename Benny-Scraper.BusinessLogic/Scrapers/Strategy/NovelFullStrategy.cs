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
            public static void FetchNovelContent(NovelData novelData, HtmlDocument htmlDocument, ScraperData scraperData)
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
                    FetchContentByAttribute(attribute, novelData, htmlDocument, scraperData);
                }

                //TODO: Brad: I notice that the name LatestChapter and CurrentChapter are both used to refer to the same thing.
                //  As is, FetchContentByAttribute(Attr.LatestChapter ...) sets the NovelData's CurrentChapterUrl property.
                //  It is probably best if the two naming schemes are unified, but I don't want to change the data members
                //  of NovelData without consulting you first.
                var fullLatestChapterUrl = new Uri(tableOfContents, novelData.CurrentChapterUrl?.TrimStart('/')).ToString();
                var fullThumbnailUrl = new Uri(tableOfContents, novelData.ThumbnailUrl?.TrimStart('/')).ToString();
                var fullLastTableOfContentUrl = new Uri(tableOfContents, novelData.LastTableOfContentsPageUrl?.TrimStart('/')).ToString();

                novelData.ThumbnailUrl = fullThumbnailUrl;
                novelData.LastTableOfContentsPageUrl = fullLatestChapterUrl;
                novelData.LastTableOfContentsPageUrl = fullLastTableOfContentUrl;
            }
        }
    }
    
    public class NovelFullStrategy : ScraperStrategy
    {
        public override async Task<NovelData> ScrapeAsync()
        {
            Logger.Info($"Getting novel data for {this.GetType().Name}");

            SetBaseUri(_scraperData.SiteTableOfContents);
            var htmlDocument = await LoadHtmlAsync(_scraperData.SiteTableOfContents);
            
            try
            {
                NovelData novelData = await BuildNovelDataAsync(htmlDocument);

                return novelData;
            }
            catch (Exception e)
            {
                Logger.Error($"Error while getting novel data. {e}");
                throw;
            }
        }

        private async Task<NovelData> BuildNovelDataAsync(HtmlDocument htmlDocument)
        {
            var novelData = FetchNovelDataFromTableOfContents(htmlDocument);

            int pageToStopAt = FetchLastTableOfContentsPageNumber(htmlDocument);
            var (chapterUrls, lastTableOfContentsUrl) = await GetPaginatedChapterUrlsAsync(_scraperData.SiteTableOfContents, true, pageToStopAt);

            novelData.ChapterUrls = chapterUrls;
            novelData.LastTableOfContentsPageUrl = lastTableOfContentsUrl;

            return novelData;
        }

        public override NovelData FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        {
            var novelData = new NovelData();
            try
            {
                NovelFullInitializer.FetchNovelContent(novelData, htmlDocument, _scraperData);
                return novelData;
            }
            catch (Exception e)
            {
                Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
            }

            return novelData;
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
