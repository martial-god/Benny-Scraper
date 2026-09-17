using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;

namespace BennyScraper.BusinessLogic.Factory;

/// <summary>
/// Client for FlareSolverr proxy service to bypass Cloudflare protection.
/// FlareSolverr must be running (typically via Docker) at the configured URL.
///
/// Docker command to run FlareSolverr:
/// docker run -d --name=flaresolverr -p 8191:8191 -e LOG_LEVEL=info ghcr.io/flaresolverr/flaresolverr:latest.
/// </summary>
internal sealed class FlareSolverrService(string baseUrl = "http://localhost:8191", int timeoutSeconds = 60)
    : IDisposable, IAsyncDisposable
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(timeoutSeconds)
    };

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _baseUrl = baseUrl.TrimEnd('/');
    private readonly SemaphoreSlim _sessionSemaphoreSlim = new(1, 1);
    private bool _disposed;
    private string? _sessionId;

    public string? LastUserAgent { get; private set; }

    private bool IsEnabled { get; set; }

    /// <summary>
    /// Convenience method to get cookies from a FlareSolverr response formatted for HttpClientFactory.
    /// </summary>
    /// <param name="response">The FlareSolverr response to read cookies from.</param>
    /// <returns>A cookie header string suitable for use with <see cref="HttpClientFactory"/>, or an empty string if there are no cookies.</returns>
    public static string GetCookieHeader(FlareSolverrResponse response)
    {
        if (response.Solution?.Cookies == null || response.Solution.Cookies.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("; ", response.Solution.Cookies.Select(c => $"{c.Name}={c.Value}"));
    }

    /// <summary>
    /// Convenience method to get System.Net.Cookie objects from a FlareSolverr response.
    /// </summary>
    /// <param name="response">The FlareSolverr response to read cookies from.</param>
    /// <returns>The <see cref="Cookie"/> objects contained in the response, or an empty sequence if there are none.</returns>
    public static IEnumerable<Cookie> GetCookies(FlareSolverrResponse response)
    {
        return GetCookiesIterator(response);
    }

    /// <summary>
    /// Detects if an HTTP response indicates a Cloudflare challenge.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the response.</param>
    /// <param name="responseContent">The response body content, if any.</param>
    /// <returns>true if the response looks like a Cloudflare challenge page; otherwise, false.</returns>
    public static bool IsCloudflareChallenge(HttpStatusCode statusCode, string? responseContent)
    {
        // Cloudflare typically returns 403 or 503 with challenge pages
        if (statusCode != HttpStatusCode.Forbidden && statusCode != HttpStatusCode.ServiceUnavailable)
        {
            return false;
        }

        if (string.IsNullOrEmpty(responseContent))
        {
            return false;
        }

        // Check for common Cloudflare challenge indicators
        return responseContent.Contains("cf-browser-verification", StringComparison.Ordinal) ||
               responseContent.Contains("cf_clearance", StringComparison.Ordinal) ||
               responseContent.Contains("Checking your browser", StringComparison.Ordinal) ||
               responseContent.Contains("cloudflare", StringComparison.Ordinal) ||
               responseContent.Contains("_cf_chl_opt", StringComparison.Ordinal) ||
               responseContent.Contains("Just a moment...", StringComparison.Ordinal);
    }

    /// <summary>
    /// Check if FlareSolverr service is running and responding.
    /// </summary>
    /// <returns>true if FlareSolverr responded successfully to a health check; otherwise, false.</returns>
    public async Task<bool> CheckHealthAsync()
    {
        ThrowIfDisposed();

        try
        {
            using var response = await _httpClient.GetAsync($"{_baseUrl}/health").ConfigureAwait(false);
            IsEnabled = response.IsSuccessStatusCode;
            if (IsEnabled)
            {
                _logger.Debug("FlareSolverr is available and healthy");
            }

            return IsEnabled;
        }
        catch (Exception ex)
        {
            _logger.Debug($"FlareSolverr health check failed: {ex.Message}");
            IsEnabled = false;
            return false;
        }
    }

    public async Task<bool> CreateSessionAsync()
    {
        ThrowIfDisposed();

        await _sessionSemaphoreSlim.WaitAsync().ConfigureAwait(false);
        try
        {
            return await CreateSessionCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _sessionSemaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Solve a Cloudflare challenge for the given URL.
    /// Returns the solved HTML content and cookies.
    /// </summary>
    /// <param name="url">The URL to solve.</param>
    /// <param name="maxTimeout">Maximum time to wait for the challenge to be solved (ms).</param>
    /// <returns>FlareSolverr response with cookies and HTML content, or null if failed.</returns>
    public async Task<FlareSolverrResponse?> SolveAsync(string url, int maxTimeout = 60000)
    {
        ThrowIfDisposed();

        await _sessionSemaphoreSlim.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_sessionId == null && !await CreateSessionCoreAsync().ConfigureAwait(false))
            {
                return null;
            }

            var request = new FlareSolverrRequest
            {
                Cmd = "request.get",
                Url = url,
                MaxTimeout = maxTimeout,
                Session = _sessionId
            };

            _logger.Debug($"Sending URL to FlareSolverr using session {_sessionId}: {url}");

            var result = await SendRequestAsync(request).ConfigureAwait(false);

            if (result?.Status == "ok")
            {
                _logger.Debug($"FlareSolverr successfully solved challenge for {url}");
                _logger.Debug($"Cookies received: {result.Solution?.Cookies?.Count ?? 0}");
                LastUserAgent = result.Solution?.UserAgent;

                return result;
            }

            _logger.Error($"FlareSolverr returned error: {result?.Message ?? "Unknown error"}");
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.Error($"FlareSolverr request timed out for {url}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error($"FlareSolverr request failed: {ex.Message}");
            return null;
        }
        finally
        {
            _sessionSemaphoreSlim.Release();
        }
    }

    public async Task DestroySessionAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _sessionSemaphoreSlim.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_sessionId == null)
            {
                return;
            }

            var sessionId = _sessionId;
            _sessionId = null;

            var result = await SendRequestAsync(new FlareSolverrRequest
            {
                Cmd = "sessions.destroy",
                Session = sessionId
            }).ConfigureAwait(false);

            if (result?.Status == "ok")
            {
                _logger.Debug($"Destroyed FlareSolverr session {sessionId}");
            }
            else
            {
                _logger.Warn($"FlareSolverr could not destroy session {sessionId}: {result?.Message ?? "Unknown error"}");
            }
        }
        catch (Exception exception)
        {
            _logger.Warn($"FlareSolverr could not destroy session: {exception.Message}");
        }
        finally
        {
            _sessionSemaphoreSlim.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            GC.SuppressFinalize(this);
            return;
        }

        await DestroySessionAsync().ConfigureAwait(false);
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            GC.SuppressFinalize(this);
            return;
        }

        _disposed = true;
        _sessionSemaphoreSlim.Dispose();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private static IEnumerable<Cookie> GetCookiesIterator(FlareSolverrResponse response)
    {
        if (response.Solution?.Cookies == null)
        {
            yield break;
        }

        foreach (var cookie in response.Solution.Cookies)
        {
            yield return new Cookie(cookie.Name, cookie.Value)
            {
                Domain = cookie.Domain,
                Path = cookie.Path ?? "/",
                HttpOnly = cookie.HttpOnly,
                Secure = cookie.Secure
            };
        }
    }

    private async Task<bool> CreateSessionCoreAsync()
    {
        if (_sessionId != null)
        {
            return true;
        }

        var sessionId = $"benny-scraper-{Guid.NewGuid():N}";

        try
        {
            var result = await SendRequestAsync(new FlareSolverrRequest
            {
                Cmd = "sessions.create",
                Session = sessionId
            }).ConfigureAwait(false);

            if (result?.Status != "ok")
            {
                _logger.Error($"FlareSolverr could not create session: {result?.Message ?? "Unknown error"}");
                return false;
            }

            _sessionId = sessionId;
            _logger.Debug($"Created FlareSolverr session {_sessionId}");
            return true;
        }
        catch (Exception exception)
        {
            _logger.Error($"FlareSolverr could not create session: {exception.Message}");
            return false;
        }
    }

    private async Task<FlareSolverrResponse?> SendRequestAsync(FlareSolverrRequest request)
    {
        var json = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync($"{_baseUrl}/v1", content).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Error($"FlareSolverr request failed with status {response.StatusCode}: {responseBody}");
            return null;
        }

        return JsonSerializer.Deserialize<FlareSolverrResponse>(responseBody, _jsonSerializerOptions);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, nameof(FlareSolverrService));
}