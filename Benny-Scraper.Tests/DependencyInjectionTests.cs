using System.Net;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.BusinessLogic.FileGenerators.Interfaces;
using BennyScraper.BusinessLogic.Interfaces;
using BennyScraper.BusinessLogic.Scrapers.Strategy;
using BennyScraper.BusinessLogic.Services.Interfaces;
using BennyScraper.BusinessLogic.Utilities;
using BennyScraper.DataAccess.Data;
using BennyScraper.DataAccess.DbInitializer;
using BennyScraper.DataAccess.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpClientFactory = BennyScraper.BusinessLogic.Factory.HttpClientFactory;
using IHttpClientFactory = BennyScraper.BusinessLogic.Factory.Interfaces.IHttpClientFactory;

namespace BennyScraper.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public async Task ApplicationServicesResolveAndRespectLifetimes()
    {
        SQLitePCL.Batteries.Init();
        var settings = new NovelScraperSettings();
        using var configuration = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddBennyScraper(configuration, settings, "Data Source=:memory:");
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<Database>());
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var first = firstScope.ServiceProvider;
        var second = secondScope.ServiceProvider;

        var database = first.GetRequiredService<Database>();
        Assert.Same(database, first.GetRequiredService<Database>());
        Assert.NotSame(database, second.GetRequiredService<Database>());
        Assert.Same(first.GetRequiredService<INovelService>(), first.GetRequiredService<INovelService>());
        Assert.NotSame(first.GetRequiredService<INovelService>(), second.GetRequiredService<INovelService>());
        Assert.Same(first.GetRequiredService<IDriverFactory>(), second.GetRequiredService<IDriverFactory>());
        Assert.Same(first.GetRequiredService<IHttpClientFactory>(), second.GetRequiredService<IHttpClientFactory>());
        Assert.Same(settings, first.GetRequiredService<IOptions<NovelScraperSettings>>().Value);
        Assert.NotSame(first.GetRequiredService<INovelProcessor>(), first.GetRequiredService<INovelProcessor>());
        Assert.NotNull(first.GetRequiredService<IChapterService>());
        Assert.NotNull(first.GetRequiredService<IConfigurationRepository>());
        Assert.NotNull(first.GetRequiredService<IEpubGenerator>());
        Assert.NotNull(first.GetRequiredService<IComicBookArchiveGenerator>());
        Assert.NotNull(first.GetRequiredService<DbInitializer>());

        var factory = first.GetRequiredService<IScraperStrategyFactory>();
        using var strategy = factory.Create(new Uri("https://example.com/novel"), new SiteConfiguration());
        Assert.IsType<CommonStrategy>(strategy);

        await database.Database.OpenConnectionAsync();
        Assert.True(await first.GetRequiredService<DbInitializer>().InitializeAsync());
        var savedConfiguration = await first.GetRequiredService<IConfigurationRepository>().GetByIdAsync(1);
        Assert.Equal("Default", savedConfiguration.Name);
    }

    [Theory]
    [InlineData("common", "CommonStrategy")]
    [InlineData("lightnovelworld", "LightNovelWorldStrategy")]
    [InlineData("mangakakalot", "MangaKakalotStrategy")]
    [InlineData("mangakatana", "MangaKatanaStrategy")]
    [InlineData("mangareader", "MangaReaderStrategy")]
    [InlineData("novelbin", "NovelBinStrategy")]
    [InlineData("noveldrama", "NovelDramaStrategy")]
    [InlineData("novelfull", "NovelFullStrategy")]
    [InlineData("novelbuddy", "NovelBuddyStrategy")]
    [InlineData("royalroad", "RoyalRoadStrategy")]
    [InlineData("wanderinginn", "WanderingInnStrategy")]
    [InlineData("wuxiaworld", "WuxiaWorldStrategy")]
    [InlineData("", "CommonStrategy")]
    [InlineData(" ", "CommonStrategy")]
    public async Task StrategySelectionCreatesFreshStateForEachOperation(string strategyName, string expectedType)
    {
        using var httpFactory = new RecordingHttpClientFactory();
        using var driverFactory = new DriverFactory();
        var factory = new ScraperStrategyFactory(httpFactory, driverFactory);
        var uri = new Uri("https://example.com/novel");
        var site = new SiteConfiguration { StrategyName = strategyName };

        await using (var first = factory.Create(uri, site))
        {
            Assert.NotNull(first);
            Assert.Equal(expectedType, first.GetType().Name);
            first.SetChapterRange(new SelectedChapterRange(1, 10));

            await using var second = factory.Create(uri, site);
            Assert.NotNull(second);
            Assert.NotSame(first, second);
            Assert.Null(second.GetChapterRange());
        }

        Assert.Equal(0, httpFactory.RequestCount);
        Assert.False(httpFactory.IsDisposed);
        using var client = httpFactory.CreateClient();
        Assert.NotNull(client);
    }

    [Fact]
    public async Task CommonStrategyScrapesUsingInjectedHttpService()
    {
        using var httpFactory = new RecordingHttpClientFactory();
        using var drivers = new DriverFactory();
        var factory = new ScraperStrategyFactory(httpFactory, drivers);
        var uri = new Uri("https://example.com/novel");
        var site = new SiteConfiguration();
        site.Selectors.TableOfContents.NovelTitle = "//p";
        site.Selectors.TableOfContents.ChapterLinks = "//a";
        await using (var strategy = factory.Create(uri, site))
        {
            Assert.NotNull(strategy);
            strategy.SetVariables(site, uri, new Models.Configuration { ConcurrencyLimit = 1 });
            var novel = await strategy.ScrapeAsync();
            Assert.Equal("Injected content", novel.Title);
            Assert.Equal("https://example.com/chapter-1", Assert.Single(novel.ChapterLinks).Url);
        }

        Assert.Equal(1, httpFactory.RequestCount);
        Assert.False(httpFactory.IsDisposed);
    }

    [Theory]
    [InlineData("missing", true)]
    [InlineData("common", false)]
    public void UnavailableStrategiesReturnNull(string strategyName, bool isActive)
    {
        using var httpFactory = new HttpClientFactory();
        using var drivers = new DriverFactory();
        var factory = new ScraperStrategyFactory(httpFactory, drivers);
        using var strategy = factory.Create(
            new Uri("https://example.com/novel"),
            new SiteConfiguration { StrategyName = strategyName, IsActive = isActive });

        Assert.Null(strategy);
    }

    [Fact]
    public async Task ProviderDisposesSharedHttpServiceAtShutdown()
    {
        using var configuration = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddBennyScraper(configuration, new NovelScraperSettings(), "Data Source=:memory:");
        IHttpClientFactory httpFactory;
        await using (var provider = services.BuildServiceProvider())
        {
            httpFactory = provider.GetRequiredService<IHttpClientFactory>();
            await using (var scope = provider.CreateAsyncScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<IScraperStrategyFactory>();
                await using var strategy = factory.Create(new Uri("https://example.com/novel"), new SiteConfiguration());
            }

            using var client = httpFactory.CreateClient();
            Assert.NotNull(client);
        }

        Assert.Throws<ObjectDisposedException>(() => httpFactory.CreateClient());
    }

    private sealed class RecordingHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly RecordingHandler _handler = new();

        public bool IsDisposed { get; private set; }

        public int RequestCount => _handler.RequestCount;

        public HttpClient CreateClient()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return new HttpClient(_handler, disposeHandler: false);
        }

        public void AddCookie(Uri uri, Cookie cookie) => throw new NotSupportedException();

        public void AddCookiesFromHeader(Uri uri, string cookieHeader) => throw new NotSupportedException();

        public void Dispose()
        {
            IsDisposed = true;
            _handler.Dispose();
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent("<html><body><p>Injected content</p><a href='/chapter-1'>Chapter 1</a></body></html>")
            });
        }
    }
}