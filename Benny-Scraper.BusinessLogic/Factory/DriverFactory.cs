using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Collections.Concurrent;

namespace Benny_Scraper.BusinessLogic.Factory
{
    public enum Browser
    {
        Chrome,
        Firefox,
        Edge,
        IE
    }

    public class DriverFactory : IDriverFactory
    {
        private readonly ConcurrentDictionary<int, IWebDriver> _drivers; // thread-safe version of the dictionary, no need to worry about multiple threads making changes
        private int _counter;

        public DriverFactory()
        {
            _drivers = new ConcurrentDictionary<int, IWebDriver>();
            _counter = 0;
        }

        /// <summary>
        /// Creates a driver and adds each driver to a dictonry of <int, IWebDriver><
        /// </summary>
        /// <param name="browser">a positive integer</param>
        /// <param name="isHeadless"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public IWebDriver CreateDriver(string url, int browser, bool isHeadless)
        {
            switch (browser)
            {
                case (int)Browser.Chrome:
                    var chromeDriverService = ChromeDriverService.CreateDefaultService(); // needs to be first in order to have the driver ready when called asycnhronously
                    chromeDriverService.HideCommandPromptWindow = true; // hides command prompt window https://stackoverflow.com/questions/53218843/stop-chromedriver-console-window-from-appearing-selenium-c-sharp
                    var chromeOptions = new ChromeOptions();
                    chromeOptions.AddArguments("--no-sandbox", "--disable-web-security", "--disable-gpu", "--hide-scrollbars", "window-size=1920,1080");

                    if (isHeadless)
                        chromeOptions.AddArgument("headless");

                    IWebDriver driver;
                    try
                    {
                        driver = new ChromeDriver(chromeDriverService, chromeOptions);
                    }
                    catch (WebDriverException ex) when (ex.Message.Contains("ChromeDriver only supports Chrome version"))
                    {
                        throw new InvalidOperationException(
                            "Chrome browser version mismatch detected.\n" +
                            "Please update Google Chrome to the latest version:\n" +
                            "  1. Open Chrome\n" +
                            "  2. Click the menu (three dots) → Help → About Google Chrome\n" +
                            "  3. Chrome will automatically update\n" +
                            "  4. Restart Chrome, then run this application again.\n\n" +
                            "The application will now exit.", ex);
                    }

                    driver.Url = url;
                    _drivers[_counter] = driver;
                    _counter++;
                    return driver;


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
        public async Task<IWebDriver> CreateDriverAsync(string url, int browser = 0, bool isHeadless = false)
        {
            switch (browser)
            {
                case (int)Browser.Chrome:
                    var chromeDriverService = ChromeDriverService.CreateDefaultService();
                    chromeDriverService.HideCommandPromptWindow = true;
                    var chromeOptions = new ChromeOptions();
                    chromeOptions.AddArguments("--no-sandbox", "--disable-web-security", "--disable-gpu", "--hide-scrollbars", "window-size=1920,1080");

                    if (isHeadless)
                        chromeOptions.AddArgument("headless");

                    IWebDriver driver;
                    try
                    {
                        driver = await Task.Run(() => new ChromeDriver(chromeDriverService, chromeOptions));
                    }
                    catch (WebDriverException ex) when (ex.Message.Contains("ChromeDriver only supports Chrome version"))
                    {
                        throw new InvalidOperationException(
                            "Chrome browser version mismatch detected.\n" +
                            "Please update Google Chrome to the latest version:\n" +
                            "  1. Open Chrome\n" +
                            "  2. Click the menu (three dots) → Help → About Google Chrome\n" +
                            "  3. Chrome will automatically update\n" +
                            "  4. Restart Chrome, then run this application again.\n\n" +
                            "The application will now exit.", ex);
                    }

                    driver.Url = url;

                    int id = Interlocked.Increment(ref _counter);
                    _drivers.TryAdd(id, driver);

                    return driver;

                default:
                    throw new ArgumentException($"{browser} is not a valid value.");
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
}
