using BennyScraper.BusinessLogic;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Extensions;
using BennyScraper.BusinessLogic.Factory;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.FileGenerators;
using BennyScraper.BusinessLogic.FileGenerators.Interfaces;
using BennyScraper.BusinessLogic.Interfaces;
using BennyScraper.BusinessLogic.Services;
using BennyScraper.BusinessLogic.Services.Interfaces;
using BennyScraper.DataAccess.Data;
using BennyScraper.DataAccess.DbInitializer;
using BennyScraper.DataAccess.Repository;
using BennyScraper.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BennyScraper;

internal static class ServiceRegistration
{
    internal static void AddBennyScraper(
        this IServiceCollection services,
        IConfiguration configuration,
        NovelScraperSettings novelScraperSettings,
        string connectionString)
    {
        services.AddSingleton(configuration);

        services.AddDbContext<Database>(options =>
            options.UseSqlite(connectionString, sqlite => sqlite.MigrationsAssembly("Benny-Scraper.DataAccess")));

        services.AddTransient<DbInitializer>();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddTransient<INovelProcessor, NovelProcessor>();
        services.AddTransient<IChapterRepository, ChapterRepository>();
        services.AddScoped<INovelService, NovelService>();
        services.AddScoped<IChapterService, ChapterService>();
        services.AddTransient<INovelRepository, NovelRepository>();
        services.AddTransient<IConfigurationRepository, ConfigurationRepository>();
        services.AddTransient<IEpubGenerator, EpubGenerator>();
        services.AddTransient<IComicBookArchiveGenerator, ComicBookArchiveGenerator>();

        services.AddHttpResilience();

        services.AddSingleton<IDriverFactory, DriverFactory>();

        services.AddSingleton(novelScraperSettings);

        services.AddSingleton(Options.Create(novelScraperSettings));

        services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            var settings = new EpubTemplates();
            config.GetSection("EpubTemplates").Bind(settings);
            return settings;
        });
        services.AddSingleton(provider => Options.Create(provider.GetRequiredService<EpubTemplates>()));

        services.AddTransient<IScraperStrategyFactory, ScraperStrategyFactory>();
    }
}