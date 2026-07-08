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
/// docker run -d --name=flaresolverr -p 8191:8191 -e LOG_LEVEL=info ghcr.io/flaresolverr/flaresolverr:latest
/// </summary>
public sealed class FlareSolverrService : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether FlareSolverr is enabled and available.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Gets the user-agent returned by FlareSolverr after solving a challenge.
    /// Use this for subsequent requests to the same site.
    /// </summary>
    public string? LastUserAgent { get; private set; }

    /// <summary>
    /// Create a new FlareSolverr client.
    /// </summary>
    /// <param name="baseUrl">FlareSolverr URL (default: http://localhost:8191)</param>
    /// <param name="timeoutSeconds">Request timeout in seconds</param>
    public FlareSolverrService(string baseUrl = "http://localhost:8191", int timeoutSeconds = 60)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
    }

    /// <summary>
    /// Check if FlareSolverr service is running and responding.
    /// </summary>
    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/health");
            IsEnabled = response.IsSuccessStatusCode;
            if (IsEnabled)
            {
                Logger.Info("FlareSolverr is available and healthy");
            }

            return IsEnabled;
        }
        catch (Exception ex)
        {
            Logger.Debug($"FlareSolverr health check failed: {ex.Message}");
            IsEnabled = false;
            return false;
        }
    }

    /// <summary>
    /// Solve a Cloudflare challenge for the given URL.
    /// Returns the solved HTML content and cookies.
    /// </summary>
    /// <param name="url">The URL to solve</param>
    /// <param name="maxTimeout">Maximum time to wait for the challenge to be solved (ms)</param>
    /// <returns>FlareSolverr response with cookies and HTML content, or null if failed</returns>
    public async Task<FlareSolverrResponse?> SolveAsync(string url, int maxTimeout = 60000)
    {
        ThrowIfDisposed();

        var request = new FlareSolverrRequest
        {
            Cmd = "request.get",
            Url = url,
            MaxTimeout = maxTimeout
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(request, jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        Logger.Info($"Sending URL to FlareSolverr: {url}");

        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/v1", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Logger.Error($"FlareSolverr request failed with status {response.StatusCode}: {responseBody}");
                return null;
            }

            var result = JsonSerializer.Deserialize<FlareSolverrResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Status == "ok")
            {
                Logger.Info($"FlareSolverr successfully solved challenge for {url}");
                Logger.Debug($"Cookies received: {result.Solution?.Cookies?.Count ?? 0}");

                // Store the user-agent for subsequent requests
                LastUserAgent = result.Solution?.UserAgent;

                return result;
            }

            Logger.Error($"FlareSolverr returned error: {result?.Message ?? "Unknown error"}");
            return null;
        }
        catch (TaskCanceledException)
        {
            Logger.Error($"FlareSolverr request timed out for {url}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error($"FlareSolverr request failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Convenience method to get cookies from a FlareSolverr response formatted for HttpClientFactory.
    /// </summary>
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
    public static IEnumerable<Cookie> GetCookies(FlareSolverrResponse response)
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

    /// <summary>
    /// Detects if an HTTP response indicates a Cloudflare challenge.
    /// </summary>
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
        return responseContent.Contains("cf-browser-verification") ||
               responseContent.Contains("cf_clearance") ||
               responseContent.Contains("Checking your browser") ||
               responseContent.Contains("cloudflare") ||
               responseContent.Contains("_cf_chl_opt") ||
               responseContent.Contains("Just a moment...");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            GC.SuppressFinalize(this);
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FlareSolverrService));
        }
    }
}