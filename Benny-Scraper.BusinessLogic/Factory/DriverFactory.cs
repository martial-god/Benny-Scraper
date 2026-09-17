using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;

namespace BennyScraper.BusinessLogic.Factory;

internal enum Browser
{
    Chrome = 1,
    Firefox = 0,
    Edge = 2,
}

internal sealed class DriverFactory : IDriverFactory
{
    private readonly ConcurrentDictionary<int, IWebDriver> _drivers = new(); // thread-safe version of the dictionary, no need to worry about multiple threads making changes
    private int _counter;

    /// <summary>
    /// Creates a WebDriver instance, navigates it to the given URL, registers it in the internal driver
    /// dictionary keyed by an auto-incrementing id, and returns it.
    /// </summary>
    /// <param name="url">The URL the driver should navigate to immediately after creation.</param>
    /// <param name="browser">The browser to launch, as the underlying integer value of the <see cref="Browser"/> enum.</param>
    /// <param name="isHeadless">true to run the browser without a visible window; otherwise, false.</param>
    /// <returns>The created <see cref="IWebDriver"/> instance.</returns>
    /// <exception cref="ArgumentException">The specified browser is not supported.</exception>
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

                var driverId = Interlocked.Increment(ref _counter);
                var chromeProcessId = GetChromeProcessId(driver);
                _drivers[driverId] = driver;
                var chromeVersion = driver.Capabilities.GetCapability("browserVersion")?.ToString();

                if (driver.Capabilities.GetCapability("chrome") is Dictionary<string, object> chromeOptionss)
                {
                    if (chromeOptionss.TryGetValue("chromedriverVersion", out var driverVersion))
                    {
                        // The driver string usually contains the version followed by the commit hash, so we split it
                        Console.WriteLine($"ChromeDriver Version: ");
                    }
                }

                try
                {
                    driver.Url = url;
                }
                catch (WebDriverException exception) when (exception.GetBaseException() is SocketException)
                {
                    KillChromeProcess(chromeProcessId);
                    DisposeDriverById(driverId);
                    throw new InvalidOperationException(
                        "ChromeDriver stopped unexpectedly while loading the page. " +
                        "Google Chrome and ChromeDriver may be temporarily incompatible. " +
                        "Make sure Chrome is up to date, then try again after a compatible ChromeDriver update is available.",
                        exception);
                }
                catch
                {
                    DisposeDriverById(driverId);
                    throw;
                }

                return driver;

            case (int)Browser.Firefox:
                FirefoxDriverService? firefoxDriverService = null;
                FirefoxDriver firefoxDriver;
                try
                {
                    firefoxDriverService = FirefoxDriverService.CreateDefaultService();
                    firefoxDriverService.HideCommandPromptWindow = true;
                    var firefoxOptions = new FirefoxOptions();

                    if (isHeadless)
                    {
                        firefoxOptions.AddArgument("-headless");
                    }

                    firefoxDriver = new FirefoxDriver(firefoxDriverService, firefoxOptions);
                    firefoxDriverService = null;
                }
                finally
                {
                    firefoxDriverService?.Dispose();
                }

                var firefoxDriverId = Interlocked.Increment(ref _counter);
                var firefoxProcessId = GetFirefoxProcessId(firefoxDriver);
                _drivers[firefoxDriverId] = firefoxDriver;

                try
                {
                    firefoxDriver.Url = url;
                }
                catch (WebDriverException exception) when (exception.GetBaseException() is SocketException)
                {
                    KillFirefoxProcess(firefoxProcessId);
                    DisposeDriverById(firefoxDriverId);
                    throw new InvalidOperationException(
                        "GeckoDriver stopped unexpectedly while loading the page. " +
                        "Make sure Firefox is up to date and try again.",
                        exception);
                }
                catch
                {
                    DisposeDriverById(firefoxDriverId);
                    throw;
                }

                return firefoxDriver;

