using System.Text.Json.Serialization;

namespace BennyScraper.BusinessLogic.Factory;

public class FlareSolverrProxy
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
