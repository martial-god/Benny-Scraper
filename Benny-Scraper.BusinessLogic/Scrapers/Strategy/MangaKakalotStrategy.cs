using System.Text;
using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    /// <summary>
    /// Strategy for https://mangakakalot.to/
    /// </summary>
    public class MangaKakalotInitializer : NovelDataInitializer
    {
        public static async Task FetchNovelContentAsync(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData, ScraperStrategy scraperStrategy)
        {
            int.TryParse(scraperData.SiteTableOfContents?.Segments.Last().Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last(), out int novelId);
            StringBuilder queryBuilder = new StringBuilder(scraperData?.BaseUri?.ToString());
            queryBuilder.Append("ajax/manga/list-chapter-volume?id=");
            queryBuilder.Append(novelId);
            Uri uriQueryForChapterUrls = new Uri(queryBuilder.ToString());
            var htmlDocumentForChapterUrls = await scraperStrategy.LoadHtmlPublicAsync(uriQueryForChapterUrls);

            var attributesToFetch = new List<Attr>()
            {
                Attr.Title,
                Attr.Author,
                Attr.Status,
                Attr.Genres,
                Attr.AlternativeNames,
                Attr.Description,
                Attr.ThumbnailUrl,
                Attr.ChapterUrls,
                Attr.LatestChapter
            };

            foreach (var attribute in attributesToFetch)
            {
                if (attribute == Attr.ChapterUrls)
                {
                    FetchContentByAttribute(attribute, novelDataBuffer, htmlDocumentForChapterUrls, scraperData);
                    if (novelDataBuffer.ChapterUrls.Any())
                    {
                        // chapters are in reverse order
                        novelDataBuffer.ChapterUrls.Reverse();
                        novelDataBuffer.ChapterUrls = novelDataBuffer.ChapterUrls.Select(partialUrl => new Uri(scraperData.BaseUri, partialUrl).ToString()).ToList();
                        novelDataBuffer.FirstChapter = novelDataBuffer.ChapterUrls.First();
                    }
                }
                else if (attribute == Attr.LatestChapter)
                {
                    FetchContentByAttribute(attribute, novelDataBuffer, htmlDocumentForChapterUrls, scraperData);
                    novelDataBuffer.CurrentChapterUrl = new Uri(scraperData.BaseUri, novelDataBuffer.CurrentChapterUrl).ToString();
                }
                else
                {
                    FetchContentByAttribute(attribute, novelDataBuffer, htmlDocument, scraperData);
                }
            }
        }

    }

    public class MangaKakalotStrategy : ScraperStrategy
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

        public override async Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = new NovelDataBuffer();
            try
            {
                await Task.WhenAll(MangaKakalotInitializer.FetchNovelContentAsync(novelDataBuffer, htmlDocument, _scraperData, this));
                return novelDataBuffer;
            }
            catch (Exception e)
            {
                Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
            }

            return novelDataBuffer;
        }

        public override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        {
            throw new NotImplementedException();
        }

        #region Private Methods
        private async Task<NovelDataBuffer> BuildNovelDataAsync(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument);
            return novelDataBuffer;
        }
        #endregion

    }
}