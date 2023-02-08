using OpenQA.Selenium;
using System.Collections.Concurrent;

namespace Benny_Scraper.Interfaces
{
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
        IWebDriver CreateDriver(int browser, bool isHeadless, string url);

        Task<IWebDriver> CreateDriverAsync(int browser, bool isHeadless, string url);
        IWebDriver GetDriverById(int id);

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
}
