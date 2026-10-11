using System;
using System.IO;

namespace PomodoroBar;

internal static class Log
{
    private static readonly object Gate = new();
    public static void Write(string message)
    {
        try { lock (Gate) {
            Directory.CreateDirectory(BuildChannel.LogDirectory);
            File.AppendAllText(Path.Combine(BuildChannel.LogDirectory, $"{DateTime.Today:yyyy-MM-dd}.log"),
                $"{DateTimeOffset.Now:O} channel={BuildChannel.Name} version={BuildChannel.Version} pid={Environment.ProcessId} {message}\n");
        } } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
