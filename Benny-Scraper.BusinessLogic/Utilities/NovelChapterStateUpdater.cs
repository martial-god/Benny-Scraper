using BennyScraper.Models;

namespace BennyScraper.BusinessLogic.Utilities;

internal static class NovelChapterStateUpdater
{
    /// <summary>
    /// Updates the first and latest chapters available on the site after the complete chapter list has been ordered.
    /// Explicit latest-chapter values extracted by a site strategy take precedence over the list-derived fallback.
    /// </summary>
    /// <param name="novelDataBuffer">The scraped novel data containing the complete, ordered site chapter list.</param>
    internal static void UpdateAvailableChapterBoundaries(NovelDataBuffer novelDataBuffer)
    {
        ArgumentNullException.ThrowIfNull(novelDataBuffer);

        if (novelDataBuffer.ChapterLinks.Count == 0)
        {
            novelDataBuffer.FirstChapter = string.Empty;
            return;
        }

        var firstAvailableChapter = novelDataBuffer.ChapterLinks.First();
        var latestAvailableChapter = novelDataBuffer.ChapterLinks.Last();

        novelDataBuffer.FirstChapter = firstAvailableChapter.Url;

        if (string.IsNullOrWhiteSpace(novelDataBuffer.CurrentChapterUrl))
        {
            novelDataBuffer.CurrentChapterUrl = latestAvailableChapter.Url;
        }

        if (string.IsNullOrWhiteSpace(novelDataBuffer.MostRecentChapterTitle))
        {
            novelDataBuffer.MostRecentChapterTitle = latestAvailableChapter.Title ??
                                                     novelDataBuffer.ChapterTitles.LastOrDefault() ??
                                                     string.Empty;
        }
    }

    /// <summary>
    /// Adds newly downloaded chapters and updates the saved novel boundaries from all chapters actually stored.
    /// </summary>
    /// <param name="novel">The saved novel being updated.</param>
    /// <param name="downloadedChapters">The newly downloaded chapters to add.</param>
    internal static void AddDownloadedChaptersAndUpdateBoundaries(Novel novel, IEnumerable<Chapter> downloadedChapters)
    {
        ArgumentNullException.ThrowIfNull(novel);
        ArgumentNullException.ThrowIfNull(downloadedChapters);

        foreach (var downloadedChapter in downloadedChapters)
        {
            novel.Chapters.Add(downloadedChapter);
        }

        UpdateDownloadedChapterBoundaries(novel);
    }

    /// <summary>
    /// Updates a saved novel with newly downloaded chapters and the latest non-chapter metadata from the site.
    /// The saved chapter boundaries are derived from stored chapters rather than the site's latest available chapter.
    /// </summary>
    /// <param name="novel">The saved novel being updated.</param>
    /// <param name="novelDataBuffer">The latest site data.</param>
    /// <param name="downloadedChapters">The chapters downloaded during this update.</param>
    internal static void UpdateNovelWithDownloadedChapters(
        Novel novel,
        NovelDataBuffer novelDataBuffer,
        IEnumerable<Chapter> downloadedChapters)
    {
        ArgumentNullException.ThrowIfNull(novel);
        ArgumentNullException.ThrowIfNull(novelDataBuffer);
        ArgumentNullException.ThrowIfNull(downloadedChapters);

        AddDownloadedChaptersAndUpdateBoundaries(novel, downloadedChapters);
        novel.LastTableOfContentsUrl = !string.IsNullOrEmpty(novelDataBuffer.LastTableOfContentsPageUrl)
            ? novelDataBuffer.LastTableOfContentsPageUrl
            : novel.LastTableOfContentsUrl;
        novel.Status = !string.IsNullOrEmpty(novelDataBuffer.NovelStatus)
            ? novelDataBuffer.NovelStatus
            : novel.Status;
        novel.LastChapter = novelDataBuffer.IsNovelCompleted;
        novel.DateLastModified = DateTime.Now;

        if (!string.IsNullOrEmpty(novelDataBuffer.NovelUrl))
        {
            novel.Url = novelDataBuffer.NovelUrl;
        }

        if (novelDataBuffer.Genres.Count != 0)
        {
            novel.Genre = string.Join(", ", novelDataBuffer.Genres);
        }
    }

