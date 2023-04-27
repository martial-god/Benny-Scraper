using Autofac;
using Benny_Scraper.BusinessLogic;
using Benny_Scraper.BusinessLogic.Config;
using Benny_Scraper.BusinessLogic.Factory;
using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using Benny_Scraper.BusinessLogic.Interfaces;
using Benny_Scraper.BusinessLogic.Services;
using Benny_Scraper.BusinessLogic.Services.Interface;
using Benny_Scraper.DataAccess.Data;
using Benny_Scraper.DataAccess.DbInitializer;
using Benny_Scraper.DataAccess.Repository;
using Benny_Scraper.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Benny_Scraper
{
    public class StartUp
    {
        private static readonly string _appSettings = "appsettings.json";
        private static readonly string _connectionType = "DefaultConnection";
        public IConfiguration Configuration { get; }

        public StartUp(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(ContainerBuilder builder)
        {
            // Register IConfiguration
            builder.RegisterInstance(Configuration).As<IConfiguration>();

            builder.Register(c => new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(GetConnectionString(), options => options.MigrationsAssembly("Benny-Scraper.DataAccess")).Options)).InstancePerLifetimeScope();


            builder.RegisterType<DbInitializer>().As<IDbInitializer>();
            builder.RegisterType<UnitOfWork>().As<IUnitOfWork>();
            builder.RegisterType<NovelProcessor>().As<INovelProcessor>();
            builder.RegisterType<ChapterRepository>().As<IChapterRepository>();
            builder.RegisterType<NovelService>().As<INovelService>().InstancePerLifetimeScope();
            builder.RegisterType<ChapterService>().As<IChapterService>().InstancePerLifetimeScope();
            builder.RegisterType<NovelRepository>().As<INovelRepository>();
            builder.RegisterType<EpubGenerator>().As<IEpubGenerator>().InstancePerDependency();

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

            // register the factory
            builder.Register<Func<string, INovelScraper>>(c =>
            {
                var context = c.Resolve<IComponentContext>();
                return key => context.ResolveNamed<INovelScraper>(key);
            });

            builder.RegisterType<NovelScraperFactory>().As<INovelScraperFactory>().InstancePerDependency();
            builder.RegisterType<SeleniumNovelScraper>().Named<INovelScraper>("Selenium").InstancePerDependency(); // InstancePerDependency() similar to transient
            builder.RegisterType<HttpNovelScraper>().Named<INovelScraper>("Http").InstancePerDependency();
        }

        private static string GetConnectionString()
        {
            // to resolve issue with the methods not being part of the ConfigurationBuilder() 
            //https://stackoverflow.com/questions/57158388/configurationbuilder-does-not-contain-a-definition-for-addjsonfile
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(_appSettings, optional: true, reloadOnChange: true);

            string connectionString = builder.Build().GetConnectionString(_connectionType);
            return connectionString;
        }
    }
}
