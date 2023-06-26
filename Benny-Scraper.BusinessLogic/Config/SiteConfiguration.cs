using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benny_Scraper.BusinessLogic.Config
{
    public class SiteConfiguration
    {
        public string Name { get; set; }
        public string UrlPattern { get; set; }
        public bool HasPagination { get; set; }
        public string? PaginationType { get; set; }
        public string? PaginationQueryPartial { get; set; }
        public Selectors Selectors { get; set; }
        public int ChaptersPerPage { get; set; }
        public int PageOffSet { get; set; }
        public string CompletedStatus { get; set; }
        public bool HasNovelInfoOnDifferentPage { get; set; }
        public bool IsSeleniumSite { get; set; }
        public bool HasImagesForChapterContent { get; set; }
    }
}
