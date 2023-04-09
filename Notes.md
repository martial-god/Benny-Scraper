# Creating EPUB
[Html to make an epub](https://www.thoughtco.com/create-epub-file-from-html-and-xml-3467282)
1. HTML => XML Collection => EPUB

## Changed Dependency Injection to `Autofac`
### Problem
1. Found that the default Microsoft Dependency Injection did not natively support loading services that had similar interfaces. In this case both `HttpNovelScraper` and `SeleniumNovelScraper` implemented `INovelScraper`, whichever service was the last to be registered in the `StartUp.cs` would be always be resolved when trying to get a service for the Service Factory.
### Solution
1. Used `Autofac` Inversion of Control that allowed for keys and custom names to differentiate similarly implemented classes like the ones above.
Ex:
```csharp
builder.RegisterType<SeleniumNovelScraper>().Named<INovelScraper>("Selenium").InstancePerDependency(); // InstancePerDependency() similar to transient
builder.RegisterType<HttpNovelScraper>().Named<INovelScraper>("Http").InstancePerDependency();
```
Resolving service
```csharp
public INovelScraper CreateSeleniumOrHttpScraper(Uri novelTableOfContentsUri)
{
    bool isSeleniumUrl = _novelScraperSettings.SeleniumSites.Any(x => novelTableOfContentsUri.Host.Contains(x));

    if (isSeleniumUrl)
    {
        try
        {
            return _serviceProvider.ResolveNamed<INovelScraper>("Selenium");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error when getting SeleniumNovelScraper. {ex}");
            throw;
        }

    }

    try
    {

        return _serviceProvider.ResolveNamed<INovelScraper>("Http");
    }
    catch (Exception ex)
    {
        Logger.Error($"Error when getting HttpNovelScraper. {ex}");
        throw;
    }
}
```