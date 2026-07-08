using System.Text.Json.Serialization;

namespace BennyScraper.BusinessLogic.Factory;

public class FlareSolverrCookie
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("expires")]
    public double? Expires { get; set; }

    [JsonPropertyName("size")]
    public int? Size { get; set; }

    [JsonPropertyName("httpOnly")]
    public bool HttpOnly { get; set; }

    [JsonPropertyName("secure")]
    public bool Secure { get; set; }

    [JsonPropertyName("session")]
    public bool Session { get; set; }

    [JsonPropertyName("sameSite")]
    public string? SameSite { get; set; }
}
