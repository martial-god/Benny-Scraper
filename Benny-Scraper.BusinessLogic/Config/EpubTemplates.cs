namespace BennyScraper.BusinessLogic.Config;

public class EpubTemplates
{
    public string ContentOpf { get; set; } = string.Empty;

    public string ContainerXml { get; set; } = string.Empty;

    public string TocNcx { get; set; } = string.Empty;

    public string TocXhtml { get; set; } = string.Empty;

    public string NavXhtml { get; set; } = string.Empty;

    public string ChapterContent { get; set; } = string.Empty;

    public string ChapterCss { get; set; } = string.Empty;

    public string NavCss { get; set; } = string.Empty;

    public string TocCss { get; set; } = string.Empty;

    public XmlSelectors XmlSelectors { get; set; } = new XmlSelectors();

    public string IntroContent { get; set; } = string.Empty;
}