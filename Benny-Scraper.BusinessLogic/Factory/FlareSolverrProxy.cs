using System.Text.Json.Serialization;

namespace BennyScraper.BusinessLogic.Factory;

internal sealed class FlareSolverrProxy
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}