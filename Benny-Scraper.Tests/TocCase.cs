using BennyScraper.BusinessLogic.Scrapers.Strategy.Impl;
using BennyScraper.Models;
using Xunit;
using Xunit.Abstractions;
using Attr = BennyScraper.BusinessLogic.Scrapers.Strategy.Impl.NovelDataInitializer.Attr;

namespace BennyScraper.Tests;

/// <summary>One row of <see cref="TableOfContentsParsingTests"/>: a synthetic TOC and what it should yield.</summary>
public sealed class TocCase : IXunitSerializable
{
    public string UrlPattern { get; set; } = string.Empty;

    public string Toc { get; set; } = string.Empty;

    public string ExpectedTitle { get; set; } = string.Empty;

    public string ExpectedAuthor { get; set; } = string.Empty;

    public int ExpectedChapterCount { get; set; }

    public string ExpectedFirstChapterUrlSuffix { get; set; } = string.Empty;

    public string ExpectedCurrentChapterUrlSuffix { get; set; } = string.Empty;

    public string ExpectedMostRecentChapterTitle { get; set; } = string.Empty;

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(UrlPattern), UrlPattern);
        info.AddValue(nameof(Toc), Toc);
        info.AddValue(nameof(ExpectedTitle), ExpectedTitle);
        info.AddValue(nameof(ExpectedAuthor), ExpectedAuthor);
        info.AddValue(nameof(ExpectedChapterCount), ExpectedChapterCount);
        info.AddValue(nameof(ExpectedFirstChapterUrlSuffix), ExpectedFirstChapterUrlSuffix);
        info.AddValue(nameof(ExpectedCurrentChapterUrlSuffix), ExpectedCurrentChapterUrlSuffix);
        info.AddValue(nameof(ExpectedMostRecentChapterTitle), ExpectedMostRecentChapterTitle);
    }

    public void Deserialize(IXunitSerializationInfo info)
    {
        UrlPattern = info.GetValue<string>(nameof(UrlPattern));
        Toc = info.GetValue<string>(nameof(Toc));
        ExpectedTitle = info.GetValue<string>(nameof(ExpectedTitle));
        ExpectedAuthor = info.GetValue<string>(nameof(ExpectedAuthor));
        ExpectedChapterCount = info.GetValue<int>(nameof(ExpectedChapterCount));
        ExpectedFirstChapterUrlSuffix = info.GetValue<string>(nameof(ExpectedFirstChapterUrlSuffix));
        ExpectedCurrentChapterUrlSuffix = info.GetValue<string>(nameof(ExpectedCurrentChapterUrlSuffix));
        ExpectedMostRecentChapterTitle = info.GetValue<string>(nameof(ExpectedMostRecentChapterTitle));
    }

    public override string ToString() => UrlPattern;
}