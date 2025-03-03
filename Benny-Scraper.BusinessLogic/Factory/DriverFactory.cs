using System.Collections.Concurrent;
using HtmlAgilityPack;
using NLog;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using WebDriverManager.DriverConfigs.Impl;

namespace Benny_Scraper.BusinessLogic.Factory;
public enum Broswer
{
    Chrome,
    Firefox,
    Edge,
    IE
}

public record DriverObj
{
    public int Id { get; init; }
    public IWebDriver Driver { get; init; }
}

public interface IDriverFactory
{
    /// <summary>
    /// Creates a driver and adds each driver to a dictonry of <int, IWebDriver><
    /// </summary>
    /// <param name="browser">a positive integer</param>
    /// <param name="isHeadless"></param>
    /// <param name="url"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    DriverObj CreateDriver(string url, int browser = 0, bool isHeadless = false);

    Task<DriverObj> CreateDriverAsync(string url, int browser = 0, bool isHeadless = false);
    IWebDriver GetDriverById(int id);
    HtmlDocument GetPageContent(int driverId);

    void GoToAndWait<T>(int driverId, string url, Func<IWebDriver, T> conditionFunc,
        int waitSeconds = 60);

    /// <summary>
    /// Gets a driver using an id
    /// </summary>
    /// <param name="id">a positive integer</param>
    /// <returns></returns>
    void DisposeDriverById(int id);

    /// <summary>
    /// Gets dictionary that contains all drivers instances created. See <see cref="DisposeDriverById(int)"/>
    /// </summary>
    /// <returns></returns>
    ConcurrentDictionary<int, IWebDriver> GetAllDrivers();

    /// <summary>
    /// Deletes all drivers
    /// </summary>
    void DisposeAllDrivers();
}

public class DriverFactory : IDriverFactory
{
    private ConcurrentDictionary<int, IWebDriver> _drivers = new(); // thread-safe version of the dictionary, no need to worry about multiple threads making changes
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private int _counter = 0;

    /// <summary>
    /// Creates a driver and adds each driver to a dictonry of <int, IWebDriver><
    /// </summary>
    /// <param name="browser">a positive integer</param>
    /// <param name="isHeadless"></param>
    /// <param name="url"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public DriverObj CreateDriver(string url, int browser, bool isHeadless)
    {
        switch (browser)
        {
            case (int)Broswer.Chrome:
                var chromeDriverService = ChromeDriverService.CreateDefaultService(); // needs to be first in order to have the driver ready when called asycnhronously
                chromeDriverService.HideCommandPromptWindow = true; // hides command prompt window https://stackoverflow.com/questions/53218843/stop-chromedriver-console-window-from-appearing-selenium-c-sharp
                new WebDriverManager.DriverManager().SetUpDriver(new ChromeConfig()); // should install a new chromedriver if there is an update
                var chromeOptions = new ChromeOptions();
                chromeOptions.AddArguments("--no-sandbox", "--disable-web-security", "--disable-gpu", "--hide-scrollbars", "window-size=1920,1080");

                if (isHeadless)
                    chromeOptions.AddArgument("headless");
                IWebDriver driver = new ChromeDriver(chromeDriverService, chromeOptions);
                driver.Url = url;
                _drivers[_counter] = driver;
                _counter++;
                return new DriverObj { Id = _counter, Driver = driver };

            default:
                // throwing resolves error since everything needs to return the correct type
                throw new ArgumentException($"{browser} is not a valid value.");
        }
    }

    /// <summary>
    /// Creates drivers and add them to a threadsafe ConcurrentDictionary that contains all drivers
    /// </summary>
    /// <param name="browser"></param>
    /// <param name="isHeadless"></param>
    /// <param name="url"></param>
    /// <returns></returns>
    public async Task<DriverObj> CreateDriverAsync(string url, int browser, bool isHeadless)
    {
        switch (browser)
        {
            case (int)Broswer.Chrome:
                var chromeDriverService = ChromeDriverService.CreateDefaultService();
                chromeDriverService.HideCommandPromptWindow = true;
                new WebDriverManager.DriverManager().SetUpDriver(new ChromeConfig());
                var chromeOptions = new ChromeOptions();
                chromeOptions.AddArguments("--no-sandbox", "--disable-web-security", "--disable-gpu", "--hide-scrollbars", "window-size=1920,1080");

                if (isHeadless)
                    chromeOptions.AddArgument("headless");

                // waits for driver to be created to prevent creating multiple on the same instance
                IWebDriver driver = await Task.Run(() => new ChromeDriver(chromeDriverService, chromeOptions));
                driver.Url = url;

                int id = Interlocked.Increment(ref _counter); // increment the counter in a thread-safe way atomatically
                _drivers.TryAdd(id, driver); // thread safe way off adding to ConcurrentDictionary

                return new DriverObj() { Id = _counter, Driver = driver };

            default:
                throw new ArgumentException($"{browser} is not a valid value.");
        }
    }

    public void GoToAndWait<T>(int driverId, string url, Func<IWebDriver, T> conditionFunc, int waitSeconds = 60)
    {
        var driver = GetDriverById(driverId);
        try
        {
            driver.Navigate().GoToUrl(url);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(waitSeconds));
            wait.Until(conditionFunc);
        }
        catch (WebDriverTimeoutException ex)
        {
            _logger.Error($"Error when waiting for element to appear: {ex}");
            throw new WebDriverTimeoutException($"Error when waiting for element to appear: {ex}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error when waiting for element to appear: {ex}");
            throw new Exception($"Error when waiting for element to appear: {ex}");
        }
    }

    /// <summary>
    /// Gets the page source and returns it as an HtmlDocument.
    /// </summary>
    /// <param name="driver"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="Exception"></exception>
    public HtmlDocument GetPageContent(int driverId)
    {
        var driver = GetDriverById(driverId);

        try
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(driver.PageSource);
            if (htmlDocument.DocumentNode is null || htmlDocument is null)
                throw new NullReferenceException($"Document is null. Possibly a navigation or rate-limit -error");
            return htmlDocument;
        }
        catch (Exception ex)
        {
            _logger.Fatal($"Error while getting page content for {driver.Url}: {ex}");
            throw new Exception($"Error while getting page content for {driver.Url}", ex);
        }
    }

    /// <summary>
    /// Gets a driver using an id
    /// </summary>
    /// <param name="id">a positive integer</param>
    /// <returns></returns>
    public IWebDriver GetDriverById(int id)
    {
        return _drivers[id];
    }

    /// <summary>
    /// Gets dictionary that contains all drivers instances created. See <see cref="DisposeDriverById(int)"/>
    /// </summary>
    /// <returns></returns>
    public ConcurrentDictionary<int, IWebDriver> GetAllDrivers()
    {
        return _drivers;
    }

    public void DisposeDriverById(int id)
    {
        var driver = _drivers[id];
        driver.Dispose();
        driver.Quit();
        _drivers.TryRemove(id, out var value);
    }

    public void DisposeAllDrivers()
    {
        foreach (IWebDriver driver in _drivers.Values)
        {
            driver.Dispose(); // Clears up unmanaged resources.
            driver.Quit(); // will also call Dispose
        }

        _drivers.Clear();
    }
}
