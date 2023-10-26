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
        public string CreateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration);
        public string UpdateComicBookArchive(Novel novel, IEnumerable<ChapterDataBuffer> chapterDataBuffers, string outputDirectory, Configuration configuration);
    }
}
