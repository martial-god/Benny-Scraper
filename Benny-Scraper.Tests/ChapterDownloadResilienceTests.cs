using System.Net;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using BennyScraper.Models;

namespace BennyScraper.Tests;

public sealed class ChapterDownloadResilienceTests
{
    [Fact]
    public async Task ImageChapterUsesHttpWhenSeleniumIsNotRequired()
    {
        using var httpMessageHandler = new ControlledChapterHttpMessageHandler();
        var httpClientFactory = new ControlledHttpClientFactory(httpMessageHandler);
        using var scraperStrategy = new TestableStrategy(httpClientFactory);
        var tableOfContentsUri = new Uri("https://example.com/novel");
        scraperStrategy.ConfigureChapterDownloads(
            CreateImageSiteConfiguration(),
            tableOfContentsUri,
            concurrentRequestLimit: 1);

        var chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(
            [new ChapterLink { Url = "https://example.com/image-chapter", Title = "Chapter 1", ChapterNumber = 1 }]);

        try
        {
            var chapterDataBuffer = Assert.Single(chapterDataBuffers);
            var page = Assert.Single(chapterDataBuffer.Pages!);

            Assert.False(chapterDataBuffer.IsPartial);
            Assert.Equal("https://example.com/chapter-page.jpg", page.Url);
            Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(page.ImagePath));
        }
        finally
        {
            var tempImageDirectory = chapterDataBuffers.FirstOrDefault()?.TempDirectory;
            foreach (var chapterDataBuffer in chapterDataBuffers)
            {
                chapterDataBuffer.Dispose();
            }

            if (!string.IsNullOrEmpty(tempImageDirectory) && Directory.Exists(tempImageDirectory))
            {
                Directory.Delete(tempImageDirectory, true);
            }
        }
    }

    [Fact]
    public async Task SuccessfulChapterResultsRemainWhenAnotherChapterRequestFails()
    {
        using var httpMessageHandler = new ControlledChapterHttpMessageHandler();
        var httpClientFactory = new ControlledHttpClientFactory(httpMessageHandler);
        using var scraperStrategy = new TestableStrategy(httpClientFactory);
        var tableOfContentsUri = new Uri("https://example.com/novel");
        scraperStrategy.ConfigureChapterDownloads(
            CreateSiteConfiguration(),
            tableOfContentsUri,
            concurrentRequestLimit: 1);

        var chapterLinks = new List<ChapterLink>
        {
            new() { Url = "https://example.com/chapter-1", Title = "Chapter 1", ChapterNumber = 1 },
            new() { Url = "https://example.com/failing-chapter", Title = "Chapter 2", ChapterNumber = 2 },
            new() { Url = "https://example.com/chapter-3", Title = "Chapter 3", ChapterNumber = 3 }
        };

        var chapterDataBuffers = await scraperStrategy.GetChaptersDataAsync(chapterLinks);

        try
        {
            Assert.Equal(3, chapterDataBuffers.Count);
            Assert.Equal([1, 2, 3], chapterDataBuffers.Select(chapter => chapter.SequenceNumber));

            Assert.Contains("Successfully scraped chapter-1", chapterDataBuffers[0].Content, StringComparison.Ordinal);
            Assert.False(chapterDataBuffers[0].IsPartial);

            Assert.Equal("Chapter 2", chapterDataBuffers[1].Title);
            Assert.Equal("No content found", chapterDataBuffers[1].Content);
            Assert.True(chapterDataBuffers[1].IsPartial);

            Assert.Contains("Successfully scraped chapter-3", chapterDataBuffers[2].Content, StringComparison.Ordinal);
            Assert.False(chapterDataBuffers[2].IsPartial);
        }
        finally
        {
            foreach (var chapterDataBuffer in chapterDataBuffers)
            {
                chapterDataBuffer.Dispose();
            }
        }
    }

    private static SiteConfiguration CreateSiteConfiguration()
    {
        return new SiteConfiguration
        {
            SiteName = "Resilience Test",
            UrlPattern = "example.com",
            MinimumChapterParagraphThreshold = 1,
            Selectors =
            {
                ChapterTitle = "//h1",
                ChapterContent = "//div[@class='chapter-content']/p"
            }
        };
    }

    private static SiteConfiguration CreateImageSiteConfiguration()
    {
        return new SiteConfiguration
        {
            SiteName = "Image Test",
            UrlPattern = "example.com",
            HasImagesForChapterContent = true,
            ChapterContentRequiresSelenium = false,
            Selectors =
            {
                ChapterTitle = "//h1",
                ChapterContent = "//div[@class='chapter-content']/img",
                ChapterContentImageUrlAttribute = "src"
            }
        };
    }

    private sealed class ControlledChapterHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath.EndsWith(".jpg", StringComparison.Ordinal) == true)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3, 4]),
                    RequestMessage = request
                });
            }

            if (request.RequestUri?.AbsolutePath.Contains("failing", StringComparison.Ordinal) == true)
            {
                return Task.FromException<HttpResponseMessage>(
                    new HttpRequestException("Simulated chapter request failure."));
            }

            var chapterName = request.RequestUri?.Segments.Last().Trim('/') ?? "unknown";
            var chapterContent = request.RequestUri?.AbsolutePath.Contains("image-chapter", StringComparison.Ordinal) == true
                ? "<img src=\"https://example.com/chapter-page.jpg\">"
                : $"<p>Successfully scraped {chapterName} with enough text for validation.</p>";
            var html = $"""
                        <html>
                          <body>
                            <h1>{chapterName}</h1>
                            <div class="chapter-content">
                              {chapterContent}
                            </div>
                          </body>
                        </html>
                        """;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html),
                RequestMessage = request
            };

            return Task.FromResult(response);
        }
    }

    private sealed class ControlledHttpClientFactory(HttpMessageHandler httpMessageHandler) : IHttpClientFactory
    {
        public HttpClient CreateClient() => new(httpMessageHandler, disposeHandler: false);

        public void AddCookie(Uri uri, Cookie cookie)
        {
        }

        public void AddCookiesFromHeader(Uri uri, string cookieHeader)
        {
        }
    }
}