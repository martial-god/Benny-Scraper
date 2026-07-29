using System.Globalization;

namespace BennyScraper.BusinessLogic.Extensions;

public static class StringExtensions
{
    public static string ToLowerCase(this string str) =>
        string.IsNullOrEmpty(str) ? str : CultureInfo.InvariantCulture.TextInfo.ToLower(str);

    public static string ToTitleCase(this string str) =>
        string.IsNullOrEmpty(str) ? str : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(str.ToLowerCase());
}