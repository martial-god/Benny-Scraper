using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.Models;
using Microsoft.Extensions.Options;
using NLog;
using System.IO.Compression;
using System.Xml;

namespace Benny_Scraper.BusinessLogic
{
    /// <summary>
    /// Generates an epub file from a novel and its chapters. Using Epub Version 3.2 https://en.wikipedia.org/wiki/EPUB#Open_Container_Format_3.2
    /// </summary>
    public class EpubGenerator : IEpubGenerator
    {
        private readonly EpubTemplates _epubTemplates;
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public EpubGenerator(IOptions<EpubTemplates> epubTemplates)
        {
            _epubTemplates = epubTemplates.Value;
        }

        public void CreateEpub(Novel novel, IEnumerable<Chapter> chapters, string outputFilePath)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);
            try
            {
                string mimetypeFilePath = Path.Combine(tempDirectory, "mimetype");
                File.WriteAllText(mimetypeFilePath, "application/epub+zip");

                string metaInfDirectory = Path.Combine(tempDirectory, "META-INF");
                string oebpsDirectory = Path.Combine(tempDirectory, "OEBPS");
                string textDirectory = Path.Combine(oebpsDirectory, "Text");
                Directory.CreateDirectory(metaInfDirectory);
                Directory.CreateDirectory(oebpsDirectory);
                Directory.CreateDirectory(textDirectory);

                XmlDocument containerXml = new XmlDocument();
                containerXml.LoadXml(_epubTemplates.ContainerXml);
                containerXml.Save(Path.Combine(metaInfDirectory, "container.xml"));

                string manifestItems = string.Empty;
                string spineItems = string.Empty;

                int chapterIndex = 1;
                foreach (var chapter in chapters)
                {
                    string chapterFileName = $"chapter{chapterIndex}.xhtml";
                    string chapterFilePath = Path.Combine(oebpsDirectory, chapterFileName);

                    string chapterContent = string.Format(_epubTemplates.ChapterContent, chapter.Title, chapter.Content, "Missing Item chapterContent");
                    File.WriteAllText(chapterFilePath, chapterContent);

                    manifestItems += $"<item id=\"chapter{chapterIndex}\" href=\"{chapterFileName}\" media-type=\"application/xhtml+xml\"/>";
                    spineItems += $"<itemref idref=\"chapter{chapterIndex}\"/>";

                    chapterIndex++;
                }

                string updatedContentOpf = string.Format(_epubTemplates.ContentOpf, Guid.NewGuid(), novel.Title, novel.Author, manifestItems, spineItems);

                XmlDocument contentOpf = new XmlDocument();
                contentOpf.LoadXml(updatedContentOpf);
                contentOpf.Save(Path.Combine(oebpsDirectory, "content.opf"));

                XmlDocument navXhtml = new XmlDocument();
                navXhtml.LoadXml(_epubTemplates.NavXhtml);
                var navList = navXhtml.SelectSingleNode("//*[local-name()='ol']");

                chapterIndex = 1;
                foreach (var chapter in chapters)
                {
                    string chapterFileName = $"chapter{chapterIndex}.xhtml";

                    XmlElement navItem = navXhtml.CreateElement("li");
                    XmlElement navLink = navXhtml.CreateElement("a");
                    navLink.SetAttribute("href", chapterFileName);
                    navLink.InnerText = chapter.Title;
                    navItem.AppendChild(navLink);
                    navList.AppendChild(navItem);

                    chapterIndex++;
                }

                navXhtml.Save(Path.Combine(oebpsDirectory, "nav.xhtml"));

                // Compress everything into an epub file
                using (FileStream fs = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
                {
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        // Add mimetype file
                        zip.CreateEntryFromFile(mimetypeFilePath, "mimetype", CompressionLevel.NoCompression);

                        // Add other files
                        AddDirectoryToZip(zip, metaInfDirectory, "META-INF");
                        AddDirectoryToZip(zip, oebpsDirectory, "OEBPS");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Fatal($"Error when generating Epub for Novel: {novel.Title} Novel Id: {novel.Id}. {ex}");
            }
            finally
            {
                // Delete temporary directory
                Directory.Delete(tempDirectory, true);
            }
        }


        private void AddDirectoryToZip(ZipArchive zip, string directoryPath, string entryPath)
        {
            foreach (string filePath in Directory.GetFiles(directoryPath))
            {
                string entryName = Path.Combine(entryPath, Path.GetFileName(filePath));
                zip.CreateEntryFromFile(filePath, entryName, CompressionLevel.Fastest);
            }

            foreach (string subDirectoryPath in Directory.GetDirectories(directoryPath))
            {
                string subEntryPath = Path.Combine(entryPath, Path.GetFileName(subDirectoryPath));
                AddDirectoryToZip(zip, subDirectoryPath, subEntryPath);
            }
        }
    }
}
