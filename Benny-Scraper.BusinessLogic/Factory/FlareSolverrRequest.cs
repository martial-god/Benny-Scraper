using System.Text.Json.Serialization;

namespace BennyScraper.BusinessLogic.Factory;

public class FlareSolverrRequest
{
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = "request.get";

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("maxTimeout")]
    public int MaxTimeout { get; set; } = 60000;

    [JsonPropertyName("session")]
    public string? Session { get; set; }

    [JsonPropertyName("session_ttl_minutes")]
    public int? SessionTtlMinutes { get; set; }

    [JsonPropertyName("cookies")]
    public IList<FlareSolverrCookie>? Cookies { get; init; }

    [JsonPropertyName("returnOnlyCookies")]
    public bool? ReturnOnlyCookies { get; set; }

    [JsonPropertyName("proxy")]
    public FlareSolverrProxy? Proxy { get; set; }
}
