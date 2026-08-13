using System.Text.Json.Serialization;

namespace BennyScraper.BusinessLogic.Factory;

internal sealed class FlareSolverrSolution
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; init; }

    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;

    [JsonPropertyName("cookies")]
    public IList<FlareSolverrCookie>? Cookies { get; init; }

    [JsonPropertyName("userAgent")]
    public string UserAgent { get; set; } = string.Empty;
}