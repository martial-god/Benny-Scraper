using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace BennyScraper.Models;

public class Page
{
    [Key]
    public int Id { get; set; }

    public Guid ChapterId { get; set; }

    public virtual Chapter Chapter { get; set; } = null!; // Lazy loaded, on demand

    public string Url { get; set; } = string.Empty;

    public ReadOnlyCollection<byte>? Image { get; private set; }

    public void SetImage(byte[]? bytes) => Image = bytes is null ? null : new ReadOnlyCollection<byte>(bytes);
}