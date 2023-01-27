using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Benny_Scraper.Models
{
    public class ChapterData
    {
        public string Url { get; set; }
        public string Content { get; set; }
        public string Title { get; set; }
        public string Number { get 
            {
                var digitMatch = Regex.Match(Title, @"\d+");
                return (digitMatch.Success ? digitMatch.Groups[0].Value : "0");
            } 
        }
    }
}
