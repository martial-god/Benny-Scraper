using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BennyScraper.BusinessLogic.Helper;

public static class CommandExecutor
{
    public static string ExecuteCommand(string command)
    {
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;

        // Detect OS and configure process start info accordingly
        if (IsWindows())
        {
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/c {command}";
        }
        else if (IsMacOS() | IsLinux())
        {
            string shellPath = GetDefaultShell();

            if (string.IsNullOrEmpty(shellPath))
            {
                throw new Exception("Unable to determine default shell.");
            }

            startInfo.FileName = shellPath;
            startInfo.Arguments = $"-c \"{command}\"";
        }
        else
        {
            throw new Exception("Unsupported OS platform.");
        }

        process.StartInfo = startInfo;

        // Set your output and error (asynchronous) handlers
        process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
        process.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode.ToString();
    }

    private static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    private static string GetDefaultShell()
    {
        var shellPath = Environment.GetEnvironmentVariable("SHELL");

        if (string.IsNullOrEmpty(shellPath))
        {
            return null;
        }

        if (shellPath.EndsWith("/zsh"))
        {
            return "/bin/zsh";
        }
        else if (shellPath.EndsWith("/bash"))
        {
            return "/bin/bash";
        }
        else
        {
            return null;
        }
    }

    private static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
    {
        if (!String.IsNullOrEmpty(outLine.Data))
        {
            Console.WriteLine(outLine.Data);
        }
    }
}