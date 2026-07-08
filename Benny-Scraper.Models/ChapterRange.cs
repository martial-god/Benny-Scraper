using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BennyScraper.Models;

public class ChapterRange
{
    [Key]
    public Guid Id { get; init; }

    [Required]
    public Guid NovelId { get; init; }

    [ForeignKey("NovelId")]
    public Novel Novel { get; init; } = null!;

    [Required]
    public int Begin { get; set; }

    [Required]
    public int End { get; set; }

    public DateTime DateCreated { get; init; }

    /// <summary>
    /// Gets or sets the optional volume name if this range represents a volume.
    /// </summary>
    public string? VolumeName { get; set; }
}