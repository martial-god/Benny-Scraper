using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using Microsoft.Extensions.Options;
using System.Xml;

namespace Benny_Scraper.EpubMaker
{
    /// <summary>
    /// Generates an epub file from a novel and its chapters. Using Epub Version 3.2 https://en.wikipedia.org/wiki/EPUB#Open_Container_Format_3.2
    /// </summary>
    public class EpubGenerator : IEpubGenerator
    {
        private readonly EpubTemplates _epubTemplates;

        public EpubGenerator(IOptions<EpubTemplates> epubTemplates)
        {
            _epubTemplates = epubTemplates.Value;
        }

        public void CreateEpub(Novel novel, IEnumerable<Chapter> chapters, string outputFilePath)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);

            string mimetypeFilePath = Path.Combine(tempDirectory, "mimetype");
            File.WriteAllText(mimetypeFilePath, "application/epub+zip");

            string metaInfDirectory = Path.Combine(tempDirectory, "META-INF");
            string oebpsDirectory = Path.Combine(tempDirectory, "OEBPS");
            Directory.CreateDirectory(metaInfDirectory);
            Directory.CreateDirectory(oebpsDirectory);

            XmlDocument containerXml = new XmlDocument();
            containerXml.LoadXml(_epubTemplates.ContainerXml);
            containerXml.Save(Path.Combine(metaInfDirectory, "container.xml"));

            XmlDocument packageOpf = new XmlDocument();
            packageOpf.LoadXml(string.Format(_epubTemplates.PackageOpf, Guid.NewGuid(), novel.Title, novel.Author));

            var mamifestNode = packageOpf.SelectSingleNode(_epubTemplates.XmlSelectors.PackageOpfManifest);
            var spineNode = packageOpf.SelectSingleNode(_epubTemplates.XmlSelectors.PackageOpfSpine);
            int chapterIndex = 1;

            foreach (var chapter in chapters)
            {
                string chapterFileName = $"chapter{chapterIndex}.xhtml";
                string chapterFilePath = Path.Combine(oebpsDirectory, chapterFileName);
                File.WriteAllText(chapterFilePath, chapter.Content);

                XmlElement itemElement = packageOpf.CreateElement("item");
                itemElement.SetAttribute("id", $"chapter{chapterIndex}");
                itemElement.SetAttribute("href", chapterFileName);
                itemElement.SetAttribute("media-type", "application/xhtml+xml");
                mamifestNode.AppendChild(itemElement);

                XmlElement itemRefElement = packageOpf.CreateElement("itemref");
                itemRefElement.SetAttribute("idref", $"chapter{chapterIndex}");
                spineNode.AppendChild(itemRefElement);

                chapterIndex++;
            }
        }
    }
}
