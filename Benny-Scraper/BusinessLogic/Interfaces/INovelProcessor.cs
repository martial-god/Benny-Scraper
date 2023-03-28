using Benny_Scraper.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benny_Scraper.BusinessLogic.Interfaces
{
    public interface INovelProcessor
    {
        public Task ProcessNovelAsync(Uri novelTableOfContentsUri);
        public Task GenerateEpubAsync(Novel novel, List<Models.Chapter> chapters, string outputPath);
    }
}
