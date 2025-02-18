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
- [2024](#2024)
- [2025](#2025)
  - [February](#february)
    - [It all started with a small change...](#it-all-started-with-a-small-change)
    - [Single Scraper from `HttpNovelScraper`/`SeleniumNovelScraper` => `NovelScraper`](#single-scraper-from-httpnovelscraperseleniumnovelscraper--novelscraper)
    - [02/09/25 - 02/13/25](#020925---021325)
    - [TODO](#todo)

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
05/01/2023
### Changing HttpNovelScraper.cs and SeleniumNovelScraper.cs to be base classes for the specific site scrapers / Goals for this month
1. I tried to avoid this, but it seems like the best way to go about it. The base classes will contain the common methods and properties that are used by the specific site scrapers. The specific site scrapers will contain the methods and properties that are unique to the site.
The only question now, is whether to use `abstract` or `virtual` methods.
2. Last week, I was finally able to get the EPUB generated with no errors, and resolved the navigation issues. 
3. A problem with the html nodes I get back from Novelfull, had me make an `if` statement to check if there was enough content to make a chapter. If not, then I would just grab all the `<p>` tags and make a chapter out of that.
4. Need to delete the `RemoveAllAsync()` method from the `NovelService` class, as it shouldn't be something a user should be able to do.
5. Share this on reddit, and how it goes.
6. Ohh, and fix the issue where an epub is being overwritten when a novel is updated, with just the new chapters.

# 2024
- Didn't really use this much, Baldur's Gate 3 was released and so were a few games.
- I started working on my website and had enough books to read.
# 2025
## February
02/03/25 - 02/08/25
### It all started with a small change...
- I have **scraped up** some motivation to try and tackle the issues with Cloudflare bot protections. After several searches on I finally decided to give Puppeteer a shot!. I really should have done it earlier as it's headless and stealth options really work.
So I tried a few test by just creating a puppeteerService and seeing if I could access lightnovelworld and get chapters. After that worked, there was no need for a Selenium scraper, while removing that I had to make the previous `HttpScraper`
behave without changing too much. Once I found out it was easier to just pass in the puppeteerService into it and still keep my Strategy pattern the same, with the exception of sites that would require a browser, I realized there was no need to
keep trying to eventually create a separate `SeleniumNovelScraper`. With that realization, changes had to be made in the `NovelScraperFactory` and then a few more places, and by the time of this writing I am having Rider telling me I should change all
simple constructors to `Primary Constructors`, which means I need to touch pretty much every file.
- All the above to say, I decided to do a rewrite of the code, I hope that not much changes and that it looks slightly better than the original mess I made.
- A lot of things still aren't where I think others would consider this a polished project, but it works and I will have that be the standard as long as I don't see new Issues.
#### Single Scraper from `HttpNovelScraper`/`SeleniumNovelScraper` => `NovelScraper`
- `HttpNovelScraper` will now handle the instantiations of Strategies. The dev should be aware if the site they are trying to scrape requires a browser to access a piece of data. This removes the need for multiple scrapers that are the same except for the parameter passed into a constructor.
```csharp
private void RegisterStrategy()
        {
            // LightNovelWorld requires a browser to bypass Cloudflare, everything that does not need browser will not need anythig passed as a dependancy.
            AddStrategy("https://www.lightnovelworld.com", new LightNovelWorldStrategy(_puppeteerDriverService));
            AddStrategy("https://novelfull.com", new NovelFullStrategy());
            AddStrategy("https://mangakakalot.to", new MangaKakalotStrategy());
            AddStrategy("https://mangareader.to", new MangaReaderStrategy());
            AddStrategy("https://mangakatana.com", new MangaKatanaStrategy());
            AddStrategy("https://noveldrama.com", new NovelDramaStrategy());
        }
```
- **Property Pattern for `is`**: I found out about this to avoid Rider warnings for possibly null patterns [Property Patterns](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns)

#### 02/09/25  - 02/13/25
1. Five days later, only 3 days working on this and I just yesterday was successfully able to scrape Cloudflare sites using Puppeteer, though things are extremely slow. I will need to do test based on concurrency.
2. Original http scrapers still work fine.
3. Using `tasks.Add(Task.Run(async () => {....}))` to run the scraping tasks concurrently maintained the order of the chapters in tasks. That was nice to know.
### TODO
1. Get manga sites to work with Puppeteer. The issue seems to be about waiting for elements such as images to load.
2. Split up `NovelProcessor` and `ScraperStrategy` into smaller classes as it is too long and hard to follow.
3. Add a new `configuration` row; change `concurrency_limit` to `http_concurrency_limit` and add `puppeteer_concurrency_limit`, which will allow for users to set limits based on the scraper type. 
4. . It may be a good idea to allow for each site to have its own concurrency limit, if none is provided then the default will be used. This will allow for more granular control based on users' preferences.
5. Go back to working on either the game, the app, or the website.
#### 02/11/25 - 02/17/25
1. Manga scraping works with Puppeteer, tried with 1 semaphore and one page. Resolved UTF16 errors when getting content base on. https://stackoverflow.com/questions/38895537/how-to-remove-utf16-characters-from-string
2. I still need to split up and decouple Scraper Strategy.
3. The issue with **PdfSharp** was resolved after finding a solution on their github page. A new extension method has been added to the `Helper` folder.
4. I need to create a method whose only responsibility is getting `ChapterData` as, all my method return the exact same thing a `List<ChapterData`, yet they each call their own thing.