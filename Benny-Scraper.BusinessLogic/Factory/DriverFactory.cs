using System.Collections.Concurrent;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace BennyScraper.BusinessLogic.Factory;

internal enum Browser
{
    Chrome
}

internal sealed class DriverFactory : IDriverFactory
{
    private readonly ConcurrentDictionary<int, IWebDriver> _drivers = new(); // thread-safe version of the dictionary, no need to worry about multiple threads making changes
    private int _counter;

    /// <summary>
    /// Creates a Chrome WebDriver instance, navigates it to the given URL, registers it in the internal driver
    /// dictionary keyed by an auto-incrementing id, and returns it.
    /// </summary>
    /// <param name="url">The URL the driver should navigate to immediately after creation.</param>
    /// <param name="browser">The browser to launch, as the underlying integer value of the <see cref="Browser"/> enum. Only <see cref="Browser.Chrome"/> (0) is currently supported.</param>
    /// <param name="isHeadless">true to run Chrome without a visible window; otherwise, false.</param>
    /// <returns>The created <see cref="IWebDriver"/> instance.</returns>
    /// <exception cref="ArgumentException">The browser parameter must be 0 (Chrome).</exception>
    public IWebDriver CreateDriver(string url, int browser, bool isHeadless)
    {
        switch (browser)
        {
            case (int)Browser.Chrome:
                ChromeDriverService? chromeDriverService = null;
                ChromeDriver driver;
                try
                {
                    chromeDriverService = ChromeDriverService.CreateDefaultService(); // needs to be first in order to have the driver ready when called asycnhronously
                    chromeDriverService.HideCommandPromptWindow = true; // hides command prompt window https://stackoverflow.com/questions/53218843/stop-chromedriver-console-window-from-appearing-selenium-c-sharp
                    var chromeOptions = new ChromeOptions();
                    chromeOptions.AddArguments("--no-sandbox", "--disable-web-security", "--disable-gpu", "--hide-scrollbars", "window-size=1920,1080");

                    if (isHeadless)
                    {
                        chromeOptions.AddArgument("headless");
                    }

                    try
                    {
                        driver = new ChromeDriver(chromeDriverService, chromeOptions);
                    }
                    catch (WebDriverException ex) when (ex.Message.Contains("ChromeDriver only supports Chrome version", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Chrome browser version mismatch detected.\n" +
                            "Please update Google Chrome to the latest version:\n" +
                            "  1. Open Chrome\n" +
                            "  2. Click the menu (three dots) → Help → About Google Chrome\n" +
                            "  3. Chrome will automatically update\n" +
                            "  4. Restart Chrome, then run this application again.\n\n" +
                            "The application will now exit.",
                            ex);
                    }

                    chromeDriverService = null; // Ownership transferred to driver; ChromeDriver disposes its service internally.
                }
                finally
                {
                    chromeDriverService?.Dispose();
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
    /// Creates a Chrome WebDriver instance asynchronously, navigates it to the given URL, and registers it in the
    /// thread-safe <see cref="ConcurrentDictionary{TKey, TValue}"/> that contains all drivers.
    /// </summary>
    /// <param name="url">The URL the driver should navigate to immediately after creation.</param>
    /// <param name="browser">The browser to launch, as the underlying integer value of the <see cref="Browser"/> enum. Only <see cref="Browser.Chrome"/> (0) is currently supported.</param>
    /// <param name="isHeadless">true to run Chrome without a visible window; otherwise, false.</param>
    /// <returns>A task that resolves to the created <see cref="IWebDriver"/> instance.</returns>
    public async Task<IWebDriver> CreateDriverAsync(string url, int browser = 0, bool isHeadless = false)
    {
        switch (browser)
        {
            case (int)Browser.Chrome:
                ChromeDriverService? chromeDriverService = null;
                ChromeDriver driver;
                try
                {
                    chromeDriverService = ChromeDriverService.CreateDefaultService();
                    chromeDriverService.HideCommandPromptWindow = true;
                    var chromeOptions = new ChromeOptions();
                    chromeOptions.AddArguments("--no-sandbox", "--disable-web-security", "--disable-gpu", "--hide-scrollbars", "window-size=1920,1080");

                    if (isHeadless)
                    {
                        chromeOptions.AddArgument("headless");
                    }

                    try
                    {
                        driver = await Task.Run(() => new ChromeDriver(chromeDriverService, chromeOptions)).ConfigureAwait(false);
                    }
                    catch (WebDriverException ex) when (ex.Message.Contains("ChromeDriver only supports Chrome version", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Chrome browser version mismatch detected.\n" +
                            "Please update Google Chrome to the latest version:\n" +
                            "  1. Open Chrome\n" +
                            "  2. Click the menu (three dots) → Help → About Google Chrome\n" +
                            "  3. Chrome will automatically update\n" +
                            "  4. Restart Chrome, then run this application again.\n\n" +
                            "The application will now exit.",
                            ex);
                    }

                    chromeDriverService = null; // Ownership transferred to driver; ChromeDriver disposes its service internally.
                }
                finally
                {
                    if (chromeDriverService != null)
                    {
                        await chromeDriverService.DisposeAsync().ConfigureAwait(false);
                    }
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
    /// Gets a driver using an id.
    /// </summary>
    /// <param name="id">a positive integer.</param>
    /// <returns>The <see cref="IWebDriver"/> instance registered under the given id.</returns>
    public IWebDriver GetDriverById(int id) => _drivers[id];

    /// <summary>
    /// Gets dictionary that contains all drivers instances created. See <see cref="DisposeDriverById(int)"/>.
    /// </summary>
    /// <returns>The <see cref="ConcurrentDictionary{TKey, TValue}"/> of all currently tracked driver instances, keyed by id.</returns>
    public ConcurrentDictionary<int, IWebDriver> GetAllDrivers() => _drivers;

    public void DisposeDriverById(int id)
    {
        var driver = _drivers[id];
        driver.Quit();
        driver.Dispose();
        _drivers.TryRemove(id, out _);
    }

    public void DisposeAllDrivers()
    {
        foreach (var driver in _drivers.Values)
        {
            try
            {
                driver.Quit();
                driver.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        _drivers.Clear();
    }
}