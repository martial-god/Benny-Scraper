using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Benny_Scraper.BusinessLogic.Scrapers.Strategy
{
    public class WebNovelPubStrategy : ScraperStrategy
    {
        // create _alternateTableOfContentsUri that will be passed a value in Scraper method, give it a default value
        private Uri? _alternateTableOfContentsUri;

        public override async Task<NovelData> ScrapeAsync()
        {
            Logger.Info("Starting scraper for Web");

            _alternateTableOfContentsUri = GetAlternateTableOfContentsPageUri(this.SiteTableOfContents);

            HtmlDocument htmlDocument = await LoadHtmlDocumentFromUrlAsync(_alternateTableOfContentsUri);

            if (htmlDocument == null)
            {
                Logger.Debug($"Error while trying to load HtmlDocument. \n");
                return null;
            }

            NovelData novelData = GetNovelDataFromTableOfContent(htmlDocument);

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
    }
}