            default:
                // throwing resolves error since everything needs to return the correct type
                throw new ArgumentException($"{browser} is not a valid value.");
        }
    }

    /// <summary>
    /// Creates a WebDriver instance asynchronously, navigates it to the given URL, and registers it in the
    /// thread-safe <see cref="ConcurrentDictionary{TKey, TValue}"/> that contains all drivers.
    /// </summary>
    /// <param name="url">The URL the driver should navigate to immediately after creation.</param>
    /// <param name="browser">The browser to launch, as the underlying integer value of the <see cref="Browser"/> enum.</param>
    /// <param name="isHeadless">true to run the browser without a visible window; otherwise, false.</param>
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

                        var chromeVersion = driver.Capabilities.GetCapability("browserVersion")?.ToString();

                        if (driver.Capabilities.GetCapability("chrome") is Dictionary<string, object> chromeOptionss)
                        {
                            if (chromeOptionss.TryGetValue("chromedriverVersion", out var driverVersion))
                            {
                                Console.WriteLine($"ChromeDriver Version: ");
                            }
                        }
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

                int id = Interlocked.Increment(ref _counter);
                var chromeProcessId = GetChromeProcessId(driver);
                _drivers.TryAdd(id, driver);
                try
                {
                    driver.Url = url;
                }
                catch (WebDriverException exception) when (exception.GetBaseException() is SocketException)
                {
                    KillChromeProcess(chromeProcessId);
                    DisposeDriverById(id);
                    throw new InvalidOperationException(
                        "ChromeDriver stopped unexpectedly while loading the page. " +
                        "Google Chrome and ChromeDriver may be temporarily incompatible. " +
                        "Make sure Chrome is up to date, then try again after a compatible ChromeDriver update is available.",
                        exception);
                }
                catch
                {
                    DisposeDriverById(id);
                    throw;
                }

                return driver;

            case (int)Browser.Firefox:
                FirefoxDriverService? firefoxDriverService = null;
                FirefoxDriver firefoxDriver;
                try
                {
                    firefoxDriverService = FirefoxDriverService.CreateDefaultService();
                    firefoxDriverService.HideCommandPromptWindow = true;
                    var firefoxOptions = new FirefoxOptions();

                    if (isHeadless)
                    {
                        firefoxOptions.AddArgument("-headless");
                    }

                    firefoxDriver = await Task.Run(
                        () => new FirefoxDriver(firefoxDriverService, firefoxOptions)).ConfigureAwait(false);
                    firefoxDriverService = null;
                }
                finally
                {
                    if (firefoxDriverService != null)
                    {
                        await firefoxDriverService.DisposeAsync().ConfigureAwait(false);
                    }
                }

                var firefoxDriverId = Interlocked.Increment(ref _counter);
                var firefoxProcessId = GetFirefoxProcessId(firefoxDriver);
                _drivers.TryAdd(firefoxDriverId, firefoxDriver);

                try
                {
                    firefoxDriver.Url = url;
                }
                catch (WebDriverException exception) when (exception.GetBaseException() is SocketException)
                {
                    KillFirefoxProcess(firefoxProcessId);
                    DisposeDriverById(firefoxDriverId);
                    throw new InvalidOperationException(
                        "GeckoDriver stopped unexpectedly while loading the page. " +
                        "Make sure Firefox is up to date and try again.",
                        exception);
                }
                catch
                {
                    DisposeDriverById(firefoxDriverId);
                    throw;
                }

                return firefoxDriver;

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

    public void DisposeDriver(IWebDriver driver)
    {
        var driverId = _drivers
            .Where(driverEntry => ReferenceEquals(driverEntry.Value, driver))
            .Select(driverEntry => (int?)driverEntry.Key)
            .FirstOrDefault();

        if (driverId.HasValue)
        {
            DisposeDriverById(driverId.Value);
            return;
        }

        QuitAndDisposeDriver(driver);
    }

    public void DisposeDriverById(int id)
    {
        IWebDriver? driver = null;
        try
        {
            if (_drivers.TryRemove(id, out driver))
            {
                QuitAndDisposeDriver(driver);
                driver = null;
            }
        }
        finally
        {
            driver?.Dispose();
        }
    }

    public void DisposeAllDrivers()
    {
        foreach (var driverId in _drivers.Keys)
        {
            IWebDriver? driver = null;
            try
            {
                if (_drivers.TryRemove(driverId, out driver))
                {
                    QuitAndDisposeDriver(driver);
                    driver = null;
                }
            }
            finally
            {
                driver?.Dispose();
            }
        }
    }

    private static int? GetChromeProcessId(ChromeDriver driver)
    {
        var processId = driver.Capabilities.GetCapability("goog:processID");
        return processId == null ? null : Convert.ToInt32(processId, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void KillChromeProcess(int? processId)
    {
        if (!processId.HasValue)
        {
            return;
        }

        try
        {
            using var chromeProcess = Process.GetProcessById(processId.Value);
            chromeProcess.Kill(true);
        }
        catch
        {
        }
    }

    private static int? GetFirefoxProcessId(FirefoxDriver driver)
    {
        var processId = driver.Capabilities.GetCapability("moz:processID");
        return processId == null ? null : Convert.ToInt32(processId, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void KillFirefoxProcess(int? processId)
    {
        if (!processId.HasValue)
        {
            return;
        }

        try
        {
            using var firefoxProcess = Process.GetProcessById(processId.Value);
            firefoxProcess.Kill(true);
        }
        catch
        {
        }
    }

    private static void QuitAndDisposeDriver(IWebDriver driver)
    {
        try
        {
            driver.Quit();
        }
        catch
        {
        }

        try
        {
            driver.Dispose();
        }
        catch
        {
        }
    }
}