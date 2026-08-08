namespace BennyScraper.BusinessLogic.Utilities;

public class SelectedChapterRange(int begin, int end)
{
    public int Begin { get; set; } = begin;

    public int End { get; set; } = end;

    public int Count => End - Begin + 1;

    public bool IsValid() => Begin > 0 && End > 0 && Begin <= End;

    public override string ToString() => $"{Begin}-{End} ({Count} chapters)";
}