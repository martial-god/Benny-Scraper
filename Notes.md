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
- **Property Pattern for `is`**: I found out about this to avoid Rider warnings for possibly null patterns [Property Patterns](<!DOCTYPE html><html lang="en" xml:lang="en" xmlns="http://www.w3.org/1999/xhtml" theme="dark" bgcolor="black" hgcolor="purple" brstype="0"><head>
    <meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, minimum-scale=1.0, maximum-scale=5.0">
    <meta name="description" content="List of the most recent chapters published for the A Depressed Kendo Player Possesses a Bastard Aristocrat novel. A total of 216 chapters have been translated and the last update in the novel is Chapter 126.2: The Start of a New Semester Part 2">
    <meta name="keywords" content="novel, light novel, web novel, chinese novel, korean novel, novel chapter, novel updates, A Depressed Kendo Player Possesses a Bastard Aristocrat">
    <meta name="mobile-web-app-capable" content="yes">
    <meta property="og:type" content="website">
    <meta property="og:url" content="https://www.lightnovelworld.com/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat/chapters?page=3">
    <meta property="og:site_name" content="lightnovelworld.com">
    <meta property="og:title" content="A Depressed Kendo Player Possesses a Bastard Aristocrat Novel Chapters | Light Novel World">
    <meta property="og:description" content="List of the most recent chapters published for the A Depressed Kendo Player Possesses a Bastard Aristocrat novel. A total of 216 chapters have been translated and the last update in the novel is Chapter 126.2: The Start of a New Semester Part 2">
    <meta property="og:image" content="https://static.lightnovelworld.com/bookcover/300x400/01695-a-depressed-kendo-player-possesses-a-bastard-aristocrat.jpg">
    <meta property="og:locale" content="en_US">
    <meta name="twitter:site" content="lightnovelworld.com">
    <meta name="twitter:creator" content="lightnovelworld.com">
    <meta name="twitter:card" content="summary_large_image">
    <meta name="twitter:title" content="A Depressed Kendo Player Possesses a Bastard Aristocrat Novel Chapters | Light Novel World">
    <meta name="twitter:description" content="List of the most recent chapters published for the A Depressed Kendo Player Possesses a Bastard Aristocrat novel. A total of 216 chapters have been translated and the last update in the novel is Chapter 126.2: The Start of a New Semester Part 2">
    <meta name="twitter:url" content="https://www.lightnovelworld.com/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat/chapters?page=3">
    <meta name="twitter:image" content="https://static.lightnovelworld.com/bookcover/300x400/01695-a-depressed-kendo-player-possesses-a-bastard-aristocrat.jpg">
    <link rel="canonical" href="https://www.lightnovelworld.com/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat/chapters?page=3">

            <link rel="prev" href="https://www.lightnovelworld.com/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat/chapters?page=2">

    <link rel="apple-touch-icon" href="/apple-touch-icon.png">
    <link rel="icon" type="image/png" href="/favicon.png">
    <link rel="manifest" href="/manifest.json">
    <link rel="preconnect" href="https://static.lightnovelworld.com" crossorigin="">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="">
    <link rel="preconnect" href="https://fonts.googleapis.com" crossorigin="">
    <link rel="preconnect" href="https://a.pub.network" crossorigin="">
    <title>A Depressed Kendo Player Possesses a Bastard Aristocrat Novel Chapters | Light Novel World</title>

    <link rel="stylesheet" as="style" href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@700&amp;family=Nunito+Sans:wght@400;600;700&amp;family=Roboto:wght@400;700&amp;display=swap" crossorigin="" onload="this.rel='stylesheet'">
    <noscript>
        <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@700&family=Nunito+Sans:wght@400;600;700&family=Roboto:wght@400;700&display=swap">
    </noscript>


        <link rel="stylesheet" type="text/css" href="https://static.lightnovelworld.com/content/fontello/css/fontello-embedded.css?v=31051253">
        <link rel="stylesheet" type="text/css" href="https://static.lightnovelworld.com/content/css/navbar.min.css?v=31051253">
        <link rel="stylesheet" type="text/css" href="https://static.lightnovelworld.com/content/css/media-mobile.min.css?v=31051253">
        <link rel="stylesheet" type="text/css" media="screen and (min-width: 768px)" href="https://static.lightnovelworld.com/content/css/media-768.min.css?v=31051253">
        <link rel="stylesheet" type="text/css" media="screen and (min-width: 1024px)" href="https://static.lightnovelworld.com/content/css/media-1024.min.css?v=31051253">
        <link rel="stylesheet" type="text/css" media="screen and (min-width: 1270px)" href="https://static.lightnovelworld.com/content/css/media-1270.min.css?v=31051253">
            <link rel="stylesheet" type="text/css" media="screen" href="https://static.lightnovelworld.com/content/css/jquery.smartbanner.min.css?v=31051253">




<style>
    :root[theme="light"] {
        --logoimg: url('https://static.lightnovelworld.com/content/img/lightnovelworld/logo.png');
        --logoxlimg: url('https://static.lightnovelworld.com/content/img/lightnovelworld/logo-xl.png');
    }
    :root[theme="dark"] {
        --logoimg: url('https://static.lightnovelworld.com/content/img/lightnovelworld/logo-dark.png');
        --logoxlimg: url('https://static.lightnovelworld.com/content/img/lightnovelworld/logo-xl-dark.png');
    }
</style>


<!-- InMobi Choice. Consent Manager Tag v3.0 (for TCF 2.2) -->
<script type="text/javascript" async="" src="https://www.googletagmanager.com/gtag/js?id=G-HZJJ4QCWBY&amp;l=dataLayer&amp;cx=c&amp;gtm=45He5230v9119291790za200"></script><script async="" type="text/javascript" src="https://cmp.inmobi.com/tcfv2/cmp2.js?referer=www.lightnovelworld.com"></script><script async="" src="https://www.googletagmanager.com/gtm.js?id=GTM-5KVSC8G"></script><script async="" type="text/javascript" src="https://cmp.inmobi.com/choice/1F3GqBHWzT9-j/www.lightnovelworld.com/choice.js?tag_version=V3"></script><script type="text/javascript" async="true">
(function() {
var host = window.location.hostname;
var element = document.createElement('script');
var firstScript = document.getElementsByTagName('script')[0];
var url = 'https://cmp.inmobi.com'
.concat('/choice/', '1F3GqBHWzT9-j', '/', host, '/choice.js?tag_version=V3');
var uspTries = 0;
var uspTriesLimit = 3;
element.async = true;
element.type = 'text/javascript';
element.src = url;

firstScript.parentNode.insertBefore(element, firstScript);

function makeStub() {
var TCF_LOCATOR_NAME = '__tcfapiLocator';
var queue = [];
var win = window;
var cmpFrame;

    function addFrame() {
      var doc = win.document;
      var otherCMP = !!(win.frames[TCF_LOCATOR_NAME]);

      if (!otherCMP) {
        if (doc.body) {
          var iframe = doc.createElement('iframe');

          iframe.style.cssText = 'display:none';
          iframe.name = TCF_LOCATOR_NAME;
          doc.body.appendChild(iframe);
        } else {
          setTimeout(addFrame, 5);
        }
      }
      return !otherCMP;
    }

    function tcfAPIHandler() {
      var gdprApplies;
      var args = arguments;

      if (!args.length) {
        return queue;
      } else if (args[0] === 'setGdprApplies') {
        if (
          args.length > 3 &&
          args[2] === 2 &&
          typeof args[3] === 'boolean'
        ) {
          gdprApplies = args[3];
          if (typeof args[2] === 'function') {
            args[2]('set', true);
          }
        }
      } else if (args[0] === 'ping') {
        var retr = {
          gdprApplies: gdprApplies,
          cmpLoaded: false,
          cmpStatus: 'stub'
        };

        if (typeof args[2] === 'function') {
          args[2](retr);
        }
      } else {
        if(args[0] === 'init' && typeof args[3] === 'object') {
          args[3] = Object.assign(args[3], { tag_version: 'V3' });
        }
        queue.push(args);
      }
    }

    function postMessageEventHandler(event) {
      var msgIsString = typeof event.data === 'string';
      var json = {};

      try {
        if (msgIsString) {
          json = JSON.parse(event.data);
        } else {
          json = event.data;
        }
      } catch (ignore) {}

      var payload = json.__tcfapiCall;

      if (payload) {
        window.__tcfapi(
          payload.command,
          payload.version,
          function(retValue, success) {
            var returnMsg = {
              __tcfapiReturn: {
                returnValue: retValue,
                success: success,
                callId: payload.callId
              }
            };
            if (msgIsString) {
              returnMsg = JSON.stringify(returnMsg);
            }
            if (event && event.source && event.source.postMessage) {
              event.source.postMessage(returnMsg, '*');
            }
          },
          payload.parameter
        );
      }
    }

    while (win) {
      try {
        if (win.frames[TCF_LOCATOR_NAME]) {
          cmpFrame = win;
          break;
        }
      } catch (ignore) {}

      if (win === window.top) {
        break;
      }
      win = win.parent;
    }
    if (!cmpFrame) {
      addFrame();
      win.__tcfapi = tcfAPIHandler;
      win.addEventListener('message', postMessageEventHandler, false);
    }
};

makeStub();

function makeGppStub() {
const CMP_ID = 10;
const SUPPORTED_APIS = [
'2:tcfeuv2',
'6:uspv1',
'7:usnatv1',
'8:usca',
'9:usvav1',
'10:uscov1',
'11:usutv1',
'12:usctv1'
];

    window.__gpp_addFrame = function (n) {
      if (!window.frames[n]) {
        if (document.body) {
          var i = document.createElement("iframe");
          i.style.cssText = "display:none";
          i.name = n;
          document.body.appendChild(i);
        } else {
          window.setTimeout(window.__gpp_addFrame, 10, n);
        }
      }
    };
    window.__gpp_stub = function () {
      var b = arguments;
      __gpp.queue = __gpp.queue || [];
      __gpp.events = __gpp.events || [];

      if (!b.length || (b.length == 1 && b[0] == "queue")) {
        return __gpp.queue;
      }

      if (b.length == 1 && b[0] == "events") {
        return __gpp.events;
      }

      var cmd = b[0];
      var clb = b.length > 1 ? b[1] : null;
      var par = b.length > 2 ? b[2] : null;
      if (cmd === "ping") {
        clb(
          {
            gppVersion: "1.1", // must be “Version.Subversion”, current: “1.1”
            cmpStatus: "stub", // possible values: stub, loading, loaded, error
            cmpDisplayStatus: "hidden", // possible values: hidden, visible, disabled
            signalStatus: "not ready", // possible values: not ready, ready
            supportedAPIs: SUPPORTED_APIS, // list of supported APIs
            cmpId: CMP_ID, // IAB assigned CMP ID, may be 0 during stub/loading
            sectionList: [],
            applicableSections: [-1],
            gppString: "",
            parsedSections: {},
          },
          true
        );
      } else if (cmd === "addEventListener") {
        if (!("lastId" in __gpp)) {
          __gpp.lastId = 0;
        }
        __gpp.lastId++;
        var lnr = __gpp.lastId;
        __gpp.events.push({
          id: lnr,
          callback: clb,
          parameter: par,
        });
        clb(
          {
            eventName: "listenerRegistered",
            listenerId: lnr, // Registered ID of the listener
            data: true, // positive signal
            pingData: {
              gppVersion: "1.1", // must be “Version.Subversion”, current: “1.1”
              cmpStatus: "stub", // possible values: stub, loading, loaded, error
              cmpDisplayStatus: "hidden", // possible values: hidden, visible, disabled
              signalStatus: "not ready", // possible values: not ready, ready
              supportedAPIs: SUPPORTED_APIS, // list of supported APIs
              cmpId: CMP_ID, // list of supported APIs
              sectionList: [],
              applicableSections: [-1],
              gppString: "",
              parsedSections: {},
            },
          },
          true
        );
      } else if (cmd === "removeEventListener") {
        var success = false;
        for (var i = 0; i < __gpp.events.length; i++) {
          if (__gpp.events[i].id == par) {
            __gpp.events.splice(i, 1);
            success = true;
            break;
          }
        }
        clb(
          {
            eventName: "listenerRemoved",
            listenerId: par, // Registered ID of the listener
            data: success, // status info
            pingData: {
              gppVersion: "1.1", // must be “Version.Subversion”, current: “1.1”
              cmpStatus: "stub", // possible values: stub, loading, loaded, error
              cmpDisplayStatus: "hidden", // possible values: hidden, visible, disabled
              signalStatus: "not ready", // possible values: not ready, ready
              supportedAPIs: SUPPORTED_APIS, // list of supported APIs
              cmpId: CMP_ID, // CMP ID
              sectionList: [],
              applicableSections: [-1],
              gppString: "",
              parsedSections: {},
            },
          },
          true
        );
      } else if (cmd === "hasSection") {
        clb(false, true);
      } else if (cmd === "getSection" || cmd === "getField") {
        clb(null, true);
      }
      //queue all other commands
      else {
        __gpp.queue.push([].slice.apply(b));
      }
    };
    window.__gpp_msghandler = function (event) {
      var msgIsString = typeof event.data === "string";
      try {
        var json = msgIsString ? JSON.parse(event.data) : event.data;
      } catch (e) {
        var json = null;
      }
      if (typeof json === "object" && json !== null && "__gppCall" in json) {
        var i = json.__gppCall;
        window.__gpp(
          i.command,
          function (retValue, success) {
            var returnMsg = {
              __gppReturn: {
                returnValue: retValue,
                success: success,
                callId: i.callId,
              },
            };
            event.source.postMessage(msgIsString ? JSON.stringify(returnMsg) : returnMsg, "*");
          },
          "parameter" in i ? i.parameter : null,
          "version" in i ? i.version : "1.1"
        );
      }
    };
    if (!("__gpp" in window) || typeof window.__gpp !== "function") {
      window.__gpp = window.__gpp_stub;
      window.addEventListener("message", window.__gpp_msghandler, false);
      window.__gpp_addFrame("__gppLocator");
    }
};

makeGppStub();

var uspStubFunction = function() {
var arg = arguments;
if (typeof window.__uspapi !== uspStubFunction) {
setTimeout(function() {
if (typeof window.__uspapi !== 'undefined') {
window.__uspapi.apply(window.__uspapi, arg);
}
}, 500);
}
};

var checkIfUspIsReady = function() {
uspTries++;
if (window.__uspapi === uspStubFunction && uspTries < uspTriesLimit) {
console.warn('USP is not accessible');
} else {
clearInterval(uspInterval);
}
};

if (typeof window.__uspapi === 'undefined') {
window.__uspapi = uspStubFunction;
var uspInterval = setInterval(checkIfUspIsReady, 6000);
}
})();
</script>
<!-- End InMobi Choice. Consent Manager Tag v3.0 (for TCF 2.2) -->

<script>(function(w,d,s,l,i){w[l]=w[l]||[];w[l].push({'gtm.start':
new Date().getTime(),event:'gtm.js'});var f=d.getElementsByTagName(s)[0],
j=d.createElement(s),dl=l!='dataLayer'?'&l='+l:'';j.async=true;j.src=
'https://www.googletagmanager.com/gtm.js?id='+i+dl;f.parentNode.insertBefore(j,f);
})(window,document,'script','dataLayer','GTM-5KVSC8G');</script>


<!--sse--><script src="https://hb.vntsm.com/v4/live/vms/sites/lightnovelworld.com/index.js" async=""></script>
<script>
self.__VM = self.__VM || [];
self.__VM.push(function (admanager, scope) {
	scope.CMP.disableCMPModule();
    scope.Config.buildPlacement((configBuilder) => {
        configBuilder.add("billboard");
        configBuilder.addDefaultOrUnique("leaderboard").setBreakPoint({mediaQuery: "(min-width:768px) and (max-width:1024px)"});
        configBuilder.addDefaultOrUnique("mobile_mpu").setBreakPoint({mediaQuery: "max-width:768px"});
    }).displayMany(["vn-slot-1","vn-slot-2","vn-slot-3","vn-slot-4","vn-slot-5","vn-slot-6","vn-slot-7"]);
    scope.Config.buildPlacement((configBuilder) => {
    	configBuilder.add("billboard");
        configBuilder.addDefaultOrUnique("leaderboard").setBreakPoint({ mediaQuery: "(min-width:768px) and (max-width:1024px)" });
        configBuilder.addDefaultOrUnique("mobile_banner").setBreakPoint({mediaQuery: 'max-width:768px'});
    }).display("vn-slot-top");
});
</script>
<style>
	.vnad, .vnad-in { line-height:0; }
@media (min-width: 768px) { 
	.vnad { min-height: 130px !important; background-color: var(--bg-color) !important; }
	.vnad-in { min-height: 110px !important; }
}
@media (min-width: 1024px) {
    .vnad { min-height: 290px !important; 
    	display:flex !important; justify-content: center; align-items: center;  
    }
	.vnad-in { min-height: 270px !important; 
		display:flex !important; justify-content: center; align-items: center;  
	}
}
</style><!--/sse-->
        <link rel="stylesheet" href="/content/css/app.css?v=0502013222" type="text/css">

    
    
        <link rel="stylesheet" type="text/css" href="https://static.lightnovelworld.com/content/css/novel.chapter-review.min.css?v=31051253">
    
    

    <link rel="stylesheet" type="text/css" href="https://static.lightnovelworld.com/content/css/pagedlist.css?v=31051253">



<style type="text/css"> .qc-cmp-button.qc-cmp-secondary-button:hover {    background-color: #368bd6 !important;    border-color: transparent !important;  }  .qc-cmp-button.qc-cmp-secondary-button:hover {    color: #ffffff !important;  }  .qc-cmp-button.qc-cmp-secondary-button {    color: #368bd6 !important;  }  .qc-cmp-button.qc-cmp-secondary-button {    background-color: #eee !important;    border-color: transparent !important;  } </style><script type="text/javascript" async="" importance="high" fetchpriority="high" src="https://hb.vntsm.com/v4/live/vms/ad-manager.js"></script><style>.OckdzH2HZ35tK3y7tXRa:before,.OckdzH2HZ35tK3y7tXRa:after{content:unset}.OckdzH2HZ35tK3y7tXRa{width:100%;height:100%}.OckdzH2HZ35tK3y7tXRa,.OckdzH2HZ35tK3y7tXRa:before,.OckdzH2HZ35tK3y7tXRa:after,.OckdzH2HZ35tK3y7tXRa *{-webkit-hyphens:manual;hyphens:manual;font-size:16px;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Helvetica,Arial,sans-serif;color:inherit;background:0 0;border:0;border-radius:0;border-spacing:0;border-collapse:collapse;box-sizing:content-box;clear:none;float:none;font-variant:normal;font-weight:inherit;letter-spacing:normal;line-height:1.4;margin:0;max-height:none;max-width:none;min-height:0;min-width:0;outline:0;padding:0;position:static;text-align:left;text-decoration:none;text-indent:0;text-transform:none;vertical-align:baseline;visibility:inherit;word-spacing:normal}.OckdzH2HZ35tK3y7tXRa{display:inline-block}.SgWToQqFfGrcMwzkaW13{position:absolute;top:0;left:0;right:0;bottom:0;z-index:1;-webkit-user-select:none;-moz-user-select:none;user-select:none}.XEsSILaeeM5KintRuPQD{transition:opacity 250ms ease 0s,visibility 250ms ease 0s;opacity:.01;visibility:hidden}.XEsSILaeeM5KintRuPQD div.plUlYp04adKwyn0qvHdQ{position:absolute;top:0;left:0;right:0;height:40%;background:linear-gradient(rgba(0, 0, 0, 0.85), rgba(0, 0, 0, 0.45) 40%, rgba(0, 0, 0, 0));pointer-events:none}.XEsSILaeeM5KintRuPQD div.nYN3g178d4YL2Vd4s6mA{position:absolute;bottom:0;left:0;right:0;height:66%;background:linear-gradient(0deg, rgba(0, 0, 0, 0.85), rgba(0, 0, 0, 0.45) 40%, rgba(0, 0, 0, 0));pointer-events:none}.Ad8QhrSE3GIISryPO9T7{position:absolute;top:50%;left:50%;margin:-25px 0 0 -25px;opacity:1;text-align:left;border:4px solid hsla(0,0%,100%,.5);box-sizing:border-box;background-clip:padding-box;width:50px;height:50px;border-radius:25px;visibility:hidden;display:none}.Ad8QhrSE3GIISryPO9T7:before,.Ad8QhrSE3GIISryPO9T7:after{content:"";position:absolute;margin:-4px;box-sizing:inherit;width:inherit;height:inherit;border-radius:inherit;opacity:1;border:inherit;border-color:rgba(0,0,0,0);border-top-color:#fff}.wn9PyoRcUdqB4tErguAA .Ad8QhrSE3GIISryPO9T7{display:block;animation:BKrYahB8a1dyv1rM5zwt 0s linear .3s forwards}.wn9PyoRcUdqB4tErguAA .Ad8QhrSE3GIISryPO9T7:before,.wn9PyoRcUdqB4tErguAA .Ad8QhrSE3GIISryPO9T7:after{animation:G_FiHUCEH0pS_GOFLE7m 1.1s cubic-bezier(0.6, 0.2, 0, 0.8) infinite,juB6kt12QpEiyg0aiXuA 1.1s linear infinite}.wn9PyoRcUdqB4tErguAA .Ad8QhrSE3GIISryPO9T7:before{border-top-color:#fff}.wn9PyoRcUdqB4tErguAA .Ad8QhrSE3GIISryPO9T7:after{border-top-color:#fff;animation-delay:.44s}@keyframes BKrYahB8a1dyv1rM5zwt{to{visibility:visible}}@keyframes G_FiHUCEH0pS_GOFLE7m{100%{transform:rotate(360deg)}}@keyframes juB6kt12QpEiyg0aiXuA{0%{border-top-color:#fff}20%{border-top-color:#fff}35%{border-top-color:#fff}60%{border-top-color:#fff}100%{border-top-color:#fff}}.REl3d1_VAR8A7b7vccdK{position:absolute;top:0;left:0;right:0;display:flex;flex-direction:column;align-items:flex-start;z-index:1;padding:25px 20px;color:#fff;transition:opacity 250ms ease 0s,visibility 250ms ease 0s;opacity:.01;visibility:hidden}.REl3d1_VAR8A7b7vccdK div.ezfjx_GIDOUc2iTNUtLj{overflow:hidden;white-space:nowrap;text-overflow:ellipsis;width:100%;font-size:18px}.REl3d1_VAR8A7b7vccdK div.F1qbAuN_fWyiu5DJ9T6P{font-size:14px}.V2GUOOkiXtNjrwoqx3bi{position:absolute;bottom:0;left:0;right:0;height:65px}._05mhNR8IvuDeKPShF6Q{position:absolute;left:20px;right:20px;bottom:56px;height:4px;cursor:pointer;background:hsla(0,0%,100%,.5);box-shadow:0 0 3px 0 rgba(0,0,0,.1);touch-action:none;transition:opacity 250ms ease 0s,visibility 250ms ease 0s;opacity:.01;visibility:hidden}._05mhNR8IvuDeKPShF6Q div.ZEAQM3Ac3aMIz4CxD2lj{position:absolute;left:0;height:100%;background:#fff}._05mhNR8IvuDeKPShF6Q div.bJA13Vtw9S9Ha8leReHy{position:absolute;left:0;height:100%;background:red}._05mhNR8IvuDeKPShF6Q div.bJA13Vtw9S9Ha8leReHy:before{transition-timing-function:cubic-bezier(0, 0, 0.2, 1);transition-duration:167ms;content:"";position:absolute;background-color:#fff;width:10px;height:10px;border-radius:5px;box-shadow:0 1px 1px 1px rgba(0,0,0,.4);top:50%;right:-5px;margin-top:-5px}._05mhNR8IvuDeKPShF6Q:before{content:"";width:100%;position:absolute;left:0;height:100%}._05mhNR8IvuDeKPShF6Q:hover .bJA13Vtw9S9Ha8leReHy:before{transform:scale(1.5)}.q2ybE239VmeGBKJ80hDc ._05mhNR8IvuDeKPShF6Q,._05mhNR8IvuDeKPShF6Q:hover,._05mhNR8IvuDeKPShF6Q.M95eHMAUv5CUmYBwqEl9{height:8px;bottom:54px}.SrjgM0k33V0fcCehqUDD{position:absolute;bottom:0;left:0;right:0;height:45px;display:flex;flex-direction:row;justify-content:flex-start;padding:0 20px 20px;align-items:center;z-index:10;pointer-events:all;box-sizing:border-box;color:#fff;transition:opacity 250ms ease 0s,visibility 250ms ease 0s;opacity:.01;visibility:hidden}.GLATa8hIWPCjcOXMbMd5{background-image:url(data:image/svg+xml;base64,PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz4NCjwhLS0gR2VuZXJhdG9yOiBBZG9iZSBJbGx1c3RyYXRvciAyMi4xLjAsIFNWRyBFeHBvcnQgUGx1Zy1JbiAuIFNWRyBWZXJzaW9uOiA2LjAwIEJ1aWxkIDApICAtLT4NCjxzdmcgdmVyc2lvbj0iMS4xIiBpZD0iTGF5ZXJfMSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIiB4bWxuczp4bGluaz0iaHR0cDovL3d3dy53My5vcmcvMTk5OS94bGluayIgeD0iMHB4IiB5PSIwcHgiDQoJIHZpZXdCb3g9IjAgMCA3NTIgMTAyNCIgc3R5bGU9ImVuYWJsZS1iYWNrZ3JvdW5kOm5ldyAwIDAgNzUyIDEwMjQ7IiB4bWw6c3BhY2U9InByZXNlcnZlIj4NCjxzdHlsZSB0eXBlPSJ0ZXh0L2NzcyI+DQoJLnN0MHtmaWxsOiNGRkZGRkY7fQ0KPC9zdHlsZT4NCjxwb2x5Z29uIGNsYXNzPSJzdDAiIHBvaW50cz0iNzUyLDUxMiAwLDAgMCwxMDI0ICIvPg0KPC9zdmc+DQo=);padding:0;background-color:rgba(0,0,0,0);border:none;background-repeat:no-repeat;background-position:center center;outline:none;width:24px;height:24px;background-size:15px;cursor:pointer}.GLATa8hIWPCjcOXMbMd5.wPBrzQAzd1hcjYMR9eWJ{background-image:url(data:image/svg+xml;base64,PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz4NCjwhLS0gR2VuZXJhdG9yOiBBZG9iZSBJbGx1c3RyYXRvciAyMi4xLjAsIFNWRyBFeHBvcnQgUGx1Zy1JbiAuIFNWRyBWZXJzaW9uOiA2LjAwIEJ1aWxkIDApICAtLT4NCjxzdmcgdmVyc2lvbj0iMS4xIiBpZD0iTGF5ZXJfMSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIiB4bWxuczp4bGluaz0iaHR0cDovL3d3dy53My5vcmcvMTk5OS94bGluayIgeD0iMHB4IiB5PSIwcHgiDQoJIHZpZXdCb3g9IjAgMCA3ODQgMTAyNCIgc3R5bGU9ImVuYWJsZS1iYWNrZ3JvdW5kOm5ldyAwIDAgNzg0IDEwMjQ7IiB4bWw6c3BhY2U9InByZXNlcnZlIj4NCjxzdHlsZSB0eXBlPSJ0ZXh0L2NzcyI+DQoJLnN0MHtmaWxsOiNGRkZGRkY7fQ0KPC9zdHlsZT4NCjxwYXRoIGNsYXNzPSJzdDAiIGQ9Ik01ODIuMiwwaDkuOGMzOSwwLDY4LjMsMjkuMyw2OC4zLDY4LjN2ODg3LjVjMCwzOS0yOS4zLDY4LjMtNjguMyw2OC4zaC05LjhjLTM5LDAtNjguMy0yOS4zLTY4LjMtNjguM1Y2OC4zDQoJQzUxMy45LDI5LjMsNTQzLjIsMCw1ODIuMiwwTDU4Mi4yLDB6Ii8+DQo8cGF0aCBjbGFzcz0ic3QwIiBkPSJNMTkyLjEsMGg5LjhjMzksMCw2OC4zLDI5LjMsNjguMyw2OC4zdjg4Ny41YzAsMzktMjkuMyw2OC4zLTY4LjMsNjguM2gtOS44Yy0zOSwwLTY4LjMtMjkuMy02OC4zLTY4LjNWNjguMw0KCUMxMjMuOCwyOS4zLDE1My4xLDAsMTkyLjEsMEwxOTIuMSwweiIvPg0KPC9zdmc+DQo=);background-size:14px}.iHr4_Nk75VKHQ0Iv6Bhi{background-image:url(data:image/svg+xml;base64,PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz4NCjwhLS0gR2VuZXJhdG9yOiBBZG9iZSBJbGx1c3RyYXRvciAyMi4xLjAsIFNWRyBFeHBvcnQgUGx1Zy1JbiAuIFNWRyBWZXJzaW9uOiA2LjAwIEJ1aWxkIDApICAtLT4NCjxzdmcgdmVyc2lvbj0iMS4xIiBpZD0iTGF5ZXJfMSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIiB4bWxuczp4bGluaz0iaHR0cDovL3d3dy53My5vcmcvMTk5OS94bGluayIgeD0iMHB4IiB5PSIwcHgiDQoJIHZpZXdCb3g9IjAgMCA4ODAgMTAyNCIgc3R5bGU9ImVuYWJsZS1iYWNrZ3JvdW5kOm5ldyAwIDAgODgwIDEwMjQ7IiB4bWw6c3BhY2U9InByZXNlcnZlIj4NCjxzdHlsZSB0eXBlPSJ0ZXh0L2NzcyI+DQoJLnN0MHtmaWxsOiNGRkZGRkY7fQ0KPC9zdHlsZT4NCjxwYXRoIGNsYXNzPSJzdDAiIGQ9Ik04MDAuOCwwaDkuOGMzOSwwLDY4LjMsMjkuMyw2OC4zLDY4LjN2ODg3LjVjMCwzOS0yOS4zLDY4LjMtNjguMyw2OC4zaC05LjhjLTM5LDAtNjguMy0yOS4zLTY4LjMtNjguM1Y2OC4zDQoJQzczMi42LDI5LjMsNzYxLjgsMCw4MDAuOCwwTDgwMC44LDB6Ii8+DQo8cGF0aCBjbGFzcz0ic3QwIiBkPSJNMS4xLDEwMjRjMCwwLDAtMzQxLjMsMC0xMDI0YzAsMCwyNDMuOCwxNjAuOSw3MzEuNCw0ODcuNmMwLDE5LjUsMCwyNC40LDAsNDguOA0KCUM3MzIuNiw1MjEuOCw0ODguOCw2ODIuNywxLjEsMTAyNHoiLz4NCjwvc3ZnPg0K);padding:0;background-color:rgba(0,0,0,0);border:none;background-repeat:no-repeat;background-position:center center;outline:none;width:24px;height:24px;background-size:16px;margin-left:6px;cursor:pointer}.hBjbX3lq67Lm2UFGZY4i{background-image:url(data:image/svg+xml;base64,PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz4NCjwhLS0gR2VuZXJhdG9yOiBBZG9iZSBJbGx1c3RyYXRvciAyMi4xLjAsIFNWRyBFeHBvcnQgUGx1Zy1JbiAuIFNWRyBWZXJzaW9uOiA2LjAwIEJ1aWxkIDApICAtLT4NCjxzdmcgdmVyc2lvbj0iMS4xIiBpZD0iTGF5ZXJfMSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIiB4bWxuczp4bGluaz0iaHR0cDovL3d3dy53My5vcmcvMTk5OS94bGluayIgeD0iMHB4IiB5PSIwcHgiDQoJIHZpZXdCb3g9IjAgMCAxMjE2IDEwMjQiIHN0eWxlPSJlbmFibGUtYmFja2dyb3VuZDpuZXcgMCAwIDEyMTYgMTAyNDsiIHhtbDpzcGFjZT0icHJlc2VydmUiPg0KPHN0eWxlIHR5cGU9InRleHQvY3NzIj4NCgkuc3Qwe2ZpbGw6I0ZGRkZGRjt9DQo8L3N0eWxlPg0KPHBhdGggY2xhc3M9InN0MCIgZD0iTTAsNzExLjloMjYzLjNMNjc3LjgsMTAyNFYwTDI2My4zLDMxMi4xSDBWNzExLjl6Ii8+DQo8cGF0aCBjbGFzcz0ic3QwIiBkPSJNODc3LjcsMzQxLjNsLTY4LjMsNjguM2M1My42LDU4LjUsNTMuNiwxNDYuMywwLDIwNC44bDY4LjMsNjguM0M5NzAuNCw1OTAsOTcwLjQsNDM0LDg3Ny43LDM0MS4zTDg3Ny43LDM0MS4zeg0KCSIvPg0KPHBhdGggY2xhc3M9InN0MCIgZD0iTTEwODcuNCwyMDQuOGwtNzMuMSw3OGM2OC4zLDczLjEsMTAyLjQsMTY1LjgsMTAyLjQsMjYzLjNjMCwxMDIuNC0zNC4xLDE5NS0xMDIuNCwyNjMuM2w3My4xLDc4DQoJYzgyLjktOTIuNiwxMzEuNy0yMTQuNiwxMzEuNy0zNDEuM1MxMTcwLjMsMjk3LjQsMTA4Ny40LDIwNC44TDEwODcuNCwyMDQuOHoiLz4NCjwvc3ZnPg0K);padding:0;background-color:rgba(0,0,0,0);border:none;background-repeat:no-repeat;background-position:center center;outline:none;width:24px;height:24px;background-size:24px;cursor:pointer;margin-left:10px}.hBjbX3lq67Lm2UFGZY4i.YRd9QsvBWJhyMoJyrkXM{background-image:url(data:image/svg+xml;base64,PD94bWwgdmVyc2lvbj0iMS4wIiBlbmNvZGluZz0idXRmLTgiPz4NCjwhLS0gR2VuZXJhdG9yOiBBZG9iZSBJbGx1c3RyYXRvciAyMi4xLjAsIFNWRyBFeHBvcnQgUGx1Zy1JbiAuIFNWRyBWZXJzaW9uOiA2LjAwIEJ1aWxkIDApICAtLT4NCjxzdmcgdmVyc2lvbj0iMS4xIiBpZD0iTGF5ZXJfMSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIiB4bWxuczp4bGluaz0iaHR0cDovL3d3dy53My5vcmcvMTk5OS94bGluayIgeD0iMHB4IiB5PSIwcHgiDQoJIHZpZXdCb3g9IjAgMCAxMjE2IDEwMjQiIHN0eWxlPSJlbmFibGUtYmFja2dyb3VuZDpuZXcgMCAwIDEyMTYgMTAyNDsiIHhtbDpzcGFjZT0icHJlc2VydmUiPg0KPHN0eWxlIHR5cGU9InRleHQvY3NzIj4NCgkuc3Qwe2ZpbGw6I0ZGRkZGRjt9DQo8L3N0eWxlPg0KPHBhdGggY2xhc3M9InN0MCIgZD0iTTAsNzExLjloMjYzLjNMNjc3LjgsMTAyNFYwTDI2My4zLDMxMi4xSDBWNzExLjl6Ii8+DQo8cGF0aCBjbGFzcz0ic3QwIiBkPSJNMTE4Ny4xLDY4NC4ybC0zOC4xLDM1LjdjLTkuMyw4LjctMjMuOCw4LjItMzIuNS0xLjFMNzczLjEsMzUyLjJjLTguNy05LjMtOC4yLTIzLjgsMS4xLTMyLjVsMzguMS0zNS43DQoJYzkuMy04LjcsMjMuOC04LjIsMzIuNSwxLjFsMzQzLjUsMzY2LjZDMTE5Ni44LDY2MSwxMTk2LjMsNjc1LjYsMTE4Ny4xLDY4NC4yeiIvPg0KPHBhdGggY2xhc3M9InN0MCIgZD0iTTc3NC4xLDY4NC4ybDM4LjEsMzUuN2M5LjMsOC43LDIzLjgsOC4yLDMyLjUtMS4xbDM0My41LTM2Ni42YzguNy05LjMsOC4yLTIzLjgtMS4xLTMyLjVsLTM4LjEtMzUuNw0KCWMtOS4zLTguNy0yMy44LTguMi0zMi41LDEuMUw3NzMuMSw2NTEuOEM3NjQuNCw2NjEsNzY0LjksNjc1LjYsNzc0LjEsNjg0LjJ6Ii8+DQo8L3N2Zz4NCg==)}.RV38o6lRkhp98cjLXPRY{margin-left:10px}.IszXaM3NrgI5TNOaiGik .XEsSILaeeM5KintRuPQD,.VERvBLx1_9XMjjDomCr_ .XEsSILaeeM5KintRuPQD,.IszXaM3NrgI5TNOaiGik .REl3d1_VAR8A7b7vccdK,.VERvBLx1_9XMjjDomCr_ .REl3d1_VAR8A7b7vccdK,.IszXaM3NrgI5TNOaiGik ._05mhNR8IvuDeKPShF6Q,.VERvBLx1_9XMjjDomCr_ ._05mhNR8IvuDeKPShF6Q,.IszXaM3NrgI5TNOaiGik .SrjgM0k33V0fcCehqUDD,.VERvBLx1_9XMjjDomCr_ .SrjgM0k33V0fcCehqUDD{opacity:1;visibility:visible}.WIFsZSlRUKx6W23mOuRg .SgWToQqFfGrcMwzkaW13,.WIFsZSlRUKx6W23mOuRg .XEsSILaeeM5KintRuPQD,.WIFsZSlRUKx6W23mOuRg .REl3d1_VAR8A7b7vccdK,.WIFsZSlRUKx6W23mOuRg ._05mhNR8IvuDeKPShF6Q,.WIFsZSlRUKx6W23mOuRg .SrjgM0k33V0fcCehqUDD{transition:none;opacity:0;visibility:hidden}.frB8hfiufxr29DmA9NSe{display:none}</style><iframe frameborder="0" marginwidth="0" marginheight="0" scrolling="no" webkitallowfullscreen="true" mozallowfullscreen="true" border="0" allowtransparency="true" allow="geolocation; microphone; camera; autoplay; fullscreen; payment; accelerometer; ; display-capture; gyroscope; magnetometer; midi; ch-ua-platform-version; ch-ua-model" style="border: 0px; display: none;"></iframe><script type="text/javascript" id="__tcfapiuiscript" src="https://cmp.inmobi.com/tcfv2/59/cmp2ui-en.js"></script><style qc-data-emotion="css-global" data-s=""></style><style qc-data-emotion="css" data-s=""></style></head>
<body class="fade-out " data-toolid="JPLKM_Qj" data-toolrun="true" data-domid="2" data-lnvstuid="true"><div class="qc-cmp2-container" id="qc-cmp2-container" data-nosnippet=""><div class="qc-cmp2-main" id="qc-cmp2-main" data-nosnippet=""><div height="600" class="qc-cmp-cleanslate css-1j1wpfg"><div id="qc-cmp2-usp" role="dialog" aria-labelledby="qc-usp-title" aria-modal="true" tabindex="0" class="css-bvrdvo"><button tabindex="0" aria-label="Close" aria-pressed="false" class="qc-usp-close-icon"></button><div class="qc-usp-ui-content"><p id="qc-usp-title" class="qc-usp-title">Light Novel World - Do Not Sell My Data</p><div class="qc-usp-main-messaging" tabindex="0"><div class="usp-dns"><p>We, and our partners, use technologies to process personal information, including IP addresses, pseudonymous identifiers associated with cookies, and in some cases mobile ad IDs.This information is processed to personalize content based on your interests, run and optimize marketing campaigns, measure the performance of ads and content, and derive insights about the audiences who engage with ads and content. This data is an integral part of how we operate our site, make revenue to support our staff, and generate relevant content for our audience. You can learn more about our data collection and use practices in our Privacy Policy.</p></div></div><div class="qc-usp-ui-form-content"><div class="qc-usp-container" style="max-height: 30vh; overflow: auto;"><ul class="qc-cmp2-consent-list css-rojy9y"><div class="qc-cmp2-scrollable-section"><ul class="qc-cmp2-consent-list"><li id="1" class="qc-cmp2-list-item "><button role="listitem" class="qc-cmp2-list-item-header" aria-label="Opt-Outs" aria-live="polite"><div class="titles-header"><p class="qc-cmp2-list-item-title">Personal Data Processing Opt Outs</p></div><svg type="expand" width="12px" height="19px" viewbox="0 0 12 19" version="1.1" class="css-jswnc6"><defs><path d="M3.88716886,8.47048371 L12.1431472,0.315826419 C12.4725453,-0.0145777987 13.005189,-0.0145777987 13.3345872,0.315826419 L13.8321886,0.814947685 C14.1615867,1.1453519 14.1615867,1.67962255 13.8321886,2.01002677 L6.6625232,9.06802326 L13.8251801,16.1260197 C14.1545782,16.456424 14.1545782,16.9906946 13.8251801,17.3210988 L13.3275787,17.8202201 C12.9981806,18.1506243 12.4655368,18.1506243 12.1361387,17.8202201 L3.88016039,9.6655628 C3.55777075,9.33515858 3.55777075,8.80088793 3.88716886,8.47048371 Z" id="path-1"></path><rect id="path-3" x="0" y="0" width="18" height="18"></rect></defs><g id="New---Mobile-2" stroke="none" stroke-width="1" fill="none" fill-rule="evenodd"><g id="iPhone-11-6-Copy" transform="translate(-23.000000, -138.000000)"><g id="v1" transform="translate(20.000000, 138.000000)"><g id="Icons/angle-left"><mask id="mask-2" fill="white"><use href="#path-1"></use></mask><use id="Mask" fill="currentColor" fill-rule="nonzero" href="#path-1"></use></g></g></g></g></svg></button></li></ul></div></ul></div><button aria-label="CONFIRM" aria-pressed="false" size="large" mode="primary" class=" css-47sehv">CONFIRM</button></div></div></div></div></div></div>
<!-- Google Tag Manager (noscript) -->
<noscript><iframe src="https://www.googletagmanager.com/ns.html?id=GTM-5KVSC8G" height="0" width="0" style="display:none;visibility:hidden"></iframe></noscript>
<!-- End Google Tag Manager (noscript) -->    <header class="main-header skiptranslate">
        <div class="wrapper">
                <a class="nav-logo" href="/hub_10102358" title="Read Most Popular Light Novels Online for Free | Light Novel World">
                    <img src="https://static.lightnovelworld.com/content/img/lightnovelworld/logo-dark.png" alt="Light Novel World">
                </a>
            <div class="navigation-bar" style="">
                <nav>
                    <span class="lnw-slog">Your fictional stories hub.</span>
                    <ul class="navbar-menu">
                        <li class="nav-item">
                            <a class="nav-link" title="Search Light Novels" href="/search"><i class="icon-search"></i><span>Search</span></a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" title="Explore the Recently Added Light Novels" href="/browse/genre-all-25060123/order-new/status-all"><i class="icon-th-large"></i><span>Browse</span></a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" title="Novel Ranking" href="/ranking-10102358"><i class="icon-diamond"></i><span>Ranking</span></a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" title="Check out the recently added novel chapters" href="/latest-updates-10102358"><i class="icon-book-open"></i><span>Updates</span></a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" title="Explore the Novels with Advanced Filter Functions" href="/searchadv"><i class="icon-filter"></i><span>Filter</span></a>
                        </li>

                            <li class="nav-item">
                                <a class="nav-link" title="Light Novel World Development Announcements" href="/notices"><i class="icon-megaphone"></i><span>DEV</span></a>
                            </li>
                        <li class="nav-item">
                            <a class="darkmode_switch" title="Dark Theme Mode">
                                <i class="icon-theme-mode"></i>
                            </a>
                        </li>
                    </ul>
                </nav>
                <div class="login-wrap-nav">
                        <a class="nav-link login button" href="/account/logintypes">Sign In</a>
                </div>
            </div>
            <div class="nav-back">
                        <i class="icon-left-open"></i>
            </div>
            <button id="mobile-menu-btn">
                <div id="burger-btn"></div>
            </button>
            <span class="nav notify-bell mobile-block icon-bell-alt"></span>
        </div>
    </header>
    <div class="sidebar-wrapper"></div>
    <main role="main">



<article id="chapter-list-page">
    <header class="container">
        <div class="novel-item">
            <div class="cover-wrap">
                <a title="A Depressed Kendo Player Possesses a Bastard Aristocrat" href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat">
                    <figure class="novel-cover">
                        <img src="https://static.lightnovelworld.com/bookcover/158x210/01695-a-depressed-kendo-player-possesses-a-bastard-aristocrat.jpg" alt="A Depressed Kendo Player Possesses a Bastard Aristocrat">
                    </figure>
                </a>
            </div>
            <div class="item-body">
                <h1>
                    <a class="text2row" title="A Depressed Kendo Player Possesses a Bastard Aristocrat" href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat">A Depressed Kendo Player Possesses a Bastard Aristocrat</a>
                </h1>
                <div class="novel-stats">
                    <span>Updated <time datetime="2024-08-22 10:32">5 months ago</time></span>
                </div>
                <div class="novel-stats">
                    Status: <span class="status ">Ongoing</span>
                </div>
            </div>
        </div>
        <span class="divider"></span>
        <h2>A Depressed Kendo Player Possesses a Bastard Aristocrat Novel Chapters</h2>
        <p>
            List of most recent chapters published for the A Depressed Kendo Player Possesses a Bastard Aristocrat novel. A total of 216 chapters have been translated and the release date of the last chapter is Aug 22, 2024<br>
        </p>
        <p>
            Latest Release:<br>
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-126-2" title="Chapter 126.2: The Start of a New Semester Part 2">Chapter 126.2: The Start of a New Semester Part 2</a>
        </p>
    </header>
    <!--sse--><div class="container"><div id="vn-slot-1" class="JPLKM_Qj mQTNcWLA vnad-in"><span><span style="display: flex; align-items: center; justify-content: center; margin: 0px auto; width: 728px; height: 90px;"><span style="pointer-events: auto;"><iframe frameborder="0" marginwidth="0" marginheight="0" scrolling="no" webkitallowfullscreen="true" mozallowfullscreen="true" border="0" allowtransparency="true" allow="geolocation; microphone; camera; autoplay; fullscreen; payment; accelerometer; ; display-capture; gyroscope; magnetometer; midi; ch-ua-platform-version; ch-ua-model" style="border: 0px; width: 728px; height: 90px; max-width: none; display: inline; position: static; pointer-events: auto;"></iframe></span></span></span></div></div><!--/sse-->
    <section class="container" id="chpagedlist" data-load="0">




<svg aria-hidden="true" style="position: absolute; width: 0px; height: 0px; overflow: hidden;">
    <symbol id="i-rank-up" viewbox="0 0 1308 1024"><path d="M512 149.33333366666665h796.444444v113.777777H512V149.33333366666665z m0 341.333333h568.888889v113.777778H512V490.6666666666667z m0 341.333333h341.333333v113.777778H512v-113.777778zM227.555556 303.6159996666667L100.124444 452.9493336666667 13.653333 379.0506666666667 341.333333-4.949333333333332V1002.6666666666666H227.555556V303.6159996666667z"></path></symbol>
</svg>
<script type="text/javascript">
    function onGotoChapSuccess(context) {
        if (context.success) {
            window.location = context.url;
        } else {
            showAlert("Chapter not found", "The chapter of the novel you entered was not found. Please try a different chapter number.");
        }
    }
</script>
<div class="filters">
    <form method="post" id="gotochap" data-ajax="true" data-ajax-method="post" data-ajax-success="onGotoChapSuccess" action="/novel/gotochap">
        <input name="str" type="hidden" value="a-depressed-kendo-player-possesses-a-bastard-aristocrat">
        <input id="gotochapno" name="chapno" type="number" placeholder="Enter Chapter No">
        <input class="button" type="submit" value="Go">
    <input name="__LNRequestVerifyToken" type="hidden" value="CfDJ8Cw-T2dg-T1Pq5cE4g3E-KFNLmQFueiEJuIRyfzsScanyri_wRAT-7VKdVy9Ap5ERSDPJKYRl3K7QVLKHuI3pZkGeBjXQVXZVJEnDXBLhpXTNWPA1Hoc5I35lhT41_TwIa4qfYnZBOwq8DiInt9r9Ec"></form>
    <div class="pagenav">
        <div class="pagination-container"><ul class="pagination"><li class="PagedList-skipToPrevious"><a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat/chapters?page=2" rel="prev">&lt;</a></li><li><a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat/chapters">1</a></li><li><a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat/chapters?page=2">2</a></li><li class="active"><span>3</span></li></ul></div>
        <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat/chapters?page=3&amp;chorder=desc">
            <i class="chorder fas asc " data-order="asc">
                <svg><use xlink:href="#i-rank-up"></use></svg>
            </i>
        </a>
    </div>
</div>

<ul class="chapter-list">
        <li data-chapterno="117.1" data-volumeno="0" data-orderno="201">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-117-1" title="Chapter 117.1: Severed Yesterday Part 1">
                <span class="chapter-no ">117.1</span>
                <strong class="chapter-title">
Severed Yesterday Part 1                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="117.2" data-volumeno="0" data-orderno="202">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-117-2" title="Chapter 117.2: Severed Yesterday Part 2">
                <span class="chapter-no ">117.2</span>
                <strong class="chapter-title">
Severed Yesterday Part 2                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="118.1" data-volumeno="0" data-orderno="203">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-118-1" title="Chapter 118.1: You are filled with hatred. Part 1">
                <span class="chapter-no ">118.1</span>
                <strong class="chapter-title">
You are filled with hatred. Part 1                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="118.2" data-volumeno="0" data-orderno="204">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-118-2" title="Chapter 118.2: You are filled with hatred. Part 2">
                <span class="chapter-no ">118.2</span>
                <strong class="chapter-title">
You are filled with hatred. Part 2                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="119" data-volumeno="0" data-orderno="205">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-119" title="Chapter 119: A Kind Soul">
                <span class="chapter-no ">119</span>
                <strong class="chapter-title">
A Kind Soul                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="120.1" data-volumeno="0" data-orderno="206">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-120-1" title="Chapter 120.1: Three months. Part 1">
                <span class="chapter-no ">120.1</span>
                <strong class="chapter-title">
Three months. Part 1                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="120.2" data-volumeno="0" data-orderno="207">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-120-2" title="Chapter 120.2: Three months. Part 2">
                <span class="chapter-no ">120.2</span>
                <strong class="chapter-title">
Three months. Part 2                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="121.1" data-volumeno="0" data-orderno="208">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-121-1" title="Chapter 121.1: I'm home Part 1">
                <span class="chapter-no ">121.1</span>
                <strong class="chapter-title">
I'm home Part 1                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="121.2" data-volumeno="0" data-orderno="209">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-121-2" title="Chapter 121.2: I'm home Part 2">
                <span class="chapter-no ">121.2</span>
                <strong class="chapter-title">
I'm home Part 2                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="122" data-volumeno="0" data-orderno="210">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-122" title="Chapter 122: Side Story - How the Golden Boy Spends His Winter Vacation">
                <span class="chapter-no ">122</span>
                <strong class="chapter-title">
Side Story - How the Golden Boy Spends His Winter Vacation                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="123.1" data-volumeno="0" data-orderno="211">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-123-1" title="Chapter 123.1: Side Story - Beyond the Nightmare Part 1">
                <span class="chapter-no ">123.1</span>
                <strong class="chapter-title">
Side Story - Beyond the Nightmare Part 1                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="123.2" data-volumeno="0" data-orderno="212">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-123-2" title="Chapter 123.2: Side Story - Beyond the Nightmare Part 2">
                <span class="chapter-no ">123.2</span>
                <strong class="chapter-title">
Side Story - Beyond the Nightmare Part 2                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="124" data-volumeno="0" data-orderno="213">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-124" title="Chapter 124: Side Story - If I Could Meet You Even in My Nightmares">
                <span class="chapter-no ">124</span>
                <strong class="chapter-title">
Side Story - If I Could Meet You Even in My Nightmares                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="125" data-volumeno="0" data-orderno="214">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-125" title="Chapter 125: Side Story - What It Means to Be at Peace">
                <span class="chapter-no ">125</span>
                <strong class="chapter-title">
Side Story - What It Means to Be at Peace                </strong>
                <time class="chapter-update" datetime="2024-08-16 02:51">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="126.1" data-volumeno="0" data-orderno="215">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-126-1" title="Chapter 126.1: The Start of a New Semester Part 1">
                <span class="chapter-no ">126.1</span>
                <strong class="chapter-title">
The Start of a New Semester Part 1                </strong>
                <time class="chapter-update" datetime="2024-08-22 10:31">
                    5 months ago
                </time>
            </a>
        </li>
        <li data-chapterno="126.2" data-volumeno="0" data-orderno="216">
            <a href="/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat-1695/chapter-126-2" title="Chapter 126.2: The Start of a New Semester Part 2">
                <span class="chapter-no ">126.2</span>
                <strong class="chapter-title">
The Start of a New Semester Part 2                </strong>
                <time class="chapter-update" datetime="2024-08-22 10:31">
                    5 months ago
                </time>
            </a>
        </li>
</ul>


    </section>
</article>




    </main><iframe name="__tcfapiLocator" style="display: none;"></iframe><iframe name="__gppLocator" style="display: none;"></iframe><iframe marginwidth="0" marginheight="0" scrolling="no" frameborder="0" id="14333faa1ce03a" width="0" height="0" src="about:blank" name="__pb_locator__" style="display: none; height: 0px; width: 0px; border: 0px;"></iframe><div style="position: fixed; pointer-events: none; inset: 110px 906.5px 0px 0px; overflow: hidden; text-align: center; z-index: 1000; display: none;"><div style="position: relative; display: inline-block; top: 0px;"><div style="pointer-events: auto; font-size: 0px; line-height: 0;"></div></div></div><div style="position: fixed; pointer-events: none; inset: 110px 0px 0px 906.5px; overflow: hidden; text-align: center; z-index: 1000; display: none;"><div style="position: relative; display: inline-block; top: 0px;"><div style="pointer-events: auto; font-size: 0px; line-height: 0;"></div></div></div>
    <footer translate="no">
        <div class="wrapper">
            <div class="col logo">
                <a href="/hub_10102358" style="display:inline-block" title="Read Most Popular Light Novels Online for Free | Light Novel World">
                    <img class="footer-logo" src="https://static.lightnovelworld.com/content/img/lightnovelworld/logo-xl-dark.png" alt="logo-footer">
                </a>
                <span class="copyright">© 2025 lightnovelworld.com</span>
            </div>
            <nav class="col links">
                <ul>
                    <li>
                        <a title="Explore the Top Rated Novels" href="/ranking-10102358">Novel Ranking</a>
                    </li>
                    <li>
                        <a title="Recently Added Light Novel Chapters" href="/latest-updates-10102358">Latest Chapters</a>
                    </li>
                    <li>
                        <a title="Recently Added Light Novels" href="/browse/genre-all-25060123/order-new/status-all">Latest Novels</a>
                    </li>
                    <li>
                        <a title="Explore All Novel Tags" href="/tag/all">All Tags</a>
                    </li>
                </ul>
            </nav>
            <nav class="col links">
                <ul>
                    <li>
                        <a title="Most Popular Romance Genre Novels" href="/browse/genre-romance-04061342/order-popular/status-all">Romance</a>
                    </li>
                    <li>
                        <a title="Most Popular Josei Genre Novels" href="/browse/genre-josei-04061342/order-popular/status-all">Josei for Ladies</a>
                    </li>
                    <li>
                        <a title="Most Popular Video Games Genre Novels" href="/browse/genre-video-games-04061342/order-popular/status-all">Video Games</a>
                    </li>
                    <li>
                        <a title="Most Popular Fantasy Genre Novels" href="/browse/genre-fantasy-04061342/order-popular/status-all">Fantasy</a>
                    </li>
                </ul>
            </nav>
            <nav class="col links">
                <ul>
                    <li>
                        <a title="Most Popular Martial Arts Genre Novels" href="/browse/genre-martial-arts-10032131/order-popular/status-all">Martial Arts</a>
                    </li>
                    <li>
                        <a title="Most Popular Slice of Life Genre Novels" href="/browse/genre-slice-of-life-04061342/order-popular/status-all">Slice of Life</a>
                    </li>
                    <li>
                        <a title="Most Popular Sci-fi Genre Novels" href="/browse/genre-sci-fi-04061342/order-popular/status-all">Sci-fi</a>
                    </li>
                    <li>
                        <a title="Most Popular Supernatural Genre Novels" href="/browse/genre-supernatural-10032131/order-popular/status-all">Supernatural</a>
                    </li>
                </ul>
            </nav>
            <nav class="col links">
                <ul>
                    <li>
                        <a href="/privacy-policy">Privacy Policy</a>
                    </li>
                    <li>
                        <a href="/terms-of-service">Terms of Service</a>
                    </li>
                    <li>
                        <a href="/dmca">DMCA Notices</a>
                    </li>
                    <li>
                        <a href="mailto:info@lightnovelworld.com">Contact Us</a>
                    </li>
                </ul>
            </nav>
        </div>
    </footer>

        <script src="https://cdnjs.cloudflare.com/ajax/libs/js-cookie/3.0.5/js.cookie.min.js" integrity="sha512-nlp9/l96/EpjYBx7EP7pGASVXNe80hGhYAUrjeXnu/fyF5Py0/RXav4BBNs7n5Hx1WFhOEOWSAVjGeC3oKxDVQ==" crossorigin="anonymous" referrerpolicy="no-referrer"></script>
        <script src="https://cdnjs.cloudflare.com/ajax/libs/jquery/3.7.0/jquery.min.js" integrity="sha512-3gJwYpMe3QewGELv8k/BX9vcqhryRdzRMxVfq6ngyWXwo03GFEzjsUm8Q7RZcHPHksttq7/GFoxjCVUjkjvPdw==" crossorigin="anonymous" referrerpolicy="no-referrer"></script>
        <script src="https://cdnjs.cloudflare.com/ajax/libs/jquery-ajax-unobtrusive/3.2.6/jquery.unobtrusive-ajax.min.js" integrity="sha512-DedNBWPF0hLGUPNbCYfj8qjlEnNE92Fqn7xd3Sscfu7ipy7Zu33unHdugqRD3c4Vj7/yLv+slqZhMls/4Oc7Zg==" crossorigin="anonymous" referrerpolicy="no-referrer"></script>
        <script src="https://static.lightnovelworld.com/lib/sticky-kit/sticky-kit.js?v=31051253"></script>
        <script src="https://static.lightnovelworld.com/lib/store.js/store.everything.min.js?v=31051253"></script>
        <script src="https://static.lightnovelworld.com/content/js/commons.min.js?v=31051253"></script>
        <script src="https://static.lightnovelworld.com/content/js/appsettings.min.js?v=31051253"></script><style id="font_range_slider_style">.range-fontsize .range {background: linear-gradient(to right, var(--anchor-color) 0%, var(--anchor-color) 14.285714285714286%, transparent 14.285714285714286%, transparent 100%)}.range-fontsize .range input::-webkit-slider-runnable-track{background: linear-gradient(to right, var(--anchor-color) 0%, var(--anchor-color) 14.285714285714286%, #b2b2b2 14.285714285714286%, #b2b2b2 100%)}.range-fontsize .range {background: linear-gradient(to right, var(--anchor-color) 0%, var(--anchor-color) 14.285714285714286%, transparent 14.285714285714286%, transparent 100%)}.range-fontsize .range input::-moz-range-track{background: linear-gradient(to right, var(--anchor-color) 0%, var(--anchor-color) 14.285714285714286%, #b2b2b2 14.285714285714286%, #b2b2b2 100%)}.range-fontsize .range {background: linear-gradient(to right, var(--anchor-color) 0%, var(--anchor-color) 14.285714285714286%, transparent 14.285714285714286%, transparent 100%)}.range-fontsize .range input::-ms-track{background: linear-gradient(to right, var(--anchor-color) 0%, var(--anchor-color) 14.285714285714286%, #b2b2b2 14.285714285714286%, #b2b2b2 100%)}</style>
        <script src="https://cdnjs.cloudflare.com/ajax/libs/lazysizes/5.3.2/lazysizes.min.js" integrity="sha512-q583ppKrCRc7N5O0n2nzUiJ+suUv7Et1JGels4bXOaMFQcamPk9HjdUknZuuFjBNs7tsMuadge5k9RzdmO+1GQ==" crossorigin="anonymous" referrerpolicy="no-referrer"></script>
        <script async="" src="https://static.lightnovelworld.com/content/js/app.min.js?v=31051253"></script>


<!--sse--><!--/sse-->
    <script src="https://static.lightnovelworld.com/content/js/jquery.smartbanner.js?v=31051253"></script>
    <script type="text/javascript">
        $(function () {
            $.mobileappbanner({});
        });
    </script>

    <script type="application/ld+json">
        {
            "@context": {
                "rdfa": "http://www.w3.org/ns/rdfa#",
                "schema": "http://schema.org/"
            },
            "@graph": [
                {
                    "@id": "https://www.lightnovelworld.com/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat",
                    "@type": "schema:Book",
                    "schema:name": "A Depressed Kendo Player Possesses a Bastard Aristocrat"
                },
                {
                    "@type":"schema:BreadcrumbList",
                    "schema:itemListElement":[
                    {
                        "@type":"schema:ListItem",
                        "schema:position":1,
                        "schema:name":"Light Novel World",
                        "schema:item":"https://www.lightnovelworld.com"
                    },
                    {
                        "@type":"schema:ListItem",
                        "schema:position":2,
                        "schema:name":"A Depressed Kendo Player Possesses a Bastard Aristocrat | Light Novel World",
                        "schema:item":"https://www.lightnovelworld.com/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat"
                    },
                    {
                        "@type":"schema:ListItem",
                        "schema:position":3,
                        "schema:name":"A Depressed Kendo Player Possesses a Bastard Aristocrat Novel Chapters | Light Novel World",
                        "schema:item":"https://www.lightnovelworld.com/novel/a-depressed-kendo-player-possesses-a-bastard-aristocrat/chapters?page=3"
                    }]
                }]
            }
    </script>

<span style="display: none; position: fixed; left: 0px; bottom: 0px; width: 100%; min-width: 100%; backdrop-filter: none; background-color: rgba(255, 255, 255, 0.83); z-index: 2147483646; text-align: center; font-size: 0px; line-height: 0; pointer-events: none; overflow: clip; overflow-clip-margin: content-box;"><span style="display: grid; grid-template-columns: minmax(0px, 1fr) minmax(0px, 150px) 1fr minmax(20px, 150px) minmax(0px, 1fr);"></span></span><span style="position: fixed; display: none; right: 5px; bottom: 100px; width: 30vw; min-width: 200px; max-width: 200px; aspect-ratio: 16 / 9; z-index: 2147483647; box-shadow: rgba(0, 0, 0, 0.22) 2px 1px 3px 1px;"><span style=""><span style="pointer-events: auto; display: block; width: 100%; height: 100%; max-width: none; position: static;"><iframe frameborder="0" marginwidth="0" marginheight="0" scrolling="no" webkitallowfullscreen="true" mozallowfullscreen="true" border="0" allowtransparency="true" allow="geolocation; microphone; camera; autoplay; fullscreen; payment; accelerometer; ; display-capture; gyroscope; magnetometer; midi; ch-ua-platform-version; ch-ua-model" style="border: 0px; width: 100%; height: 100%; max-width: none; position: static;"></iframe></span></span><div style="position: absolute; z-index: 1; display: flex; justify-content: center; align-items: center; color: rgb(0, 0, 0); right: 0px; top: -15px; width: 15px; height: 15px; box-shadow: rgba(0, 0, 0, 0.22) 1px 1px 5px 1px; outline: none; user-select: none; cursor: pointer; pointer-events: auto; border-radius: 0px; background-color: rgba(255, 255, 255, 0.8); border: 1px solid rgb(0, 0, 0);">×</div></span></body><iframe sandbox="allow-scripts allow-same-origin" id="84dbc274384d57d" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://ads.yieldmo.com/pbcas?us_privacy=&amp;gdpr=0&amp;gdpr_consent=&amp;type=iframe">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="85298ef9531ae2" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://visitor.omnitagjs.com/visitor/isync?uid=19340f4f097d16f41f34fc0274981ca4&amp;gdpr=0&amp;gdpr_consent=">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="86963828be19bcd" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://eus.rubiconproject.com/usync.html?gdpr=0">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="87d720671af7d39" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://acdn.adnxs.com/dmp/async_usersync.html">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="8879a9f6f582b3e" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://u.4dex.io/usync.html?it=adg-pb-clt&amp;lang=en&amp;publisher_id=1090&amp;website_name=lightnovelworld-com">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="899053e31467bea" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://js-sec.indexww.com/um/ixmatch.html">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="906fa8bc142bcbd" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://ads.pubmatic.com/AdServer/js/user_sync.html?kdntuid=1&amp;p=159234&amp;gdpr=0&amp;gdpr_consent=">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="912422ab86a9e45" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://elb.the-ozone-project.com/static/load-cookie.html?gdpr=0&amp;gdpr_consent=&amp;usp_consent=&amp;gpp=&amp;gpp_sid=&amp;criteo.com=FmO8ql84NllnTGVPMWhQc3JMYVViN3ElMkZ6Tm1jdUxEOVZHYWtiVlV6cno4OUpwNkFnNTAwcUpIeW51Zmd3aElxSmtISW5YJTJGODhSaFM0MENNWmhsalEwUUFwUlhYTEY0Wk9RdWFmZWowYXF0JTJGa0VOTUVJRmhTYnFhelo1ZnI5bG5FMUNkYg&amp;audigent.com=0602tj2g5dbae9ldd8je9bik7hiccc9ifeesgwsqykgem60kk4wm6guy2suiii6uo&amp;id5-sync.com=ID5*TdaiFddr3gcl4Ct1V5Rfcg03Kncy7ZIoaBteh1EOh0j1ucDKLIsJniwWKkY7EZLE&amp;pubcid.org=f40587f7-7ecf-44cb-a630-778f7df3a414&amp;adserver.org=0ccbcb4e-3d30-4de0-88c4-2d785a0fd9d5&amp;publisherId=OZONEVEN0005&amp;siteId=3500000566&amp;cb=1738789026138&amp;bidder=ozone">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="927eb6534a72872" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://prebid.a-mo.net/isyn?gdpr_consent=&amp;gdpr=0&amp;us_privacy=&amp;gpp=&amp;gpp_sid=">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="9305b67da44f56b" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://eb2.3lift.com/sync?">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="945c3426ff075f3" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://sdk.streamrail.com/cs-config/cs.html?org=5fa94677b2db6a00015b22a9&amp;tc=5fcca73e13fd9b000100aa2e&amp;as=5fcca73e13fd9b000100aa30&amp;type=hb&amp;wd=cs.yellowblue.io&amp;domain=lightnovelworld.com&amp;us_privacy=1NNN">
</iframe><iframe sandbox="allow-scripts allow-same-origin" id="95ddfbff0e2c70f" frameborder="0" allowtransparency="true" marginheight="0" marginwidth="0" width="0" hspace="0" vspace="0" height="0" style="height:0px;width:0px;display:none;" scrolling="no" src="https://gum.criteo.com/syncframe?origin=criteoPrebidAdapter&amp;topUrl=www.lightnovelworld.com&amp;gpp=#{%22bundle%22:%220tphrF8lMkZpWEJBSFJ2a0pSM0VpU0RHJTJCaUtWUXVwOEM4TFV2NHNaZW91dVNoTEd2d0hBVjdOVkt5Yk4wOGFaeWRSYnJXelZ0UnlhNTZ6MyUyRmNJUFBySVE2JTJCSVl2ZzZaVnJZM2puSHpjMkxmRHI1VSUyQkFkSGMzNkd1YktUTDB2TVlwNnhzYTlpUXp2akpyeEpFNSUyQktwakJHMmUxMSUyQnhKb2JqM09Rc05HSExHMEI2V3JncyUzRA%22,%22cw%22:true,%22lsw%22:true,%22origin%22:%22criteoPrebidAdapter%22,%22requestId%22:%220.7857831549558945%22,%22tld%22:%22www.lightnovelworld.com%22,%22topUrl%22:%22www.lightnovelworld.com%22,%22version%22:%229_29_0%22}">
</iframe></html>)
#### Thoughts on using [Puppeteer-sharp-extra](https://github.com/Overmiind/Puppeteer-sharp-extra?tab=readme-ov-file) if things do not work still.
### Somehow people keep staring this project.
Every couple of months, I see a new person star this project, which is quite interesting as, it might mean that someone is using it. I really want to know which sites are used most, is it mangas or webnovels?