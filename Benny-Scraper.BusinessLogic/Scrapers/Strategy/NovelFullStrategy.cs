using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using System.Globalization;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    public class NovelFullStrategy : ScraperStrategy
    {
        public override async Task<NovelData> ScrapeAsync()
        {
            Logger.Info("Getting novel data");

            SetBaseUri(SiteTableOfContents);
            HtmlDocument htmlDocument = await LoadHtmlDocumentFromUrlAsync(SiteTableOfContents);

            if (htmlDocument == null)
            {
                Logger.Debug($"Error while trying to load HtmlDocument. \n");
                return null;
            }

            try
            {
                NovelData novelData = await BuildNovelDataAsync(htmlDocument);

                return novelData;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error while getting novel data. {ex}");
                throw;
            }
        }

        private async Task<NovelData> BuildNovelDataAsync(HtmlDocument htmlDocument)
        {
            NovelData novelData = GetNovelDataFromTableOfContent(htmlDocument);

            int pageToStopAt = GetLastTableOfContentsPageNumber(htmlDocument);
            var (chapterUrls, lastTableOfContentsUrl) = await GetPaginatedChapterUrlsAsync(SiteTableOfContents, true, pageToStopAt);

            novelData.ChapterUrls = chapterUrls;
            novelData.LastTableOfContentsPageUrl = lastTableOfContentsUrl;

            return novelData;
        }


        public override NovelData GetNovelDataFromTableOfContent(HtmlDocument htmlDocument)
        {
            NovelData novelData = new NovelData();
            
            try
            {
                HtmlNode authorNode = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.NovelAuthor);
                novelData.Author = authorNode.InnerText.Trim();

                HtmlNodeCollection novelTitleNodes = htmlDocument.DocumentNode.SelectNodes(SiteConfig.Selectors.NovelTitle);
                if (novelTitleNodes.Any())
                {
                    novelData.Title = novelTitleNodes.First().InnerText.Trim();
                }

                HtmlNode novelRatingNode = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.NovelRating);
                novelData.Rating = double.Parse(novelRatingNode.InnerText.Trim());

                HtmlNode totalRatingsNode = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.TotalRatings);
                novelData.TotalRatings = int.Parse(totalRatingsNode.InnerText.Trim());

                HtmlNodeCollection descriptionNode = htmlDocument.DocumentNode.SelectNodes(SiteConfig.Selectors.NovelDescription);
                novelData.Description = descriptionNode.Select(description => description.InnerText.Trim()).ToList();

                HtmlNodeCollection genreNodes = htmlDocument.DocumentNode.SelectNodes(SiteConfig.Selectors.NovelGenres);
                novelData.Genres = genreNodes.Select(genre => genre.InnerText.Trim()).ToList();

                HtmlNodeCollection alternateNameNodes = htmlDocument.DocumentNode.SelectNodes(SiteConfig.Selectors.NovelAlternativeNames);
                novelData.AlternativeNames = alternateNameNodes.Select(alternateName => alternateName.InnerText.Trim()).ToList();

                HtmlNode novelStatusNode = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.NovelStatus);
                novelData.NovelStatus = novelStatusNode.InnerText.Trim();

                HtmlNode thumbnailUrlNode = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.NovelThumbnailUrl);
                novelData.ThumbnailUrl = thumbnailUrlNode.Attributes["src"].Value;

                HtmlNode lastTableOfContentsPageUrl = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.LastTableOfContentsPage);
                novelData.LastTableOfContentsPageUrl = lastTableOfContentsPageUrl.Attributes["href"].Value;

                HtmlNodeCollection chapterLinkNodes = htmlDocument.DocumentNode.SelectNodes(SiteConfig.Selectors.ChapterLinks);
                if (chapterLinkNodes.Any())
                {
                    novelData.FirstChapter = chapterLinkNodes.First().InnerText.Trim();
                }

                novelData.IsNovelCompleted = novelData.NovelStatus.ToLower().Contains(SiteConfig.CompletedStatus);

                HtmlNode latestChapterNode = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.LatestChapterLink);

                if (latestChapterNode == null)
                {
                    Logger.Debug($"Error while trying to get the latest chapter node. \n");
                    return null;
                }

                string latestChapterUrl = latestChapterNode.Attributes["href"].Value;
                string latestChapterName = latestChapterNode.InnerText;
                string fullLatestChapterUrl = new Uri(SiteTableOfContents, latestChapterUrl.TrimStart('/')).ToString();
                string fullThumbnailUrl = new Uri(SiteTableOfContents, novelData.ThumbnailUrl.TrimStart('/')).ToString();
                string fullTableOfContentUrl = new Uri(SiteTableOfContents, novelData.LastTableOfContentsPageUrl.TrimStart('/')).ToString();
                string firstChapterUrl = new Uri(SiteTableOfContents, novelData.FirstChapter.TrimStart('/')).ToString();

                novelData.CurrentChapterUrl = latestChapterUrl;
                novelData.ThumbnailUrl = fullThumbnailUrl;
                novelData.LastTableOfContentsPageUrl = fullLatestChapterUrl;
                novelData.MostRecentChapterTitle = latestChapterName;
                novelData.FirstChapter = firstChapterUrl;

                return novelData;
            }
            catch (Exception e)
            {
                Logger.Error($"Error occurred while getting novel data from table of content. Error: {e}");
            }

            return novelData;
        }

        private int GetLastTableOfContentsPageNumber(HtmlDocument htmlDocument)
        {
            Logger.Info($"Getting last table of contents page number at {SiteConfig.Selectors.LastTableOfContentsPage}");
            try
            {
                HtmlNode lastPageNode = htmlDocument.DocumentNode.SelectSingleNode(SiteConfig.Selectors.LastTableOfContentsPage);
                string lastPage = lastPageNode.Attributes[SiteConfig.Selectors.LastTableOfContentPageNumberAttribute].Value;

                int lastPageNumber = int.Parse(lastPage, NumberStyles.AllowThousands);

                if (SiteConfig.PageOffSet > 0)
                {
                    lastPageNumber += SiteConfig.PageOffSet;
                }

                Logger.Info($"Last table of contents page number is {lastPage}");
                return lastPageNumber;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error when getting last page table of contents page number. {ex}");
                throw;
            }
        }

    }
}
