
namespace Benny_Scraper.Models
{
    /// <summary>
    /// Information that can be found on the table of contents page, such as status
    /// </summary>
    public class NovelData
    {
        public NovelData() 
        {
            RecentChapterUrls = new List<string>();
            Description = new List<string>();
            Genres = new List<string>();
            AlternativeNames = new List<string>();
        }

        public string Title { get; set; }
        public List<string> RecentChapterUrls { get; set; }
        public string NovelStatus { get; set; }
        public string LastTableOfContentsPageUrl { get; set; }
        public bool IsNovelCompleted { get; set; }
        public string ThumbnailUrl { get; set; }
        public double Rating { get; set; }
        public int TotalRatings { get; set; }
        public List<string> Description { get; set; }
        public string Author { get; set; }
        public List<string> Genres { get; set; }
        public List<string> AlternativeNames { get; set; }
        public string MostRecentChapterTitle { get; set; }
        public string CurrentChapterUrl { get; set; }
        public string FirstChapter { get; set; }
    }
}
