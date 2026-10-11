using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace PomodoroBar;

internal static class BuildChannel
{
#if DEV
    public static bool IsDev => true;
    public const string Name = "dev";
    public const string DisplayName = "Pomodoro Bar (Dev)";
    public const string DataFolder = "PomodoroBar.Dev";
#else
    public static bool IsDev => false;
    public const string Name = "prod";
    public const string DisplayName = "Pomodoro Bar";
    public const string DataFolder = "PomodoroBar.Data";
#endif
    public static string Version => (Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0").Split('+')[0];
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataFolder);
    public static string LogDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PomodoroBar.Data", "logs");
    public static string PipeName => "PomodoroBar." + Name + "." + Environment.UserName + "." + Process.GetCurrentProcess().SessionId;
    public static string MutexName => "Local\\" + PipeName;
}
