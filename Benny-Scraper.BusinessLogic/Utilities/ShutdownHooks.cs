using Benny_Scraper.BusinessLogic.Factory.Interfaces;
using NLog;

namespace Benny_Scraper.BusinessLogic.Utilities
{
    /// <summary>
    /// Registers process-level shutdown handlers so we can dispose unmanaged resources
    /// (e.g., Selenium WebDrivers) even when the app exits unexpectedly.
    /// </summary>
    public static class ShutdownHooks
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
        private static int _disposed;

        /// <summary>
        /// Safe to call multiple times; disposal will only happen once.
        /// </summary>
        public static void Register(IDriverFactory driverFactory)
        {
            ArgumentNullException.ThrowIfNull(driverFactory);

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
                    return;

                try
                {
                    Logger.Warn($"Shutting down: disposing Selenium drivers. Reason: {reason}");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Please be sure to check Task manager for orphaned ChromeDriver applications and shut them down manually if any exist.");
                    Console.ResetColor();
                    if (ex != null)
                        Logger.Error(ex, "Unhandled exception triggered shutdown.");

                    driverFactory.DisposeAllDrivers();
                }
                catch (Exception disposeEx)
                {
                    Logger.Error(disposeEx, "Failed while disposing Selenium drivers during shutdown.");
                }
            }
        }
    }
}
