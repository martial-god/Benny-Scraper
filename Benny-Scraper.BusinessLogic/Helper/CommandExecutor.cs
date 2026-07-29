using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace BennyScraper.BusinessLogic.Helper;

public static class CommandExecutor
{
    public static string ExecuteCommand(string command)
    {
        using var process = new Process();
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        // Detect OS and configure process start info accordingly
        if (IsWindows())
        {
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/c {command}";
        }
        else if (IsMacOS() || IsLinux())
        {
            var shellPath = GetDefaultShell();

            if (string.IsNullOrEmpty(shellPath))
            {
                throw new PlatformNotSupportedException("Unable to determine default shell.");
            }

            startInfo.FileName = shellPath;
            startInfo.Arguments = $"-c \"{command}\"";
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported OS platform.");
        }

        process.StartInfo = startInfo;

        // Set your output and error (asynchronous) handlers
        process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
        process.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    private static string? GetDefaultShell()
    {
        string? shellPath = Environment.GetEnvironmentVariable("SHELL");

        if (string.IsNullOrEmpty(shellPath))
        {
            return null;
        }

        if (shellPath.EndsWith("/zsh", StringComparison.OrdinalIgnoreCase))
        {
            return "/bin/zsh";
        }

        return shellPath.EndsWith("/bash", StringComparison.OrdinalIgnoreCase) ? "/bin/bash" : null;
    }

    private static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
    {
        if (!string.IsNullOrEmpty(outLine.Data))
        {
            Console.WriteLine(outLine.Data);
        }
    }
}