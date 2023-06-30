﻿using Benny_Scraper.BusinessLogic.Scrapers.Strategy.Impl;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Text;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    /// <summary>
    /// Strategy for https://mangareader.to/
    /// </summary>
    public class MangaReaderInitializer : NovelDataInitializer
    {
        public static async Task FetchNovelContentAsync(NovelData novelData, HtmlDocument htmlDocument, ScraperData scraperData, ScraperStrategy scraperStrategy)
        {
            int.TryParse(scraperData.SiteTableOfContents?.Segments.Last().Split("-").Last(), out int novelId);
            StringBuilder queryBuilder = new StringBuilder(scraperData?.BaseUri?.ToString());
            queryBuilder.Append("ajax/manga/list-chapter-volume?id=");
            queryBuilder.Append(novelId);
            Uri uriQueryForChapterUrls = new Uri(queryBuilder.ToString());

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
                FetchContentByAttribute(attribute, novelData, htmlDocument, scraperData);
            }

            if (novelData.ChapterUrls.Any())
            {
                // chapters are in reverse order
                novelData.ChapterUrls.Reverse();
                novelData.ChapterUrls = novelData.ChapterUrls.Select(partialUrl => new Uri(scraperData.BaseUri, partialUrl).ToString()).ToList();
            }
            if (!string.IsNullOrEmpty(novelData.MostRecentChapterTitle))
            {
                novelData.MostRecentChapterTitle = novelData.MostRecentChapterTitle.Split("\n").First(); // remove new line and everything after
            }
        }

    }

    public class MangaReaderStrategy : ScraperStrategy
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
            var novelData = await FetchNovelDataFromTableOfContentsAsync(htmlDocument);
            return novelData;
        }

        public override async Task<NovelData> FetchNovelDataFromTableOfContentsAsync(HtmlDocument htmlDocument)
        {
            var novelData = new NovelData();
            try
            {
                await Task.WhenAll(MangaReaderInitializer.FetchNovelContentAsync(novelData, htmlDocument, _scraperData, this));
                return novelData;
            }
            catch (Exception e)
            {
                Logger.Error($"Error occurred while getting novel data from table of contents. Error: {e}");
            }

            return novelData;
        }

        public override NovelData FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
        {
            throw new NotImplementedException();
        }
    }
}
