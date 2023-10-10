using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Benny_Scraper.Models;

namespace Benny_Scraper.BusinessLogic.FileGenerators.Interfaces
{
    public interface IComicBookArchiveGenerator
    {
        public void CreateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration);
    }
}
