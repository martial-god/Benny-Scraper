using System.Text.RegularExpressions;

namespace Benny_Scraper.Models
{
    public class ChapterData
    {
        public string Url { get; set; }
        public string Content { get; set; }
        public string Title { get; set; }
        public string Number
        {
            get
            {
                var digitMatch = Regex.Match(Title, @"\d+");
                return (digitMatch.Success ? digitMatch.Groups[0].Value : "0");
            }
        }
        public DateTime DateLastModified { get; set; }
    }
}
