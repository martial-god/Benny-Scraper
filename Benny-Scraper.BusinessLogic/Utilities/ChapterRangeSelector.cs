using System.Globalization;
using BennyScraper.BusinessLogic.Extensions;
using BennyScraper.BusinessLogic.Helper;
using BennyScraper.Models;
using NLog;

namespace BennyScraper.BusinessLogic.Utilities;

internal static class ChapterRangeSelector
{
    private const int _defaultMaxDisplayCount = 20;
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Displays the selected chapter range for CLI mode.
    /// </summary>
    /// <param name="range">The chapter range that was selected.</param>
    /// <param name="chapterTitles">The list of chapter titles used to display the first and last chapter in the range.</param>
    public static void DisplayRangeInfo(SelectedChapterRange range, IReadOnlyList<string> chapterTitles)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\nSelected chapters: {range}");
        if (chapterTitles.Count >= range.End)
        {
            var firstChapter = chapterTitles[range.Begin - 1];
            var lastChapter = chapterTitles[range.End - 1];

            Console.WriteLine($"  Begin: {firstChapter}");
            Console.WriteLine($"  End:   {lastChapter}");
        }

        Console.ResetColor();
    }

    /// <summary>
    /// Prompts user to confirm premium chapter unlock for selected range.
    /// Returns true if user wants to unlock premium chapters, false if they want teasers only.
    /// </summary>
    /// <param name="range">The chapter range selected for download.</param>
    /// <param name="chapterLinks">The full list of chapter links, used to look up premium chapters within the range.</param>
    /// <param name="userCurrencies">The user's premium currency balances, used to display the current balance; null if not available.</param>
    /// <param name="isLoggedIn">Whether the user is currently authenticated with the site.</param>
    public static void ConfirmPremiumChapters(SelectedChapterRange range, IList<ChapterLink> chapterLinks, IList<UserPremiumCurrency>? userCurrencies, bool isLoggedIn)
    {
        var selectedLinks = chapterLinks.Skip(range.Begin - 1).Take(range.Count).ToList();
        var selectedPremium = selectedLinks.Where(cl => cl.PremiumInfo.IsPremium).ToList();

        if (selectedPremium.Count == 0)
        {
            _logger.Debug("No premium chapters in selected range");
            return;
        }

        var headerMessages = new[] { "Premium Chapters Detected" };
        CommonHelper.DrawBox(headerMessages, ConsoleColor.Yellow);

        Console.WriteLine($"\nThe following premium chapters were found in your selection:\n");

        foreach (var premiumChapter in selectedPremium)
        {
            var chapterIndex = chapterLinks.IndexOf(premiumChapter) + 1;
            var currency = premiumChapter.PremiumInfo?.CurrencyName ?? "Credits";
            var title = string.IsNullOrWhiteSpace(premiumChapter.Title)
                ? $"Chapter {chapterIndex}"
                : premiumChapter.Title;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [{chapterIndex,4}] [Premium {premiumChapter.PremiumInfo!.Cost} {currency}] {title}");
            Console.ResetColor();
        }

        var premiumSummary = BuildPremiumSummary(selectedPremium);

        if (!isLoggedIn)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            var notLoggedInMessages = new[]
            {
                "⚠️  PREMIUM CHAPTERS DETECTED - NOT LOGGED IN  ⚠️",
                string.Empty,
                $"Your selection includes {selectedPremium.Count} premium chapter(s).",
                string.Empty,
                "Since you are NOT logged in, premium chapters will be downloaded as",
                "TEASERS ONLY (limited preview content).",
                string.Empty,
                "To download full premium chapters:",
                "  1. Stop the program now (Ctrl+C)",
                "  2. Re-run with the --with-login flag:",
                "     Benny-Scraper <url> --with-login",
                string.Empty,
                "Press Enter to continue with teasers, or Ctrl+C to stop."
            };
            CommonHelper.DrawBox(notLoggedInMessages, ConsoleColor.Yellow);
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nTotal: {selectedPremium.Count} premium chapters ({premiumSummary})");
            Console.WriteLine("These will be downloaded as TEASERS ONLY.");
            Console.ResetColor();
            Console.Write("\nPress Enter to continue with teasers: ");
            Console.ReadLine();
            _logger.Info("User acknowledged premium chapters will be teasers (not logged in)");
        }
        else
        {
            // User is logged in, show credit consumption warning
            Console.ForegroundColor = ConsoleColor.Red;
            var warningMessages = new[]
            {
                "⚠️  WARNING: UNLOCKING PREMIUM CHAPTERS WILL COST YOU CREDITS!  ⚠️",
                string.Empty,
                "If you have 'Enabled Auto Unlock' set to true, premium chapters will be",
                "automatically unlocked for you using your account credits/karma.",
                string.Empty,
                "If you DO NOT want your credits consumed, you should:",
                "  1. Stop the program now (Ctrl+C)",
                "  2. Change the chapter range to exclude premium chapters",
                string.Empty,
                "For instructions, visit:",
                "https://github.com/martial-god/Benny-Scraper#quick-start"
            };
            CommonHelper.DrawBox(warningMessages, ConsoleColor.Red);
            Console.ResetColor();

            var karmaBalance = userCurrencies?.Where(uc => uc.CurrencyName == "Karma")?.FirstOrDefault()?.Balance;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nTotal: {selectedPremium.Count} premium chapters ({premiumSummary})");
            Console.WriteLine($"Current premium currency balance: {karmaBalance?.ToString("N0", CultureInfo.InvariantCulture)} Karma");
            Console.Write("\nType 'continue' to proceed with premium chapters, or press Enter to proceed without unlocking (they will remain teasers): ");
            Console.ResetColor();

            var premiumChoice = Console.ReadLine()?.Trim();
            if (string.Equals(premiumChoice, "continue", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info("User confirmed proceeding with premium chapters (unlock flow not implemented)");
                return;
            }

            _logger.Info("User did not confirm premium unlock; proceeding without unlocking premium chapters");
        }
    }

    /// <summary>
    /// Gets a chapter range from command line options, validating it against the total chapter count.
    /// </summary>
    /// <param name="totalChapters">The total number of chapters available for the novel.</param>
    /// <param name="begin">The requested starting chapter number; defaults to 1 if not specified.</param>
    /// <param name="end">The requested ending chapter number; defaults to <paramref name="totalChapters"/> if not specified.</param>
    /// <returns>The validated chapter range built from the requested begin and end values.</returns>
    public static SelectedChapterRange GetRangeFromOptions(int totalChapters, int? begin, int? end)
    {
        var startChapter = begin ?? 1;
        var endChapter = end ?? totalChapters;

        _logger.Debug($"Creating chapter range from CLI options: Begin={startChapter}, End={endChapter}, Total={totalChapters}");

        if (!ValidateRange(startChapter, endChapter, totalChapters))
        {
            var errorMessages = new[] { "Invalid Chapter Range" };
            CommonHelper.DrawBox(errorMessages, ConsoleColor.Red);

            Console.WriteLine($"\nYour chapter range selection is out of bounds:");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Requested: Chapters {startChapter}-{endChapter}");
            Console.WriteLine($"  Available: Chapters 1-{totalChapters} ({totalChapters} total chapters)");
            Console.ResetColor();
            Console.WriteLine($"\nPlease adjust your chapter range using:");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  -B {Math.Min(startChapter, totalChapters)} -E {totalChapters}  # Download from chapter {Math.Min(startChapter, totalChapters)} to end");
            Console.WriteLine($"  -E {totalChapters}                    # Download first {totalChapters} chapters");
            Console.WriteLine($"  (no options)              # Download all chapters interactively");
            Console.ResetColor();

            _logger.Error($"Invalid chapter range: {startChapter}-{endChapter}. Total chapters: {totalChapters}. Exiting.");
            Environment.Exit(1);
        }

        var range = new SelectedChapterRange(startChapter, endChapter);
        _logger.Debug($"Chapter range created: {range}");
        return range;
    }

    /// <summary>
    /// Prompts the user interactively to select a range of chapters to download.
    /// </summary>
    /// <param name="chapterLinks">The full list of chapter links available on the table of contents page.</param>
    /// <param name="userCurrencies">The user's premium currency balances, used to display premium chapter costs; null if not available.</param>
    /// <param name="isLoggedIn">Whether the user is currently authenticated with the site.</param>
    /// <returns>The chapter range selected by the user, or null if all chapters should be downloaded.</returns>
    public static SelectedChapterRange? PromptUserForRange(IList<ChapterLink> chapterLinks, IList<UserPremiumCurrency>? userCurrencies, bool isLoggedIn)
    {
        if (chapterLinks.Count == 0)
        {
            Console.WriteLine("No chapters available.");
            return null;
        }

        var chapterTitles = chapterLinks
            .Select((cl, i) => string.IsNullOrWhiteSpace(cl.Title) ? $"Chapter {i + 1}" : cl.Title!)
            .ToList();

        Console.WriteLine("\nDownload specific chapter range? (y/N): Hit Enter for all chapters");
        var response = Console.ReadLine()?.Trim().ToLowerCase();

        if (response != "y" && response != "yes")
        {
            _logger.Debug("User declined chapter range selection, proceeding with all chapters");
            return null;
        }

        var totalChapters = chapterLinks.Count;

        DisplayChapterList(chapterLinks, totalChapters);
        Console.WriteLine("\nEnter chapter selection:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Examples: '1-50', '1,5,10-20', '25-100', 'all'");
        Console.WriteLine($"  Or press Enter for all chapters (1-{totalChapters})");
        Console.ResetColor();
        Console.Write("\nSelection: ");

        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("User pressed Enter, downloading all chapters");
            _logger.Debug("User pressed Enter, downloading all chapters");
            return null;
        }

        var selectedChapters = ParseFlexibleInput(input, totalChapters);
        if (selectedChapters.Count == 0)
        {
            _logger.Warn("No valid chapters selected");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("No valid chapters selected. Downloading all chapters.");
            Console.ResetColor();
            return null;
        }

        var begin = selectedChapters.Min();
        var end = selectedChapters.Max();
        var range = new SelectedChapterRange(begin, end);

        _logger.Info($"User selected {selectedChapters.Count} chapters, range: {range}");

        if (!ConfirmRange(range, chapterTitles, selectedChapters))
        {
            _logger.Info("User cancelled range selection");
            Console.WriteLine("Range selection cancelled. Downloading all chapters.");
            return null;
        }

        var selectedLinks = chapterLinks.Skip(range.Begin - 1).Take(range.Count).ToList();
        var selectedPremium = selectedLinks.Where(cl => cl.PremiumInfo.IsPremium).ToList();
        if (selectedPremium.Count != 0)
        {
            var premiumSummary = BuildPremiumSummary(selectedPremium);

            if (!isLoggedIn)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                var notLoggedInMessages = new[]
                {
                    "⚠️  PREMIUM CHAPTERS DETECTED - NOT LOGGED IN  ⚠️",
                    string.Empty,
                    $"Your selection includes {selectedPremium.Count} premium chapter(s).",
                    string.Empty,
                    "Since you are NOT logged in, premium chapters will be downloaded as",
                    "TEASERS ONLY (limited preview content).",
                    string.Empty,
                    "To download full premium chapters:",
                    "  1. Stop the program now (Ctrl+C)",
                    "  2. Re-run with the --with-login flag:",
                    "     Benny-Scraper <url> --with-login",
                    string.Empty,
                    "Press Enter to continue with teasers, or Ctrl+C to stop."
                };
                CommonHelper.DrawBox(notLoggedInMessages, ConsoleColor.Yellow);
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\nPremium chapters in selection: {selectedPremium.Count} ({premiumSummary})");
                Console.WriteLine("These will be downloaded as TEASERS ONLY.");
                Console.ResetColor();
                Console.Write("\nPress Enter to continue with teasers: ");
                Console.ReadLine();
                _logger.Info("User acknowledged premium chapters will be teasers (not logged in)");
            }
            else
            {
                // User is logged in, show credit consumption warning
                Console.ForegroundColor = ConsoleColor.Red;
                var warningMessages = new[]
                {
                    "⚠️  WARNING: UNLOCKING PREMIUM CHAPTERS WILL COST YOU CREDITS!  ⚠️",
                    string.Empty,
                    "If you have 'Enabled Auto Unlock' set to true, premium chapters will be",
                    "automatically unlocked for you using your account credits/karma.",
                    string.Empty,
                    "If you DO NOT want your credits consumed, you should:",
                    "  1. Stop the program now (Ctrl+C)",
                    "  2. Change the chapter range to exclude premium chapters",
                    string.Empty,
                    "For instructions, visit:",
                    "https://github.com/martial-god/Benny-Scraper#quick-start"
                };
                CommonHelper.DrawBox(warningMessages, ConsoleColor.Red);
                Console.ResetColor();

                var karmaBalance = userCurrencies?.Where(uc => uc.CurrencyName == "Karma")?.FirstOrDefault()?.Balance;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\nPremium chapters in selection: {selectedPremium.Count} ({premiumSummary})");
                Console.WriteLine($"Current premium currency balance: {karmaBalance?.ToString("N0", CultureInfo.InvariantCulture)} Karma");
                Console.ResetColor();
                Console.Write("\nType 'continue' to proceed with premium chapters, or press Enter to proceed without unlocking (they will remain teasers): ");

                var premiumChoice = Console.ReadLine()?.Trim();
                _logger.Info(string.Equals(premiumChoice, "continue", StringComparison.OrdinalIgnoreCase)
                    ? "User confirmed proceeding with premium chapters (unlock flow not implemented)"
                    : "User did not confirm premium unlock; proceeding without unlocking premium chapters");
            }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\nChapter range {range} confirmed.");
        Console.ResetColor();
        _logger.Debug($"Chapter range confirmed: {range}");
        return range;
    }

    /// <summary>
    /// Validates that the range is within bounds.
    /// </summary>
    /// <param name="begin">The requested starting chapter number.</param>
    /// <param name="end">The requested ending chapter number.</param>
    /// <param name="totalChapters">The total number of chapters available.</param>
    /// <returns>True if the range is within bounds and begin does not exceed end; otherwise, false.</returns>
    private static bool ValidateRange(int begin, int end, int totalChapters)
    {
        return begin >= 1 && begin <= totalChapters &&
               end >= 1 && end <= totalChapters &&
               begin <= end;
    }

    /// <summary>
    /// Parses flexible input formats like "1-50", "1,5,10-20", "all".
    /// </summary>
    /// <param name="input">The raw chapter selection text entered by the user.</param>
    /// <param name="totalChapters">The total number of chapters available, used to validate parsed values.</param>
    /// <returns>The sorted, de-duplicated list of chapter numbers described by <paramref name="input"/>.</returns>
    private static List<int> ParseFlexibleInput(string input, int totalChapters)
    {
        _logger.Debug($"Parsing flexible input: '{input}', Total chapters: {totalChapters}");

        if (string.IsNullOrWhiteSpace(input) || input.Trim().ToLowerCase() == "all")
        {
            _logger.Info("Input is 'all' or empty, returning all chapters");
            return Enumerable.Range(1, totalChapters).ToList();
        }

        var chapters = new HashSet<int>();
        var parts = input.Split(',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            if (trimmed.Contains('-', StringComparison.Ordinal))
            {
                var rangeParts = trimmed.Split('-');
                if (rangeParts.Length != 2 ||
                    !int.TryParse(rangeParts[0].Trim(), out var rangeStart) ||
                    !int.TryParse(rangeParts[1].Trim(), out var rangeEnd))
                {
                    continue;
                }

                if (rangeStart < 1 || rangeEnd > totalChapters || rangeStart > rangeEnd)
                {
                    _logger.Warn($"Invalid range: {rangeStart}-{rangeEnd}");
                    continue;
                }

                for (var i = rangeStart; i <= rangeEnd; i++)
                {
                    chapters.Add(i);
                }

                _logger.Debug($"Added range {rangeStart}-{rangeEnd}");
                continue;
            }

            if (!int.TryParse(trimmed, out var singleChapter))
            {
                continue;
            }

            if (singleChapter < 1 || singleChapter > totalChapters)
            {
                _logger.Warn($"Invalid chapter number: {singleChapter}");
                continue;
            }

            chapters.Add(singleChapter);
            _logger.Debug($"Added chapter {singleChapter}");
        }

        var result = chapters.OrderBy(c => c).ToList();
        _logger.Info($"Parsed {result.Count} chapters from flexible input");
        return result;
    }

    /// <summary>
    /// Displays the chapter list with smart pagination.
    /// </summary>
    /// <param name="chapterLinks">The full list of chapter links to display.</param>
    /// <param name="totalChapters">The total number of chapters available.</param>
    /// <param name="maxDisplay">The maximum number of chapters to display before truncating the list.</param>
    private static void DisplayChapterList(IList<ChapterLink> chapterLinks, int totalChapters, int maxDisplay = _defaultMaxDisplayCount)
    {
        _logger.Debug($"Displaying chapter list. Total: {totalChapters}, MaxDisplay: {maxDisplay}");
        Console.WriteLine($"\nAvailable Chapters (1-{totalChapters}):\n");

        if (totalChapters <= maxDisplay)
        {
            for (var i = 0; i < chapterLinks.Count; i++)
            {
                Console.WriteLine(FormatChapterLine(i, chapterLinks[i]));
            }

            PrintPremiumSummary(chapterLinks);
            return;
        }

        var showCount = maxDisplay / 2;
        Console.WriteLine($"Showing first {showCount}:");
        for (var i = 0; i < showCount && i < chapterLinks.Count; i++)
        {
            Console.WriteLine($"  {FormatChapterLine(i, chapterLinks[i])}");
        }

        Console.WriteLine("  ...");

        Console.WriteLine($"\nShowing last {showCount}:");
        var startIndex = Math.Max(0, totalChapters - showCount);
        for (var i = startIndex; i < chapterLinks.Count; i++)
        {
            Console.WriteLine($"  {FormatChapterLine(i, chapterLinks[i])}");
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("\n(Type 'list' to see all chapters, or press Enter to continue)");
        Console.ResetColor();

        var listChoice = Console.ReadLine()?.Trim().ToLowerCase();
        if (listChoice != "list")
        {
            PrintPremiumSummary(chapterLinks);
            return;
        }

        _logger.Info("User requested full chapter list");
        Console.WriteLine("\nAll Chapters:\n");
        for (var i = 0; i < chapterLinks.Count; i++)
        {
            Console.WriteLine($"  {FormatChapterLine(i, chapterLinks[i])}");
        }

        PrintPremiumSummary(chapterLinks);
    }

    private static string FormatChapterLine(int index, ChapterLink link)
    {
        var currency = link.PremiumInfo?.CurrencyName ?? "Credits";
        var premiumTag = link.PremiumInfo!.IsPremium
            ? $"[Premium {link.PremiumInfo.Cost} {currency}]"
            : "[Free]";
        var title = string.IsNullOrWhiteSpace(link.Title) ? $"Chapter {index + 1}" : link.Title;
        return $"[{index + 1,4}] {premiumTag} {title}";
    }

    private static void PrintPremiumSummary(IList<ChapterLink> chapterLinks)
    {
        var summary = BuildPremiumSummary(chapterLinks);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\nPremium summary (all chapters): {summary}\n");
        Console.ResetColor();
    }

    private static string BuildPremiumSummary(IEnumerable<ChapterLink> links)
    {
        var premiumGroups = links
            .Where(cl => cl.PremiumInfo.IsPremium)
            .GroupBy(cl => cl.PremiumInfo.CurrencyName ?? "Credits")
            .ToList();

        if (premiumGroups.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", premiumGroups.Select(g =>
            $"{g.Count().ToString("N0", CultureInfo.InvariantCulture)} premium, total {g.Sum(cl => cl.PremiumInfo.Cost).ToString("N0", CultureInfo.InvariantCulture)} {g.Key}"));
    }

    /// <summary>
    /// Shows confirmation of the selected range.
    /// </summary>
    /// <param name="range">The chapter range to confirm.</param>
    /// <param name="chapterTitles">The list of chapter titles used to display the first and last chapter in the range.</param>
    /// <param name="selectedChapters">The individual chapter numbers the user selected, if the selection was non-contiguous.</param>
    /// <returns>True if the user confirmed the range; otherwise, false.</returns>
    private static bool ConfirmRange(SelectedChapterRange range, List<string> chapterTitles, List<int>? selectedChapters = null)
    {
        Console.WriteLine("\nSelected Range:");
        Console.ForegroundColor = ConsoleColor.Cyan;

        var firstChapter = range.Begin - 1 < chapterTitles.Count
            ? chapterTitles[range.Begin - 1]
            : $"Chapter {range.Begin}";

        var lastChapter = range.End - 1 < chapterTitles.Count
            ? chapterTitles[range.End - 1]
            : $"Chapter {range.End}";

        Console.WriteLine($"  Begin: [{range.Begin,4}] {firstChapter}");
        Console.WriteLine($"  End:   [{range.End,4}] {lastChapter}");

        if (selectedChapters != null && selectedChapters.Count != range.Count)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n  Note: You selected {selectedChapters.Count} specific chapters,");
            Console.WriteLine($"        but will download all {range.Count} chapters in range {range.Begin}-{range.End}");
            Console.WriteLine($"        (Non-contiguous selection is converted to full range)");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Cyan;
        }

        Console.WriteLine($"  Total chapters to download: {range.Count}");
        Console.ResetColor();

        Console.Write("\nProceed? (Y/n): ");
        var response = Console.ReadLine()?.Trim().ToLowerCase();

        var isConfirmed = string.IsNullOrEmpty(response) || response == "y" || response == "yes";
        _logger.Debug($"Range confirmation: {(isConfirmed ? "accepted" : "declined")}");
        return isConfirmed;
    }
}