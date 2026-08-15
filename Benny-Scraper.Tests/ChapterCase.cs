using HtmlAgilityPack;
using Xunit;
using Xunit.Abstractions;

namespace BennyScraper.Tests;

public sealed class ChapterCase : IXunitSerializable
{
    public string UrlPattern { get; set; } = string.Empty;

    public string Chapter { get; set; } = string.Empty;

    public string MustContain { get; set; } = string.Empty;

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(UrlPattern), UrlPattern);
        info.AddValue(nameof(Chapter), Chapter);
        info.AddValue(nameof(MustContain), MustContain);
    }

    public void Deserialize(IXunitSerializationInfo info)
    {
        UrlPattern = info.GetValue<string>(nameof(UrlPattern));
        Chapter = info.GetValue<string>(nameof(Chapter));
        MustContain = info.GetValue<string>(nameof(MustContain));
    }

    public override string ToString() => UrlPattern;
}