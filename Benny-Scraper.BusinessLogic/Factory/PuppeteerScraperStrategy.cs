using Benny_Scraper.BusinessLogic.Scrapers.Strategy;
using Benny_Scraper.Models;
using HtmlAgilityPack;
using PuppeteerSharp;

namespace Benny_Scraper.BusinessLogic.Factory;

public class PuppeteerScraperStrategy(IPuppeteerDriverService puppeteerDriverService) : ScraperStrategy
{
    public override async Task<NovelDataBuffer> ScrapeAsync()
    {
        SetBaseUri(_scraperData.SiteTableOfContents);
        
        var page = await puppeteerDriverService.CreatePageAsync(_scraperData.SiteTableOfContents, false);
        
        // Wait for Cloudflare challenge to resolve
        await WaitForCloudflareAsync(page);
        var uri = new Uri(page.Url);
        
        var content = await page.GetContentAsync();
        var document = new HtmlDocument();
        document.Load(content);

        return new NovelDataBuffer();
    }

    protected override NovelDataBuffer FetchNovelDataFromTableOfContents(HtmlDocument htmlDocument)
    {
        throw new NotImplementedException();
    }

    private async Task WaitForCloudflareAsync(IPage page)
    {
        try
        {
            await page.WaitForSelectorAsync("#cf-challenge-running", new WaitForSelectorOptions 
            {
                Timeout = 30000,
                Hidden = true
            });
        }
        catch (TimeoutException)
        {
            Logger.Warn("Cloudflare challenge timeout");
        }
    }
}