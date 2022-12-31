using Benny_Scraper.DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;

namespace Benny_Scraper
{
    internal class Program
    {
        // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"
        static async Task Main(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer("DefaultConnection");
            using (var context = new ApplicationDbContext(optionsBuilder.Options))
            {
                // Your code here...
                context.Database.EnsureCreated();
            }
            Console.WriteLine("Hello, World!");
            IDriverFactory driverFactory = new DriverFactory(); // Instantiating an interface https://softwareengineering.stackexchange.com/questions/167808/instantiating-interfaces-in-c
            Task<IWebDriver> driver = driverFactory.CreateDriverAsync(1, false, "https://www.deviantart.com/blix-kreeg");
            Task<IWebDriver> driver2 = driverFactory.CreateDriverAsync(1, false, "https://www.google.com");
            Task<IWebDriver> driver3 = driverFactory.CreateDriverAsync(1, false, "https://www.novelfull.com");
            Task<IWebDriver> driver4 = driverFactory.CreateDriverAsync(1, false, "https://www.novelupdates.com");
            //await driver;
            //await driver2;
            //await driver3;
            await Task.WhenAll(driver, driver2, driver3, driver4);
            //Console.WriteLine(driverFactory.GetAllDrivers().Count);

            // Create an array of tasks to represent the asynchronous operations
            //var tasks = new Task<IWebDriver>[3];
            //tasks[0] = driver;
            //tasks[1] = driver2;
            //tasks[2] = driver3;

            // Create a loop to create 1000 driver instances
            //for (int i = 0; i < 3; i++)
            //{
            //    // Create a new driver instance and add it to the tasks array
            //    Console.WriteLine($"Creating driver {i}");
            //    tasks[i] = driverFactory.CreateDriverAsync(1, false, "https://www.google.com");
            //}

            // Wait for all tasks to complete
            //await Task.WhenAll(tasks);

            //Dispose of all driver instances
            //foreach (var task in tasks)
            //{
            //    using (var driver = task.Result) // will automatically dispose of the instance once used
            //    {
            //        driver.Dispose();
            //        Console.WriteLine($"Disposing driver {task.Result.GetHashCode()}");
            //    }
            //    //task.Result.Dispose();
            //    //task.Result.Quit();
            //}
            driverFactory.DisposeAllDrivers();

            //driverFactory.DisposeAllDrivers();
        }
    }
}