    /// <summary>
    /// Adds a downloaded chapter range and merges every overlapping or exactly adjacent range.
    /// A range separated by one or more missing chapters remains separate.
    /// </summary>
    /// <param name="novel">The saved novel whose downloaded ranges should be updated.</param>
    /// <param name="selectedRange">The range downloaded during the current operation.</param>
    internal static void AddOrMergeDownloadedChapterRange(
        Novel novel,
        SelectedChapterRange selectedRange)
    {
        ArgumentNullException.ThrowIfNull(novel);
        ArgumentNullException.ThrowIfNull(selectedRange);

        var connectedRanges = new List<ChapterRange>();
        var mergedBegin = selectedRange.Begin;
        var mergedEnd = selectedRange.End;
        var connectedRangeWasFound = true;

        while (connectedRangeWasFound)
        {
            connectedRangeWasFound = false;

            foreach (var chapterRange in novel.ChapterRanges
                         .Except(connectedRanges)
                         .OrderBy(range => range.Begin))
            {
                var overlapsOrIsAdjacent = mergedBegin <= chapterRange.End + 1 &&
                                           mergedEnd >= chapterRange.Begin - 1;
                if (!overlapsOrIsAdjacent)
                {
                    continue;
                }

                connectedRanges.Add(chapterRange);
                mergedBegin = Math.Min(mergedBegin, chapterRange.Begin);
                mergedEnd = Math.Max(mergedEnd, chapterRange.End);
                connectedRangeWasFound = true;
            }
        }

        if (connectedRanges.Count == 0)
        {
            novel.ChapterRanges.Add(new ChapterRange
            {
                NovelId = novel.Id,
                Begin = selectedRange.Begin,
                End = selectedRange.End,
                DateCreated = DateTime.Now
            });
            return;
        }

        var mergedRange = connectedRanges.First();
        var previousBegin = mergedRange.Begin;
        var previousEnd = mergedRange.End;
        mergedRange.Begin = mergedBegin;
        mergedRange.End = mergedEnd;

        if (mergedRange.Begin != previousBegin || mergedRange.End != previousEnd || connectedRanges.Count > 1)
        {
            mergedRange.VolumeName = null;
        }

        foreach (var redundantRange in connectedRanges.Skip(1))
        {
            novel.ChapterRanges.Remove(redundantRange);
        }
    }

    /// <summary>
    /// Updates the saved novel boundaries from the lowest- and highest-numbered chapters actually stored.
    /// </summary>
    /// <param name="novel">The saved novel whose downloaded chapter state should be synchronized.</param>
    internal static void UpdateDownloadedChapterBoundaries(Novel novel)
    {
        ArgumentNullException.ThrowIfNull(novel);

        var orderedDownloadedChapters = novel.Chapters
            .OrderBy(chapter => chapter.Number)
            .ThenBy(chapter => chapter.DateCreated)
            .ToList();

        var firstDownloadedChapter = orderedDownloadedChapters.FirstOrDefault();
        var latestDownloadedChapter = orderedDownloadedChapters.LastOrDefault();

        novel.FirstChapter = firstDownloadedChapter?.Url ?? string.Empty;
        novel.CurrentChapter = latestDownloadedChapter?.Title ?? string.Empty;
        novel.CurrentChapterUrl = latestDownloadedChapter?.Url ?? string.Empty;
        novel.TotalChapters = orderedDownloadedChapters.Count;
    }

    /// <summary>
    /// Clears partial-download ranges when every chapter currently available on the site is stored.
    /// </summary>
    /// <param name="novel">The saved novel whose downloaded coverage should be evaluated.</param>
    /// <param name="availableChapterCount">The number of chapters currently available on the site.</param>
    /// <returns>True when stale partial ranges were cleared; otherwise, false.</returns>
    internal static bool ClearChapterRangesWhenAllAvailableChaptersAreDownloaded(
        Novel novel,
        int availableChapterCount)
    {
        ArgumentNullException.ThrowIfNull(novel);

        if (availableChapterCount <= 0 || novel.ChapterRanges.Count == 0)
        {
            return false;
        }

        var downloadedChapterNumbers = novel.Chapters
            .Select(chapter => (int)chapter.Number)
            .ToHashSet();
        var hasCompleteCoverage = Enumerable
            .Range(1, availableChapterCount)
            .All(downloadedChapterNumbers.Contains);

        if (!hasCompleteCoverage)
        {
            return false;
        }

        novel.ChapterRanges.Clear();
        return true;
    }
}