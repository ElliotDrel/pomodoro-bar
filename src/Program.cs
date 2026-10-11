using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using PomodoroBar.Core;
using Velopack;

namespace PomodoroBar;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
    [STAThread]
    public static int Main(string[] args)
    {
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        if (args.Contains("--version")) { Log.Write("version_requested"); return 0; }
        bool smoke = args.Contains("--startup-smoke",StringComparer.OrdinalIgnoreCase);
        bool restart = args.Contains("--restart",StringComparer.OrdinalIgnoreCase) || args.Contains("--update",StringComparer.OrdinalIgnoreCase);
        bool check = args.Contains("--check-for-updates",StringComparer.OrdinalIgnoreCase);
        if ((restart || check) && BuildChannel.IsDev) return 1;
        bool quit = args.Contains("--quit",StringComparer.OrdinalIgnoreCase);
        using var mutex = new Mutex(false, smoke ? BuildChannel.MutexName + ".smoke." + Environment.ProcessId : BuildChannel.MutexName);
        bool owner;
        try { owner = mutex.WaitOne(0); } catch (AbandonedMutexException) { owner = true; }
        if (!owner) return InstanceServer.Send(quit ? "quit" : restart ? "restart" : check ? "check" : "show").GetAwaiter().GetResult() ? 0 : 1;
        try {
            if (quit) return 0;
            string path = Path.Combine(BuildChannel.DataDirectory,"settings.json");
            StartupSettings startup = new(new Settings(),true,null);
            if (!smoke) {
                int import = Array.IndexOf(args,"--import-settings");
                startup = StartupSettings.Load(path,import >= 0 && import+1 < args.Length ? args[import+1] : null,GetDpiForSystem()/96d);
                if (startup.Warning is not null) Log.Write("settings_warning " + startup.Warning);
            }
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += (_,e) => { Log.Write("fatal " + e.Exception); };
            using var host = new AppHost(startup.Value,smoke || !startup.CanSave ? null : path,smoke ? new SilentAudio() : new Audio(),smoke:smoke,startupTrigger:check ? UpdateTrigger.Manual : UpdateTrigger.Launch);
            if (startup.Warning is not null) { host.OpenEditor(); host.Editor.Note(startup.Warning); }
            if (smoke) {
                foreach (string name in new[] {"earlier","second-last","last","complete"})
                    if (!File.Exists(Path.Combine(AppContext.BaseDirectory,"sounds",name+".wav"))) throw new IOException("Missing sound " + name);
                host.OpenEditor(); host.Bar.Width = 130; host.Bar.Height = 20; host.Bar.SetResize(true); host.Bar.SetResize(false);
                var timer = new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(500) };
                timer.Tick += (_,_) => { timer.Stop(); host.Quit(); }; timer.Start();
            }
            app.Run(); return 0;
        } catch (Exception ex) { Log.Write("fatal " + ex); if (!smoke) MessageBox.Show(ex.Message,BuildChannel.DisplayName); return 1; }
        finally { mutex.ReleaseMutex(); }
    }
    private sealed class SilentAudio : IAudio { public void Play(string name,bool loop=false) {} public void Stop() {} public void Dispose() {} }
}
