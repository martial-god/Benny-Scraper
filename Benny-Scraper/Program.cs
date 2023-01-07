using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenQA.Selenium;

namespace Benny_Scraper
{
    internal class Program
    {
        //private const string _connectionString = "Server=localhost;Database=Test;TrustServerCertificate=True;Trusted_Connection=True;";
        // Added Task to Main in order to avoid "Program does not contain a static 'Main method suitable for an entry point"
        static async Task Main(string[] args)
        {
            // Create a service collection and configure the services
            
            var services = new ServiceCollection();
            Console.WriteLine($"Connection String: {GetConnectionString()}");

            //Configure service
            ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();
            Console.WriteLine($"Service Provider built");
            // Unable to add any parameters to this. This is what will actually call the Construct the applicationdbcontext from Package Manager Console
            var context = serviceProvider.GetService<ApplicationDbContext>();
            Console.WriteLine("Done with dbcontext");

            // Create the database if it doesn't exist
            context.Database.EnsureCreated();
            // Add a new novel to the database
            var novel = new Novel
            {
                Title = "Test Novel",
                Author = "Test Author",
                Description = "Test Description",
                SiteName = "Test Site",
                ChapterName = "Test Chapter",
                ChapterNumber = 1,
                DateCreated = DateTime.Now,
                Genre = "Test Genre",
                Url = "Test Url",
            };
            context.Novels.Add(novel);
            // Save the changes
            await context.SaveChangesAsync();
            //Console.WriteLine("Hello, World!");
            //IDriverFactory driverFactory = new DriverFactory(); // Instantiating an interface https://softwareengineering.stackexchange.com/questions/167808/instantiating-interfaces-in-c
            //Task<IWebDriver> driver = driverFactory.CreateDriverAsync(1, false, "https://www.deviantart.com/blix-kreeg");
            //Task<IWebDriver> driver2 = driverFactory.CreateDriverAsync(1, false, "https://www.google.com");
            //Task<IWebDriver> driver3 = driverFactory.CreateDriverAsync(1, false, "https://www.novelfull.com");
            //Task<IWebDriver> driver4 = driverFactory.CreateDriverAsync(1, false, "https://www.novelupdates.com");
            
            //await Task.WhenAll(driver, driver2, driver3, driver4);
            //driverFactory.DisposeAllDrivers();
        }

        private static string GetConnectionString()
        {
            // to resolve issue with the methods not being part of the ConfigurationBuilder() 
            //https://stackoverflow.com/questions/57158388/configurationbuilder-does-not-contain-a-definition-for-addjsonfile
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            
            string connectionString = builder.Build().GetConnectionString("DefaultConnection");
            return connectionString;
        }

        /// <summary>
        /// Manages servers we can add. This is the same as the MVC builder.Services, as we have the actual service
        /// </summary>
        /// <param name="services"></param>
        private static void ConfigureServices(IServiceCollection services)
        {
            // https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
            // Add Entity Framework services to the service collection
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(GetConnectionString()));

            // Add other services to the service collection as needed
            services.AddSingleton(GetConnectionString());
        }
    }
}