using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Text;
using Benny_Scraper.BusinessLogic.Factory;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    /// <summary>
    /// Strategy for https://mangakatana.com/
    /// </summary>
    public class MangaKatanaInitializer : NovelDataInitializer
    {
        public static void FetchNovelContent(NovelDataBuffer novelDataBuffer, HtmlDocument htmlDocument, ScraperData scraperData, ScraperStrategy scraperStrategy)
        {
            int.TryParse(scraperData.SiteTableOfContents?.Segments.Last().Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last(), out int novelId);
            StringBuilder queryBuilder = new StringBuilder(scraperData?.BaseUri?.ToString());
            queryBuilder.Append("ajax/manga/list-chapter-volume?id=");
            queryBuilder.Append(novelId);

            var attributesToFetch = new List<Attr>()
            {
                Attr.Title,
                Attr.Author,
                Attr.NovelStatus,
                Attr.Genres,
                Attr.AlternativeNames,
                Attr.Description,
                Attr.ThumbnailUrl,
                Attr.ChapterUrls,
                Attr.CurrentChapter
            };

            foreach (var attribute in attributesToFetch)
            {
                FetchContentByAttribute(attribute, novelDataBuffer, htmlDocument, scraperData);
            }

            if (novelDataBuffer.ChapterUrls.Any())
            {
                // chapters are in reverse order
                novelDataBuffer.ChapterUrls.Reverse();
                novelDataBuffer.ChapterUrls = novelDataBuffer.ChapterUrls.Select(partialUrl => new Uri(scraperData.BaseUri, partialUrl).ToString()).ToList();
                novelDataBuffer.FirstChapter = novelDataBuffer.ChapterUrls.First();
            }
            if (!string.IsNullOrEmpty(novelDataBuffer.MostRecentChapterTitle))
            {
                novelDataBuffer.MostRecentChapterTitle = novelDataBuffer.MostRecentChapterTitle.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).First(); // remove new line and everything after
            }
        }

    }

    public class MangaKatanaStrategy(IDriverFactory driverFactory) : ScraperStrategy(driverFactory)
    {
        protected override bool RequiresBrowser => true;
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
            var novelDataBuffer = await FetchNovelDataFromTableOfContentsAsync(htmlDocument);
            return novelDataBuffer;
        }

        protected override Task<NovelDataBuffer> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
        {
            var novelDataBuffer = new NovelDataBuffer();
            try
            {
                MangaKatanaInitializer.FetchNovelContent(novelDataBuffer, htmlDocument, _scraperData, this);
                return Task.FromResult(novelDataBuffer);
            }
            catch (Exception e)
            {
                Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
            }

            return Task.FromResult(novelDataBuffer);
        }

        protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        {
            throw new NotImplementedException();
        }
    }
}
