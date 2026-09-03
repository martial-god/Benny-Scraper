using System.Net;
using BennyScraper.BusinessLogic.Config;
using BennyScraper.BusinessLogic.Factory.Interfaces;

namespace BennyScraper.Tests;

public sealed class PaginationTests
{
    [Fact]
    public async Task PaginationWithoutPageCountStopsWhenNoNewChapterLinksAreFound()
    {
        using var httpMessageHandler = new PaginationHttpMessageHandler();
        var httpClientFactory = new PaginationHttpClientFactory(httpMessageHandler);
        using var scraperStrategy = new TestableStrategy(httpClientFactory);
        var tableOfContentsUri = new Uri("https://example.com/novel/test");
        scraperStrategy.ConfigurePagination(
            new SiteConfiguration
            {
                HasPagination = true,
                PaginationType = "?page-{0}",
                Selectors =
                {
                    TableOfContents =
                    {
                        ChapterLinks = "//a"
                    }
                }
            },
            tableOfContentsUri);

        var (chapterLinks, lastTableOfContentsUrl) = await scraperStrategy.GetPaginatedChapterLinksAsync(
            tableOfContentsUri,
            null);

        Assert.Equal(3, chapterLinks.Count);
        Assert.Equal(3, httpMessageHandler.RequestCount);
        Assert.Equal("https://example.com/novel/test?page-2", lastTableOfContentsUrl);
    }

    private sealed class PaginationHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var pageContent = request.RequestUri?.Query switch
            {
                "?page-1" => "<a href='/chapter-1'>Chapter 1</a><a href='/chapter-2'>Chapter 2</a>",
                "?page-2" => "<a href='/chapter-3'>Chapter 3</a>",
                _ => "<a href='/chapter-3'>Chapter 3</a>"
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(pageContent),
                RequestMessage = request
            });
        }
    }

    private sealed class PaginationHttpClientFactory(HttpMessageHandler httpMessageHandler) : IHttpClientFactory
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