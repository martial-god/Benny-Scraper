using System.Collections.Concurrent;
using BennyScraper.Models;
using OpenQA.Selenium;

namespace BennyScraper.BusinessLogic.Factory.Interfaces;

internal interface IDriverFactory
{
    /// <summary>
    /// Creates a WebDriver instance, navigates it to the given URL, registers it in the internal driver
    /// dictionary keyed by an auto-incrementing id, and returns it.
    /// </summary>
    /// <param name="url">The URL the driver should navigate to immediately after creation.</param>
    /// <param name="browser">The browser to launch.</param>
    /// <param name="isHeadless">true to run the browser without a visible window; otherwise, false.</param>
    /// <returns>The created <see cref="IWebDriver"/> instance.</returns>
    /// <exception cref="ArgumentException">The specified browser is not supported.</exception>
    IWebDriver CreateDriver(string url, SeleniumBrowser browser = SeleniumBrowser.Chrome, bool isHeadless = false);

    Task<IWebDriver> CreateDriverAsync(string url, SeleniumBrowser browser = SeleniumBrowser.Chrome, bool isHeadless = false);

    IWebDriver GetDriverById(int id);

    void DisposeDriver(IWebDriver driver);

    /// <summary>
    /// Disposes the driver registered under the given id and removes it from the internal driver dictionary.
    /// </summary>
    /// <param name="id">The id of the driver to dispose.</param>
    void DisposeDriverById(int id);

    /// <summary>
    /// Gets dictionary that contains all drivers instances created. See <see cref="DisposeDriverById(int)"/>.
    /// </summary>
    /// <returns>The <see cref="ConcurrentDictionary{TKey, TValue}"/> of all currently tracked driver instances, keyed by id.</returns>
    ConcurrentDictionary<int, IWebDriver> GetAllDrivers();

    /// <summary>
    /// Deletes all drivers.
    /// </summary>
    void DisposeAllDrivers();
}