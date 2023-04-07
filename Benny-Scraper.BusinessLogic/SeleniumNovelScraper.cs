using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic
{
    /// <summary>
    /// A selenium implementation of the INovelScraper interface. Use this for sites that require login-in to get the chapter contents like wuxiaworld.com
    /// </summary>
    public class SeleniumNovelScraper : INovelScraper
    {
        
        public Task<List<string>> BuildChaptersUrlsFromTableOfContentUsingPaginationAsync(int pageToStartAt, Uri siteUrl, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetLatestChapterAsync(Uri uri, SiteConfiguration siteConfig)
        {
            throw new NotImplementedException();
        }

        public Task GoToTableOfContentsPageAsync(Uri novelTableOfContentsUri)
        {
            throw new NotImplementedException();
        }
    }
}
