
namespace Benny_Scraper.Models
{
    public class NovelData
    {
        public List<string> LatestChapterUrls { get; set; }
        public string Status { get; set; }
        public string LastTableOfContentsUrl { get; set; }
        public bool LastChapter { get; set; }
    }
}
