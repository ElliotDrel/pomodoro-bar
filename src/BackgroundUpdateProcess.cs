using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;

namespace PomodoroBar;

// Both Velopack restart and apply operations use this process policy.
internal sealed class BackgroundUpdateProcess : IProcessImpl
{
    private readonly DefaultProcessImpl defaults = new(new NullVelopackLogger());
    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);

    public string GetCurrentProcessPath() => defaults.GetCurrentProcessPath();
    public uint GetCurrentProcessId() => defaults.GetCurrentProcessId();
    public void Exit(int code) => defaults.Exit(code);

    internal static ProcessStartInfo StartInfo(string exe, IEnumerable<string> args, string directory)
    {
        var info = new ProcessStartInfo(exe) {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in args) info.ArgumentList.Add(argument);
        return info;
    }

    public void StartProcess(string exePath, IEnumerable<string> args, string workDir, bool showWindow)
    {
        using var process = Process.Start(StartInfo(exePath, args, workDir))
            ?? throw new System.InvalidOperationException("Could not start the background update helper.");
        // The helper remains hidden; allow the restarted app to show its normal windows.
        AllowSetForegroundWindow((uint)process.Id);
        Log.Write("update_helper_started pid=" + process.Id + " hidden=true");
    }
}
