
namespace Benny_Scraper.Models
{
    /// <summary>
    /// Information that can be found on the table of contents page, such as status
    /// </summary>
    public class NovelData
    {
        public List<string> LatestChapterUrls { get; set; }
        public string Status { get; set; }
        public string LastTableOfContentsUrl { get; set; }
        public bool LastChapter { get; set; }
        public string LatestChapterUrl
        {
            get 
            { 
                return LatestChapterUrls.Last();
            }
        }
    }
}
