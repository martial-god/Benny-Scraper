using System.Net;
using BennyScraper.BusinessLogic.Factory.Interfaces;
using Polly;

namespace BennyScraper.BusinessLogic.Factory;

/// <summary>
/// Reuses a single underlying handler to avoid socket exhaustion, while returning new HttpClient instances
/// so callers can safely set per-request headers without affecting others.
/// Includes cookie management for better Cloudflare bypass.
/// </summary>
internal sealed class HttpClientFactory : IHttpClientFactory, IDisposable
{
    private readonly SocketsHttpHandler _handler;
    private readonly System.Net.CookieContainer _cookieContainer;
    private readonly ResiliencePipeline<HttpResponseMessage> _responsePipeline;
    private bool _disposed;

    public HttpClientFactory(
        ResiliencePipeline<HttpResponseMessage>? responsePipeline = null,
        TimeSpan? timeout = null)
    {
        _responsePipeline = responsePipeline ?? HttpClientResilience.CreatePipeline();
        Timeout = timeout ?? TimeSpan.FromSeconds(30);

        // Shared cookie container across all clients for proper session management
        _cookieContainer = new System.Net.CookieContainer();

        _handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 20,

            // Enable cookie management for Cloudflare session handling
            CookieContainer = _cookieContainer,
            UseCookies = true,

            // Allow cookies to be sent across domains (important for CDN-hosted content)
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };
    }

    private TimeSpan Timeout { get; }

    public HttpClient CreateClient()
    {
        ThrowIfDisposed();

        ResilientHttpMessageHandler? handler = null;
        try
        {
            handler = new ResilientHttpMessageHandler(_handler, _responsePipeline);
            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout
            };
            handler = null; // Ownership transferred to client (disposeHandler: true); don't dispose it below.

            // Updated default user agent for 2024-2025
            // Note: This is overridden per request in ScraperStrategy for rotation
            if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            }

            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    /// <summary>
    /// Add a cookie to the shared cookie container for a specific URI.
    /// Useful for injecting cookies from the browser to bypass Cloudflare.
    /// </summary>
    /// <param name="uri">The URI the cookie applies to.</param>
    /// <param name="cookie">The cookie to add.</param>
    public void AddCookie(Uri uri, Cookie cookie)
    {
        ThrowIfDisposed();
        _cookieContainer.Add(uri, cookie);
    }

    /// <summary>
    /// Add cookies from a browser cookie header string.
    /// Example: "cf_clearance=abc123; session=xyz789".
    /// </summary>
    /// <param name="uri">The URI the cookies apply to.</param>
    /// <param name="cookieHeader">The raw cookie header string to parse.</param>
    public void AddCookiesFromHeader(Uri uri, string cookieHeader)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(cookieHeader))
        {
            return;
        }

        var cookiePairs = cookieHeader.Split(';');
        foreach (var pair in cookiePairs)
        {
            var trimmedPair = pair.Trim();
            var separatorIndex = trimmedPair.IndexOf('=', StringComparison.Ordinal);

            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = trimmedPair[..separatorIndex].Trim();
            var value = trimmedPair[(separatorIndex + 1)..].Trim();

            var cookie = new Cookie(name, value)
            {
                Domain = uri.Host
            };

            _cookieContainer.Add(uri, cookie);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _handler.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, nameof(HttpClientFactory));
}