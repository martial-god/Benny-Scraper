- [Creating EPUB](#creating-epub)
  - [Changed Dependency Injection to `Autofac`](#changed-dependency-injection-to-autofac)
    - [Problem](#problem)
    - [Solution](#solution)
    - [Resolving service](#resolving-service)
  - [April Update](#april-update)
    - [appsettings.json](#appsettingsjson)
    - [EpubGenerator.cs](#epubgeneratorcs)
    - [HttpNovelScraper.cs](#httpnovelscrapercs)
  - [May Update](#may-update)
    - [Changing HttpNovelScraper.cs and SeleniumNovelScraper.cs to be base classes for the specific site scrapers / Goals for this month](#changing-httpnovelscrapercs-and-seleniumnovelscrapercs-to-be-base-classes-for-the-specific-site-scrapers--goals-for-this-month)

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
builder.RegisterType<NovelScraperFactory>().As<INovelScraperFactory>().InstancePerDependency();
            builder.RegisterType<SeleniumNovelScraper>().Named<INovelScraper>("Selenium").InstancePerDependency(); // InstancePerDependency() similar to transient
            builder.RegisterType<HttpNovelScraper>().Named<INovelScraper>("Http").InstancePerDependency();
```
### Resolving service
```csharp
/// <summary>
/// Creates an instance of either a SeleniumNovelScraper or HttpNovelScraper depending on the url.
/// </summary>
/// <param name="novelTableOfContentsUri"></param>
/// <returns>Scraper instance that implemnts INovelService </returns>
public INovelScraper CreateSeleniumOrHttpScraper(Uri novelTableOfContentsUri)
{
    bool isSeleniumUrl = _novelScraperSettings.SeleniumSites.Any(x => novelTableOfContentsUri.Host.Contains(x));

    if (isSeleniumUrl)
    {
        try
        {
            return _novelScraperResolver("Selenium");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error when getting SeleniumNovelScraper. {ex}");
            throw;
        }
    }

    try
    {
        return _novelScraperResolver("Http");
    }
    catch (Exception ex)
    {
        Logger.Error($"Error when getting HttpNovelScraper. {ex}");
        throw;
    }
}
```
---
## April Update
04/01/2023 - 04/16/2023
* General change from `DateTime.UTCNow` to `DataTime.Now`, better for the future UI to display date add/last modified
* Changes to `NovelData` and `ChapterData` models
* Validated chapter xhtml using https://validator.w3.org/
### appsettings.json
* Removed most of the hardcoded strings to this file, the choice came about whether to use this or to create a `Configuration` table that would be used in the database ( benefit of easier to update without a need to build)
* Setup in a way that each site config can be added easily, classes will need to be craeated passed into the dependency injection.

```csharp
builder.Register(c =>
{
    var config = c.Resolve<IConfiguration>();
    var settings = new NovelScraperSettings();
    config.GetSection("NovelScraperSettings").Bind(settings);
    return settings;
}).SingleInstance();
//needed to register NovelScraperSettings implicitly, Autofac does not resolve 'IOptions<T>' by defualt. Optoins.Create avoids ArgumentException
builder.Register(c => Options.Create(c.Resolve<NovelScraperSettings>())).As<IOptions<NovelScraperSettings>>().SingleInstance();

// register EpuTemplates.cs
builder.Register(c =>
{
    var config = c.Resolve<IConfiguration>();
    var settings = new EpubTemplates();
    config.GetSection("EpubTemplates").Bind(settings);
    return settings;
}).SingleInstance();
builder.Register(c => Options.Create(c.Resolve<EpubTemplates>())).As<IOptions<EpubTemplates>>().SingleInstance();
```
### EpubGenerator.cs
FINALLY ABLE TO GENERATE AN EPUB FILE. Still a work in progress, as @Voice will not open it.
* Ran into an `UnauthorizedAccessException` it had to do with not have a file name passed into the `CreateEpub()` method, instead it was the path to a directory.
* Changed chapter content recieved from the scraper to only contain `<p>` tags and `title` tags. This was done to make the xhtml valid. Added method to build and xhtml document from the chapter content.
### HttpNovelScraper.cs
* Fixed bug that would not get chapters from pages newer than the last saved page when updating a novel.
---
## May Update
05/01/2033
### Changing HttpNovelScraper.cs and SeleniumNovelScraper.cs to be base classes for the specific site scrapers / Goals for this month
1. I tried to avoid this, but it seems like the best way to go about it. The base classes will contain the common methods and properties that are used by the specific site scrapers. The specific site scrapers will contain the methods and properties that are unique to the site.
The only question now, is whether to use `abstract` or `virtual` methods.
2. Last week, I was finally able to get the EPUB generated with no errors, and resolved the navigation issues. 
3. A problem with the html nodes I get back from Novelfull, had me make an `if` statement to check if there was enough content to make a chapter. If not, then I would just grab all the `<p>` tags and make a chapter out of that.
4. Need to delete the `RemoveAllAsync()` method from the `NovelService` class, as it shouldn't be something a user should be able to do.
5. Share this on reddit, and how it goes.
6. Ohh, and fix the issue where an epub is being overwritten when a novel is updated, with just the new chapters.



