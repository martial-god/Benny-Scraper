using System.Text.Json.Serialization;

namespace BennyScraper.BusinessLogic.Factory;

public class FlareSolverrResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("startTimestamp")]
    public long StartTimestamp { get; set; }

    [JsonPropertyName("endTimestamp")]
    public long EndTimestamp { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("solution")]
    public FlareSolverrSolution? Solution { get; set; }
}
