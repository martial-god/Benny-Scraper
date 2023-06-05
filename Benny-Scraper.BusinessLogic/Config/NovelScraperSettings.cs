namespace Benny_Scraper.BusinessLogic.Config
{
    public class NovelScraperSettings
    {
        public string UserAgent { get; set; }
        public int HttpTimeout { get; set; }
        public SeleniumSettings SeleniumSettings { get; set; }
        public List<SiteConfiguration> SiteConfigurations { get; set; }
    }
}
