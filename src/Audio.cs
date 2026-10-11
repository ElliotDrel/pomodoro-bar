using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace PomodoroBar;

internal interface IAudio : IDisposable
{
    void Play(string name, bool loop = false);
    void Stop();
}

internal sealed class Audio : IAudio
{
    private readonly Func<string?,uint,bool> playback;
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string? sound, IntPtr module, uint flags);
    public Audio(Func<string?,uint,bool>? playback = null) => this.playback = playback ?? ((path,flags) => PlaySound(path,IntPtr.Zero,flags));
    public void Play(string name, bool loop = false)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "sounds", name + ".wav");
        if (!File.Exists(path)) throw new IOException("Missing timer sound: " + name);
        // SND_ASYNC | SND_NODEFAULT | SND_FILENAME; never block the dispatcher.
        if (!playback(path, 0x20003u | (loop ? 8u : 0u))) throw new Win32Exception("Could not play timer sound.");
    }
    public void Stop() => playback(null, 0);
    public void Dispose() => Stop();
}
