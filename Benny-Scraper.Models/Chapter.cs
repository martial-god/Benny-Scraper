using System.ComponentModel.DataAnnotations;

namespace BennyScraper.Models;

/// <summary>
/// One to many relationship between Novel and Chapter. Each novel has many chapters, and each chapter belongs to one novel.
/// </summary>
public sealed class Chapter
{
    [Key]
    public Guid Id { get; set; }

    public Guid NovelId { get; set; }

    public Novel Novel { get; set; } = null!;

    [StringLength(255)]
    public string? Title { get; set; }

    public string Url { get; set; } = string.Empty;

    public string? Content { get; set; }

    public float Number { get; set; } // number used to sort chapters

    public DateTime DateCreated { get; set; }

    public DateTime DateLastModified { get; set; }

    public bool IsPartial { get; set; } // true if chapter contains teaser/preview content only

    public ICollection<Page>? Pages { get; private set; } // New property for manga pages

    public void SetPages(IEnumerable<Page>? pages) => Pages = pages?.ToList();
}