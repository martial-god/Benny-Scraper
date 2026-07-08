using System.ComponentModel.DataAnnotations;

namespace BennyScraper.Models;

public class Page
{
    [Key]
    public int Id { get; set; }

    public Guid ChapterId { get; set; }

    public virtual Chapter Chapter { get; set; } = null!; // Lazy loaded, on demand

    public string Url { get; set; } = string.Empty;

    public byte[]? Image { get; set; }
}