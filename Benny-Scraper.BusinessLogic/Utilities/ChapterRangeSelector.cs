using Benny_Scraper.Models;
using NLog;
using System.Text.RegularExpressions;

namespace Benny_Scraper.BusinessLogic.Utilities
{
    public class ChapterRangeSelector
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private const int DefaultMaxDisplay = 20;

        /// <summary>
        /// Prompts user interactively for chapter range selection
        /// </summary>
        public ChapterRange? PromptUserForRange(List<ChapterLink> chapterLinks, List<UserPremiumCurrency>? userCurrencies)
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
            var response = Console.ReadLine()?.Trim().ToLower();

            if (response != "y" && response != "yes")
            {
                Logger.Debug("User declined chapter range selection, proceeding with all chapters");
                return null;
            }
            Console.WriteLine("\nFetching chapter list... (this may take a moment)");

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
                Logger.Info("User pressed Enter, downloading all chapters");
                return null;
            }

            var selectedChapters = ParseFlexibleInput(input, totalChapters);
            if (selectedChapters.Count == 0)
            {
                Logger.Warn("No valid chapters selected");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No valid chapters selected. Downloading all chapters.");
                Console.ResetColor();
                return null;
            }

            var begin = selectedChapters.Min();
            var end = selectedChapters.Max();
            var range = new ChapterRange(begin, end);

            Logger.Info($"User selected {selectedChapters.Count} chapters, range: {range}");

            if (!ConfirmRange(range, chapterTitles, selectedChapters))
            {
                Logger.Info("User cancelled range selection");
                Console.WriteLine("Range selection cancelled. Downloading all chapters.");
                return null;
            }

            var selectedLinks = chapterLinks.Skip(range.Begin - 1).Take(range.Count).ToList();
            var selectedPremium = selectedLinks.Where(cl => cl.PremiumInfo.IsPremium).ToList();
            if (selectedPremium.Count != 0)
            {
                var premiumSummary = BuildPremiumSummary(selectedPremium);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\nPremium chapters in selection: {selectedPremium.Count} ({premiumSummary})");
                Console.WriteLine($"Current premium currency balance: " +
                                  $"{userCurrencies?.Where(uc => uc.CurrencyName == "Karma")?.FirstOrDefault()?.Balance:N0} Karma");
                Console.Write("Type 'continue' to proceed with premium chapters, or press Enter to proceed without unlocking (they will remain teasers): ");
                Console.ResetColor();
                var premiumChoice = Console.ReadLine()?.Trim();
                Logger.Info(string.Equals(premiumChoice, "continue", StringComparison.OrdinalIgnoreCase)
                    ? "User confirmed proceeding with premium chapters (unlock flow not implemented)"
                    : "User did not confirm premium unlock; proceeding without unlocking premium chapters");
            }

            Logger.Info($"Chapter range confirmed: {range}");
            return range;
        }

        /// <summary>
        /// Gets range from command line options
        /// </summary>
        public ChapterRange GetRangeFromOptions(int totalChapters, int? begin, int? end)
        {
            var startChapter = begin ?? 1;
            var endChapter = end ?? totalChapters;

            Logger.Info($"Creating chapter range from CLI options: Begin={startChapter}, End={endChapter}, Total={totalChapters}");

            if (!ValidateRange(startChapter, endChapter, totalChapters))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n╔═══════════════════════════════════════════════════╗");
                Console.WriteLine($"║              Invalid Chapter Range                ║");
                Console.WriteLine($"╚═══════════════════════════════════════════════════╝");
                Console.ResetColor();
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

                Logger.Error($"Invalid chapter range: {startChapter}-{endChapter}. Total chapters: {totalChapters}. Exiting.");
                Environment.Exit(1);
            }

            var range = new ChapterRange(startChapter, endChapter);
            Logger.Info($"Chapter range created: {range}");
            return range;
        }

        /// <summary>
        /// Parses flexible input formats like "1-50", "1,5,10-20", "all"
        /// </summary>
        public List<int> ParseFlexibleInput(string input, int totalChapters)
        {
            Logger.Debug($"Parsing flexible input: '{input}', Total chapters: {totalChapters}");

            if (string.IsNullOrWhiteSpace(input) || input.Trim().ToLower() == "all")
            {
                Logger.Info("Input is 'all' or empty, returning all chapters");
                return Enumerable.Range(1, totalChapters).ToList();
            }

            var chapters = new HashSet<int>();
            var parts = input.Split(',');

            foreach (var part in parts)
            {
                var trimmed = part.Trim();

                if (trimmed.Contains('-'))
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
                        Logger.Warn($"Invalid range: {rangeStart}-{rangeEnd}");
                        continue;
                    }

                    for (var i = rangeStart; i <= rangeEnd; i++)
                    {
                        chapters.Add(i);
                    }
                    Logger.Debug($"Added range {rangeStart}-{rangeEnd}");
                    continue;
                }

                if (!int.TryParse(trimmed, out int singleChapter))
                {
                    continue;
                }

                if (singleChapter < 1 || singleChapter > totalChapters)
                {
                    Logger.Warn($"Invalid chapter number: {singleChapter}");
                    continue;
                }

                chapters.Add(singleChapter);
                Logger.Debug($"Added chapter {singleChapter}");
            }

            var result = chapters.OrderBy(c => c).ToList();
            Logger.Info($"Parsed {result.Count} chapters from flexible input");
            return result;
        }

        /// <summary>
        /// Displays chapter list with smart pagination
        /// </summary>
        private static void DisplayChapterList(List<ChapterLink> chapterLinks, int totalChapters, int maxDisplay = DefaultMaxDisplay)
        {
            Logger.Debug($"Displaying chapter list. Total: {totalChapters}, MaxDisplay: {maxDisplay}");
            Console.WriteLine($"\nAvailable Chapters (1-{totalChapters}):\n");

            string FormatLine(int index, ChapterLink link)
            {
                var currency = link.PremiumInfo?.CurrencyName ?? "Credits";
                var premiumTag = link.PremiumInfo.IsPremium
                    ? $"[Premium {link.PremiumInfo.Cost} {currency}]"
                    : "[Free]";
                var title = string.IsNullOrWhiteSpace(link.Title) ? $"Chapter {index + 1}" : link.Title;
                return $"[{index + 1,4}] {premiumTag} {title}";
            }

            void PrintPremiumTotals()
            {
                var summary = BuildPremiumSummary(chapterLinks);
                if (string.IsNullOrWhiteSpace(summary)) return;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nPremium summary (all chapters): {summary}\n");
                Console.ResetColor();
            }

            if (totalChapters <= maxDisplay)
            {
                for (var i = 0; i < chapterLinks.Count; i++)
                {
                    Console.WriteLine(FormatLine(i, chapterLinks[i]));
                }
                PrintPremiumTotals();
                return;
            }

            var showCount = maxDisplay / 2;

            Console.WriteLine($"Showing first {showCount}:");
            for (var i = 0; i < showCount && i < chapterLinks.Count; i++)
            {
                Console.WriteLine($"  {FormatLine(i, chapterLinks[i])}");
            }

            Console.WriteLine("  ...");

            Console.WriteLine($"\nShowing last {showCount}:");
            var startIndex = Math.Max(0, totalChapters - showCount);
            for (var i = startIndex; i < chapterLinks.Count; i++)
            {
                Console.WriteLine($"  {FormatLine(i, chapterLinks[i])}");
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n(Type 'list' to see all chapters, or press Enter to continue)");
            Console.ResetColor();

            var listChoice = Console.ReadLine()?.Trim().ToLower();
            if (listChoice != "list")
            {
                PrintPremiumTotals();
                return;
            }

            Logger.Info("User requested full chapter list");
            Console.WriteLine("\nAll Chapters:\n");
            for (var i = 0; i < chapterLinks.Count; i++)
            {
                Console.WriteLine($"  {FormatLine(i, chapterLinks[i])}");
            }

            PrintPremiumTotals();
        }

        private static string BuildPremiumSummary(IEnumerable<ChapterLink> links)
        {
            var premiumGroups = links
                .Where(cl => cl.PremiumInfo.IsPremium)
                .GroupBy(cl => cl.PremiumInfo.CurrencyName ?? "Credits")
                .ToList();

            if (!premiumGroups.Any()) return string.Empty;

            return string.Join(", ", premiumGroups.Select(g =>
                $"{g.Count():N0} premium, total {g.Sum(cl => cl.PremiumInfo.Cost):N0} {g.Key}"));
        }

        /// <summary>
        /// Detects volume boundaries from chapter titles
        /// </summary>
        private List<VolumeRange> DetectVolumes(List<string> chapterTitles)
        {
            Logger.Debug("Detecting volume boundaries from chapter titles");
            var volumes = new List<VolumeRange>();
            var volumePatterns = new[]
            {
                 @"volume\s*(\d+)",
                 @"vol\.?\s*(\d+)",
                 @"book\s*(\d+)",
                 @"part\s*(\d+)",
                 @"v(\d+)",
                 @"\(v(\d+)\)"
             };

            int? currentVolumeStart = null;
            int? currentVolumeNumber = null;

            for (int i = 0; i < chapterTitles.Count; i++)
            {
                var title = chapterTitles[i].ToLower();
                int? detectedVolume = null;

                foreach (var pattern in volumePatterns)
                {
                    var match = Regex.Match(title, pattern, RegexOptions.IgnoreCase);
                    if (!match.Success || match.Groups.Count <= 1)
                    {
                        continue;
                    }

                    if (!int.TryParse(match.Groups[1].Value, out int volNum))
                    {
                        continue;
                    }

                    detectedVolume = volNum;
                    break;
                }

                if (!detectedVolume.HasValue)
                {
                    continue;
                }

                if (currentVolumeStart.HasValue)
                {
                    currentVolumeStart = i + 1;
                    currentVolumeNumber = detectedVolume.Value;
                    continue;
                }

                if (detectedVolume.Value == currentVolumeNumber.Value)
                {
                    continue;
                }

                if (currentVolumeStart.HasValue)
                {
                    volumes.Add(new VolumeRange
                    {
                        Begin = currentVolumeStart.Value,
                        End = i,
                        Name = $"Volume {currentVolumeNumber.Value}"
                    });
                    Logger.Debug($"Detected volume {currentVolumeNumber.Value}: chapters {currentVolumeStart.Value}-{i}");
                }

                currentVolumeStart = i + 1;
                currentVolumeNumber = detectedVolume.Value;
            }

            if (currentVolumeStart.HasValue && currentVolumeNumber.HasValue)
            {
                volumes.Add(new VolumeRange
                {
                    Begin = currentVolumeStart.Value,
                    End = chapterTitles.Count,
                    Name = $"Volume {currentVolumeNumber.Value}"
                });
                Logger.Debug($"Detected volume {currentVolumeNumber.Value}: chapters {currentVolumeStart.Value}-{chapterTitles.Count}");
            }

            Logger.Info($"Volume detection complete. Found {volumes.Count} volumes");
            return volumes;
        }

        /// <summary>
        /// Shows confirmation of selected range
        /// </summary>
        private bool ConfirmRange(ChapterRange range, List<string> chapterTitles, List<int>? selectedChapters = null)
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
            var response = Console.ReadLine()?.Trim().ToLower();

            var isConfirmed = string.IsNullOrEmpty(response) || response == "y" || response == "yes";
            Logger.Debug($"Range confirmation: {(isConfirmed ? "accepted" : "declined")}");
            return isConfirmed;
        }

        /// <summary>
        /// Validates that the range is within bounds
        /// </summary>
        private bool ValidateRange(int begin, int end, int totalChapters)
        {
            return begin >= 1 && begin <= totalChapters &&
                   end >= 1 && end <= totalChapters &&
                   begin <= end;
        }

        /// <summary>
        /// Displays selected range for CLI mode
        /// </summary>
        public void DisplayRangeInfo(ChapterRange range, List<string> chapterTitles)
        {
            Logger.Info($"Displaying range info for CLI mode: {range}");
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
        public bool ConfirmPremiumChapters(ChapterRange range, List<ChapterLink> chapterLinks, List<UserPremiumCurrency>? userCurrencies)
        {
            var selectedLinks = chapterLinks.Skip(range.Begin - 1).Take(range.Count).ToList();
            var selectedPremium = selectedLinks.Where(cl => cl.PremiumInfo.IsPremium).ToList();

            if (!selectedPremium.Any())
            {
                Logger.Debug("No premium chapters in selected range");
                return false;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n╔═══════════════════════════════════════════════════╗");
            Console.WriteLine($"║           Premium Chapters Detected               ║");
            Console.WriteLine($"╚═══════════════════════════════════════════════════╝");
            Console.ResetColor();

            Console.WriteLine($"\nThe following premium chapters were found in your selection:\n");

            foreach (var premiumChapter in selectedPremium)
            {
                var chapterIndex = chapterLinks.IndexOf(premiumChapter) + 1;
                var currency = premiumChapter.PremiumInfo?.CurrencyName ?? "Credits";
                var title = string.IsNullOrWhiteSpace(premiumChapter.Title)
                    ? $"Chapter {chapterIndex}"
                    : premiumChapter.Title;

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [{chapterIndex,4}] [Premium {premiumChapter.PremiumInfo.Cost} {currency}] {title}");
                Console.ResetColor();
            }

            var premiumSummary = BuildPremiumSummary(selectedPremium);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nTotal: {selectedPremium.Count} premium chapters ({premiumSummary})");
            Console.WriteLine($"Current premium currency balance: " +
                              $"{userCurrencies?.Where(uc => uc.CurrencyName == "Karma")?.FirstOrDefault()?.Balance:N0} Karma");
            Console.Write("\nType 'continue' to proceed with premium chapters, or press Enter to proceed without unlocking (they will remain teasers): ");
            Console.ResetColor();

            var premiumChoice = Console.ReadLine()?.Trim();
            if (string.Equals(premiumChoice, "continue", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info("User confirmed proceeding with premium chapters (unlock flow not implemented)");
                return true;
            }

            Logger.Info("User did not confirm premium unlock; proceeding without unlocking premium chapters");
            return false;
        }

        private class VolumeRange
        {
            public int Begin { get; init; }
            public int End { get; init; }
            public string Name { get; init; } = string.Empty;
        }
    }
}
