using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.FileGenerators.Interfaces;
using Benny_Scraper.BusinessLogic.Helper;
using Benny_Scraper.Models;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Options;
using NLog;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Benny_Scraper.BusinessLogic.FileGenerators
{
    /// <summary>
    /// Generates an epub file from a novel and its chapters. Using Epub Version 3.2 https://en.wikipedia.org/wiki/EPUB#Open_Container_Format_3.2
    /// Validation for files can be done at https://validator.w3.org/check
    /// </summary>
    public class EpubGenerator : IEpubGenerator
    {
        private readonly EpubTemplates _epubTemplates;
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        public bool UseCalibre { get; set; } = false;

        public EpubGenerator(IOptions<EpubTemplates> epubTemplates)
        {
            _epubTemplates = epubTemplates.Value;
        }

        public void CreateEpub(Novel? novel, IEnumerable<Chapter> chapters, string outputFilePath, byte[]? coverImage)
        {
            Logger.Info("Creating epub file. Novel: {0}, Chapters: {1}, OutputFilePath: {2}", novel.Title, chapters.Count(), outputFilePath);
            var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Logger.Info("Temp directory: {0}", tempDirectory);
            Directory.CreateDirectory(tempDirectory);
            Logger.Info("Temp directory created");

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                    Logger.Info($"Application shutdown. Temp directory {tempDirectory} deleted");
                }
            };

            try
            {
                var mimetypeFilePath = Path.Combine(tempDirectory, "mimetype");
                File.WriteAllText(mimetypeFilePath, "application/epub+zip");

                // used templates from https://github.com/IDPF/epub3-samples
                var metaInfDirectory = Path.Combine(tempDirectory, "META-INF");
                var oebpsDirectory = Path.Combine(tempDirectory, "OEBPS");
                var textDirectory = Path.Combine(oebpsDirectory, "Text");
                var cssDirectory = Path.Combine(oebpsDirectory, "css");
                var imagesDirectory = Path.Combine(oebpsDirectory, "Images");
                Logger.Info("Creating directories: {0}, {1}, {2}, {3}, {4}", metaInfDirectory, oebpsDirectory, textDirectory, cssDirectory, imagesDirectory);
                Directory.CreateDirectory(metaInfDirectory);
                Directory.CreateDirectory(oebpsDirectory);
                Directory.CreateDirectory(textDirectory);
                Directory.CreateDirectory(cssDirectory);
                Directory.CreateDirectory(imagesDirectory);
                Logger.Info("Directories created");

                var containerXml = new XmlDocument();
                containerXml.LoadXml(_epubTemplates.ContainerXml);
                Logger.Info("Saving container.xml");
                containerXml.Save(Path.Combine(metaInfDirectory, "container.xml"));
                Logger.Info("container.xml saved");

                var manifestItems = string.Empty;
                var spineItems = string.Empty;
                var subjectItems = string.Empty;

                foreach (var tag in novel.Genre.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    subjectItems += $"<dc:subject>{tag}</dc:subject>";
                }

                // save cover image
                var coverImageFileName = "cover.png";
                var coverImageFilePath = Path.Combine(imagesDirectory, coverImageFileName);

                if (coverImage != null)
                {
                    Logger.Info("Saving cover image to {0}", coverImageFilePath);
                    File.WriteAllBytes(coverImageFilePath, coverImage);
                    Logger.Info("Cover image saved");
                }

                // create intro page
                int chapterIndex = 0;
                var introTitle = "Information";
                var introImage = "../Images/" + coverImageFileName;
                var introDescription = novel.Description;

                var introFileName = $"000{chapterIndex}_intro.xhtml";
                var introFilePath = Path.Combine(textDirectory, introFileName);

                var introContent = string.Format(_epubTemplates.IntroContent, introTitle, introImage, introDescription, novel.Url);
                File.WriteAllText(introFilePath, introContent);

                manifestItems += $"<item id=\"intro\" href=\"Text/{introFileName}\" media-type=\"application/xhtml+xml\"/>";
                spineItems += $"<itemref idref=\"intro\"/>";

                chapterIndex++;
                Logger.Info("Creating chapters and adding to manifest and spine");
                foreach (var chapter in chapters)
                {
                    var safeChapterTitleName = Regex.Replace(chapter.Title, "[^a-zA-Z0-9_.]+", "_", RegexOptions.Compiled);
                    var chapterFileName = $"000{chapterIndex}_{safeChapterTitleName}.xhtml";
                    var chapterFilePath = Path.Combine(textDirectory, chapterFileName);

                    var chapterContent = BuildXhtmlContent(chapter.Title, chapter.Content, chapter.Url);
                    File.WriteAllText(chapterFilePath, chapterContent);

                    manifestItems += $"<item id=\"chapter{chapterIndex}\" href=\"Text/{chapterFileName}\" media-type=\"application/xhtml+xml\"/>";
                    spineItems += $"<itemref idref=\"chapter{chapterIndex}\"/>";

                    chapterIndex++;
                }
                Logger.Info("Chapters created and added to manifest and spine");

                // Add cover to manifest only if image was provided
                var coverMeta = string.Empty;
                var coverManifest = string.Empty;
                if (coverImage != null)
                {
                    coverMeta = "<meta name=\"cover\" content=\"cover\"/>";
                    coverManifest = "<item id=\"cover\" href=\"Images/cover.png\" media-type=\"image/png\"/>";
                }

                manifestItems += "<item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>";
                manifestItems += "<item id=\"css_chapter\" href=\"css/chapter.css\" media-type=\"text/css\"/>";
                manifestItems += "<item id=\"css_nav\" href=\"css/nav.css\" media-type=\"text/css\"/>";
                manifestItems += "<item id=\"css_toc\" href=\"css/toc.css\" media-type=\"text/css\"/>";

                string updatedContentOpf = string.Format(_epubTemplates.ContentOpf, Regex.Replace(novel.Title, @"[^a-zA-Z0-9\s_.]+", "", RegexOptions.Compiled), novel.Author, novel.Author, subjectItems, manifestItems, spineItems, coverMeta, coverManifest);

                XmlDocument contentOpf = new XmlDocument();
                contentOpf.LoadXml(updatedContentOpf);
                Logger.Info("Saving content.opf");
                contentOpf.Save(Path.Combine(oebpsDirectory, "content.opf"));
                Logger.Info("content.opf saved");

                // Create nav.xhtml
                var navXhtml = new XmlDocument();
                navXhtml.LoadXml(_epubTemplates.NavXhtml);
                var navList = navXhtml.SelectSingleNode("//*[local-name()='ol']");

                // Add the intro page to the navigation
                var navItemIntro = navXhtml.CreateElement("li");
                var navLinkIntro = navXhtml.CreateElement("a");
                navLinkIntro.SetAttribute("href", $"Text/{introFileName}");
                navLinkIntro.InnerText = introTitle;
                navItemIntro.AppendChild(navLinkIntro);
                navList?.AppendChild(navItemIntro);

                chapterIndex = 1;
                foreach (var chapter in chapters)
                {
                    var safeChapterTitleName = Regex.Replace(chapter.Title, "[^a-zA-Z0-9_.]+", "_", RegexOptions.Compiled);
                    var chapterFileName = $"000{chapterIndex}_{safeChapterTitleName}.xhtml";

                    var navItem = navXhtml.CreateElement("li");
                    var navLink = navXhtml.CreateElement("a");
                    navLink.SetAttribute("href", $"Text/{chapterFileName}");
                    navLink.InnerText = chapter.Title;
                    navItem.AppendChild(navLink);
                    navList?.AppendChild(navItem);

                    chapterIndex++;
                }

                navXhtml.Save(Path.Combine(oebpsDirectory, "nav.xhtml"));
                Logger.Info("nav.xhtml saved");

                // Add CSS files
                File.WriteAllText(Path.Combine(cssDirectory, "chapter.css"), _epubTemplates.ChapterCss);
                File.WriteAllText(Path.Combine(cssDirectory, "nav.css"), _epubTemplates.NavCss);
                File.WriteAllText(Path.Combine(cssDirectory, "toc.css"), _epubTemplates.TocCss);

                Logger.Info("Compressing everything into an epub file");

                // Compress everything into an epub file
                using (var fs = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
                {
                    using (ZipOutputStream zipStream = new ZipOutputStream(fs))
                    {
                        // Add mimetype file
                        var mimetypeEntry = new ZipEntry("mimetype");
                        mimetypeEntry.CompressionMethod = CompressionMethod.Stored; // No compression for mimetype file
                        zipStream.PutNextEntry(mimetypeEntry);
                        var mimetypeBuffer = File.ReadAllBytes(mimetypeFilePath);
                        zipStream.Write(mimetypeBuffer, 0, mimetypeBuffer.Length);
                        zipStream.CloseEntry();

                        // Add META-INF and OEBPS files
                        AddDirectoryToZip(zipStream, metaInfDirectory, "META-INF", tempDirectory);
                        AddDirectoryToZip(zipStream, oebpsDirectory, "OEBPS", tempDirectory);
                    }
                }
                Logger.Info("Epub file created at: {0}", outputFilePath);
            }
            catch (Exception ex)
            {
                Logger.Fatal($"Error when generating Epub for Novel: {novel.Title} Novel Id: {novel.Id}. {ex}");
            }
            finally
            {
                Logger.Info($"Deleting temporary directory: {tempDirectory}");
                // Delete temporary directory
                Directory.Delete(tempDirectory, true);
                Logger.Info($"Deleted temporary directory: {tempDirectory}\n");

                // Display completion summary in a formatted box
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                    EPUB GENERATION COMPLETE!                             ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  Novel:          {novel.Title}");
                Console.WriteLine($"  Novel ID:       {novel.Id}");
                if (novel.ChapterRanges.Any())
                {
                    var ranges = novel.ChapterRanges.OrderBy(r => r.Begin).Select(r => $"{r.Begin}-{r.End}").ToList();
                    var totalInRanges = novel.ChapterRanges.Sum(r => r.End - r.Begin + 1);
                    Console.WriteLine($"  Chapter Ranges: {string.Join(", ", ranges)} ({totalInRanges} chapters)");
                }
                Console.WriteLine($"  Total Chapters: {chapters.Count()}");
                Console.WriteLine($"  Saved to:       {outputFilePath}");
                Console.ResetColor();

                Console.WriteLine();
                Console.WriteLine(new string('─', 78));
                Console.WriteLine();

                // Try to add to Calibre
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Adding to Calibre database...");
                Console.ResetColor();
                try
                {
                    var result = CommandExecutor.ExecuteCommand($"calibredb add \"{outputFilePath}\" --automerge \"overwrite\" --series \"{novel.Title}\"");
                    Logger.Debug($"Calibre command executed with code: {result}");

                    if (result == "0")
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✓ Successfully added to Calibre");
                        Console.ResetColor();
                    }
                }
                catch
                {
                }

                Console.WriteLine();
                Logger.Debug($"EPUB generation complete - Novel: {novel.Title}, Chapters: {chapters.Count()}, Location: {outputFilePath}");
            }
        }

        static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            // Do what you want with the output (write to console/log/StringBuilder)
            Console.WriteLine(outLine.Data);
        }

        private void AddDirectoryToZip(ZipOutputStream zipStream, string sourceDirectory, string targetDirectory, string baseDirectory)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);

            foreach (var fileInfo in diSource.GetFiles())
            {
                var entryName = Path.Combine(targetDirectory, fileInfo.Name).Replace("\\", "/");
                var entry = new ZipEntry(entryName);
                entry.CompressionMethod = CompressionMethod.Deflated;
                zipStream.PutNextEntry(entry);
                var buffer = File.ReadAllBytes(fileInfo.FullName);
                zipStream.Write(buffer, 0, buffer.Length);
                zipStream.CloseEntry();
            }

            foreach (var sourceSubDir in diSource.GetDirectories())
            {
                var nextTargetSubDir = Path.Combine(targetDirectory, sourceSubDir.Name);
                AddDirectoryToZip(zipStream, sourceSubDir.FullName, nextTargetSubDir, baseDirectory);
            }
        }

        private string BuildXhtmlContent(string title, string content, string url)
        {
            var xhtmlContentBuilder = new StringBuilder();

            xhtmlContentBuilder.AppendLine("<div>");
            xhtmlContentBuilder.AppendFormat("<h2>{0}</h2>", title);

            var paragraphs = content?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (paragraphs == null || paragraphs.Length == 0)
            {
                xhtmlContentBuilder.AppendFormat("<p>{0} {1}</p>", "Error getting chapter content from ", url);
                xhtmlContentBuilder.AppendLine("</div>");

                return string.Format(_epubTemplates.ChapterContent, title, xhtmlContentBuilder.ToString());
            }

            foreach (string paragraph in paragraphs)
            {
                xhtmlContentBuilder.AppendFormat("<p>{0}</p>", paragraph.Trim());
            }

            xhtmlContentBuilder.AppendLine("</div>");

            return string.Format(_epubTemplates.ChapterContent, title, xhtmlContentBuilder.ToString());
        }

        public void ValidateEpub(string epubFilePath)
        {
            //check if the file exists
            if (!File.Exists(epubFilePath))
            {
                throw new FileNotFoundException($"Epub file not found: {epubFilePath}");
            }

            //check if the file is a valid epub
            using (FileStream fs = new FileStream(epubFilePath, FileMode.Open, FileAccess.Read))
            {
                using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    //check if the mimetype file exists
                    var mimetypeEntry = zip.Entries.FirstOrDefault(x => x.FullName == "mimetype");
                    if (mimetypeEntry == null)
                    {
                        throw new Exception("Epub file is not valid. Mimetype file is missing.");
                    }

                    //check if the mimetype file is the first file in the zip
                    if (zip.Entries.IndexOf(mimetypeEntry) != 0)
                    {
                        throw new Exception("Epub file is not valid. Mimetype file is not the first file in the zip.");
                    }

                    //check if the mimetype file is uncompressed, do not use mimetypeEntry.CompressionLevel as it is not supported in .net core
                    if (mimetypeEntry.CompressedLength != mimetypeEntry.Length)
                    {
                        throw new Exception("Epub file is not valid. Mimetype file is compressed.");
                    }

                    //check if the mimetype file is not empty
                    if (mimetypeEntry.Length == 0)
                    {
                        throw new Exception("Epub file is not valid. Mimetype file is empty.");
                    }

                    //check if the mimetype file is not empty
                    if (mimetypeEntry.Length > 100)
                    {
                        throw new Exception("Epub file is not valid. Mimetype file is too big.");
                    }

                    //check if the mimetype file is not empty
                    if (mimetypeEntry.Length < 20)
                    {
                        throw new Exception("Epub file is not valid. Mimetype file is too small.");
                    }

                    //check if the mimetype file is not empty
                    if (mimetypeEntry.Length != 20)
                    {
                        throw new Exception("Epub file is not valid. Mimetype file is not 20 bytes.");
                    }

                    //check if the mimetype file is not empty
                    if (mimetypeEntry.Length != 20)
                    {
                        throw new Exception("Epub file is not valid. Mimetype file is not 20 bytes.");
                    }
                }
            }
        }
    }
}

