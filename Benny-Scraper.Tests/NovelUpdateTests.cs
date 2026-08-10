using BennyScraper.BusinessLogic.Helper;
using BennyScraper.BusinessLogic.Utilities;
using BennyScraper.Models;

namespace BennyScraper.Tests;

public class NovelUpdateTests
{
    [Fact]
    public void UpdateNovelUsesActuallyDownloadedChaptersForSavedBoundaries()
    {
        var novel = new Novel
        {
            Title = "Range Test",
            FirstChapter = "stale-first",
            CurrentChapter = "stale-current",
            CurrentChapterUrl = "stale-current-url"
        };
        novel.Chapters.Add(new Chapter
        {
            Number = 50,
            Title = "Chapter 50",
            Url = "https://example.com/chapter-50"
        });
        novel.Chapters.Add(new Chapter
        {
            Number = 100,
            Title = "Chapter 100",
            Url = "https://example.com/chapter-100"
        });

        using var novelDataBuffer = new NovelDataBuffer
        {
            CurrentChapterUrl = "https://example.com/chapter-500",
            MostRecentChapterTitle = "Chapter 500"
        };
        var downloadedChapters = new List<Chapter>
        {
            new()
            {
                Number = 101,
                Title = "Chapter 101",
                Url = "https://example.com/chapter-101"
            }
        };

        NovelChapterStateUpdater.UpdateNovelWithDownloadedChapters(
            novel,
            novelDataBuffer,
            downloadedChapters);

        Assert.Equal("https://example.com/chapter-50", novel.FirstChapter);
        Assert.Equal("Chapter 101", novel.CurrentChapter);
        Assert.Equal("https://example.com/chapter-101", novel.CurrentChapterUrl);
        Assert.Equal(3, novel.TotalChapters);
    }

    [Fact]
    public void FillingAChapterGapMergesDownloadedRanges()
    {
        var novel = new Novel { Title = "Range Test" };
        var firstDownloadedRange = CreateChapterRange(novel.Id, 1, 100);
        firstDownloadedRange.VolumeName = "Old volume name";
        novel.ChapterRanges.Add(firstDownloadedRange);
        novel.ChapterRanges.Add(CreateChapterRange(novel.Id, 120, 130));

        NovelChapterStateUpdater.AddOrMergeDownloadedChapterRange(
            novel,
            new SelectedChapterRange(101, 119));

        var downloadedRange = Assert.Single(novel.ChapterRanges);
        Assert.Equal(1, downloadedRange.Begin);
        Assert.Equal(130, downloadedRange.End);
        Assert.Null(downloadedRange.VolumeName);
    }

    [Fact]
    public void RangeWithMissingChaptersRemainsSeparate()
    {
        var novel = new Novel { Title = "Range Test" };
        novel.ChapterRanges.Add(CreateChapterRange(novel.Id, 1, 100));

        NovelChapterStateUpdater.AddOrMergeDownloadedChapterRange(
            novel,
            new SelectedChapterRange(103, 110));

        Assert.Equal(2, novel.ChapterRanges.Count);
        Assert.Contains(novel.ChapterRanges, range => range.Begin == 1 && range.End == 100);
        Assert.Contains(novel.ChapterRanges, range => range.Begin == 103 && range.End == 110);
    }

    [Fact]
    public void DownloadingEveryAvailableChapterRemovesPartialRanges()
    {
        var novel = new Novel { Title = "Range Test" };
        novel.ChapterRanges.Add(CreateChapterRange(novel.Id, 1, 3));
        novel.Chapters.Add(new Chapter { Number = 3, Url = "chapter-3" });
        novel.Chapters.Add(new Chapter { Number = 1, Url = "chapter-1" });
        novel.Chapters.Add(new Chapter { Number = 2, Url = "chapter-2" });

        var rangesWereCleared = NovelChapterStateUpdater
            .ClearChapterRangesWhenAllAvailableChaptersAreDownloaded(novel, 3);

        Assert.True(rangesWereCleared);
        Assert.Empty(novel.ChapterRanges);
        Assert.False(novel.IsPartialDownload);
    }

    [Fact]
    public void FillingAnEarlierRangeKeepsChaptersInReadingOrder()
    {
        var novel = new Novel { Title = "Range Test" };
        novel.Chapters.Add(new Chapter
        {
            Number = 50,
            Title = "Chapter 50",
            DateCreated = DateTime.UtcNow.AddMinutes(-2)
        });
        novel.Chapters.Add(new Chapter
        {
            Number = 100,
            Title = "Chapter 100",
            DateCreated = DateTime.UtcNow.AddMinutes(-1)
        });
        novel.Chapters.Add(new Chapter
        {
            Number = 49,
            Title = "Chapter 49",
            DateCreated = DateTime.UtcNow
        });

        var orderedChapters = CommonHelper.SortNovelChaptersByNumber(novel.Chapters);

        Assert.Equal([49f, 50f, 100f], orderedChapters.Select(chapter => chapter.Number));
    }

    private static ChapterRange CreateChapterRange(Guid novelId, int begin, int end) => new()
    {
        NovelId = novelId,
        Begin = begin,
        End = end,
        DateCreated = DateTime.UtcNow
    };
}