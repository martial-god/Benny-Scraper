using System.Globalization;
using System.Text.RegularExpressions;

namespace BennyScraper.Models;

public sealed class ChapterDataBuffer : IDisposable
{
    public string Url { get; set; } = string.Empty;

    public string? Content { get; set; }

    public string Title { get; set; } = string.Empty;

    public float Number
    {
        get
        {
            if (string.IsNullOrEmpty(Title))
            {
                return 0f;
            }

            var digitMatch = Regex.Match(Title, @"[+-]?([0-9]*[.])?[0-9]+");
            return digitMatch.Success ? float.Parse(digitMatch.Groups[0].Value, CultureInfo.InvariantCulture) : 0f;
        }
    }

    public int SequenceNumber { get; set; } // using the Table of Contents order as the definitive order of chapters, this avoids issues with sorting by chapter title where titles contain numbers

    public DateTime DateLastModified { get; set; }

    public bool IsPartial { get; set; } // true if chapter contains teaser/preview content only

    public ICollection<PageData>? Pages { get; private set; }

    public string TempDirectory { get; init; } = string.Empty;

    public void SetPages(IEnumerable<PageData>? pages) => Pages = pages?.ToList();

    public void Dispose()
    {
        if (Pages != null)
        {
            foreach (var page in Pages)
            {
                page.ImagePath = null!;
            }
        }

        GC.SuppressFinalize(this);
    }
}