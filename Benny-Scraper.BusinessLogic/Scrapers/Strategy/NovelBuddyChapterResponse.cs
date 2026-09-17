using System.Text.Json.Serialization;

namespace BennyScraper.BusinessLogic.Scrapers.Strategy;

internal sealed record NovelBuddyChapterResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public NovelBuddyChapterResponseData? Data { get; init; }
}

internal sealed record NovelBuddyChapterResponseData
{
    [JsonPropertyName("chapters")]
    public IReadOnlyList<NovelBuddyChapterResponseItem> Chapters { get; init; } = [];
}

internal sealed record NovelBuddyChapterResponseItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    [JsonPropertyName("views")]
    public int Views { get; init; }

    [JsonPropertyName("comments_count")]
    public int CommentsCount { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("cv")]
    public long Cv { get; init; }
}