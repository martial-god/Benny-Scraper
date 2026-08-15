using BennyScraper.BusinessLogic.Factory.Interfaces;
using NLog;

namespace BennyScraper.BusinessLogic.Utilities;

/// <summary>
/// Registers process-level shutdown handlers so we can dispose unmanaged resources
/// (e.g., Selenium WebDrivers) even when the app exits unexpectedly.
/// </summary>
internal static class ShutdownHooks
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private static int _disposed;

    /// <summary>
    /// Safe to call multiple times; disposal will only happen once.
    /// </summary>
    /// <param name="driverFactory">The driver factory whose Selenium drivers should be disposed on shutdown.</param>
    public static void Register(IDriverFactory driverFactory)
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeDriversOnce("ProcessExit");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            DisposeDriversOnce("UnhandledException", e.ExceptionObject as Exception);

        Console.CancelKeyPress += (_, e) =>
        {
            DisposeDriversOnce("CancelKeyPress");
            e.Cancel = false; // allow the process to terminate
        };
        return;

        void DisposeDriversOnce(string reason, Exception? ex = null)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _logger.Warn($"Shutting down: disposing Selenium drivers. Reason: {reason}");
                if (reason != "ProcessExit")
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("If the application was interrupted, check Task Manager for orphaned ChromeDriver processes.");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("On Windows, run 'taskkill /F /IM chromedriver.exe /T' if cleanup was unsuccessful.");
                    Console.ResetColor();
                }

                if (ex != null)
                {
                    _logger.Error(ex, "Unhandled exception triggered shutdown.");
                }

                driverFactory.DisposeAllDrivers();
            }
            catch (Exception disposeEx)
            {
                _logger.Error(disposeEx, "Failed while disposing Selenium drivers during shutdown.");
            }
        }
    }
}