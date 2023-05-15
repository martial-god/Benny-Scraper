using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using HtmlAgilityPack;
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
        public override NovelData Scrape()
        {
            NovelData novelData = new NovelData();
            novelData.Genres = new List<string>();
            return novelData;
        }

        public override NovelData GetNovelDataFromTableOfContent(HtmlDocument htmlDocument, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }

        List<string> GetGenres(HtmlDocument htmlDocument, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }
    }
}
