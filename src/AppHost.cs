using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PomodoroBar.Core;

namespace PomodoroBar;

internal sealed class AppHost : IDisposable
{
    public Settings Settings { get; private set; }
    public TimerModel Timer { get; private set; }
    public BarWindow Bar { get; }
    public EditorWindow Editor { get; }
    public bool Closing { get; private set; }
    public bool Pulsing => Timer.Waiting || Environment.TickCount64 < pulseUntil;
    private long pulseUntil;
    private readonly string? settingsPath;
    private readonly Func<double> clock;
    private readonly Func<DateTimeOffset> wallClock;
    internal DateTimeOffset Now => wallClock();
    private readonly IAudio audio;
    private readonly DispatcherTimer tick, save, animation;
    private readonly NativeTray? tray;
    private readonly UpdateService? updates;
    private readonly InstanceServer? server;
    private bool disposed;
    private bool sessionTouched;
    private bool restarting;
    public bool CanAutoRestart => !sessionTouched && !Timer.Running && !Timer.Waiting && !Editor.Editing;

    public AppHost(Settings settings, string? settingsPath, IAudio audio, Func<double>? clock = null, bool services = true, bool smoke = false, UpdateTrigger startupTrigger = UpdateTrigger.Launch, Func<DateTimeOffset>? wallClock = null)
    {
        Settings = settings; this.settingsPath = settingsPath; this.audio = audio;
        // TickCount64 advances across sleep; deadlines recover elapsed time on resume.
        this.clock = clock ?? (() => Environment.TickCount64 / 1000d);
        this.wallClock = wallClock ?? (() => DateTimeOffset.Now);
        Timer = new TimerModel(settings.Focus,settings.Rest,settings.Thresholds,this.clock);
        save = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        save.Tick += (_,_) => { save.Stop(); Save(); };
        tick = new DispatcherTimer();
        tick.Tick += (_,_) => { WakeCallbackCount++; Tick(); };
        animation = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        animation.Tick += (_,_) => {
            if (Closing) { animation.Stop(); return; }
            if (Visible(Bar)) Bar!.Surface.InvalidateVisual();
            if (!Pulsing || !Visible(Bar) || !SystemParameters.ClientAreaAnimation) animation.Stop();
        };
        Bar = new BarWindow(this); Editor = new EditorWindow(this);
        Bar.IsVisibleChanged += (_,_) => Tick(); Editor.IsVisibleChanged += (_,_) => Tick();
        Bar.StateChanged += (_,_) => Tick(); Editor.StateChanged += (_,_) => Tick();
        Microsoft.Win32.SystemEvents.PowerModeChanged += PowerChanged;
        Microsoft.Win32.SystemEvents.TimeChanged += TimeChanged;
        Bar.Show(); ShowEditor(); Render();
        if (services) {
            if (!smoke) {
                updates = new UpdateService();
                updates.StateChanged += _ => Application.Current.Dispatcher.BeginInvoke(() => { if (!Closing) Editor.UpdateDetails(updates); });
                updates.ApplyRequested += () => Application.Current.Dispatcher.BeginInvoke(() => { if (CanAutoRestart) InstallUpdate(); });
                Editor.UpdateDetails(updates);
            }
            tray = new NativeTray(OpenEditor);
            void Item(string text, Action action) => tray.Item(text,action);
            Item("Edit timer",OpenEditor); Item("Show floating bar",() => { Bar.Show(); Render(); });
            Item("Play / pause",() => { Silence(); Toggle(); });
            Item("Reset session",Reset);
            tray.Separator();
            Item("Silence alarm",Silence);
            if (!smoke && updates?.CanUpdate == true) {
                tray.Item("Check for updates",CheckUpdates,() => !updates.IsBusy);
                tray.Item("Install downloaded update",InstallUpdate,() => updates.HasPending);
                Item("Restart",Restart);
            }
            Item("Quit",Quit);
            if (!smoke) server = new InstanceServer(command => Application.Current.Dispatcher.InvokeAsync(() => {
                if (command == "restart") { Log.Write("restart_command_received"); if (updates?.CanUpdate != true) return false; Restart(); return true; }
                if (command == "check") { if (updates?.CanUpdate != true) return false; CheckUpdates(); return true; }
                if (command == "quit") { RequestQuit(); return true; }
                ShowApp(); return true;
            }).Task);
            if (!smoke) { _ = updates!.CheckAsync(startupTrigger); Log.Write("started"); }
        }
    }
    public void Tick()
    {
        if (Closing) return;
        tick.Stop();
        string? sound = Timer.Tick();
        PlayEvent(sound);
        Render();
    }
    private void PlayEvent(string? sound)
    {
        if (sound is not null) {
            pulseUntil = Environment.TickCount64 + 4000;
            try { audio.Play(sound, sound == "complete"); }
            catch (Exception ex) { Log.Write("audio_failed " + ex.Message); Report("Could not play timer sound: " + ex.Message); }
        }
    }
    public void Silence() => audio.Stop();
    public void Toggle() { sessionTouched = true; Silence(); pulseUntil = 0; PlayEvent(Timer.Toggle()); Render(); }
    public void Acknowledge() { sessionTouched = true; Silence(); if (Timer.Waiting) Timer.Advance(true); pulseUntil = 0; Render(); }
    public void Skip() { sessionTouched = true; Silence(); Timer.Advance(); pulseUntil = 0; Render(); }
    public void Reset()
    {
        sessionTouched = true; Silence(); Timer = new TimerModel(Settings.Focus,Settings.Rest,Settings.Thresholds,clock); pulseUntil = 0; Render();
    }
    public void SetSession(double minutes,double[] thresholds)
    {
        if (minutes > 1440) { Editor.Note("Maximum session length is 24 hours."); return; }
        Editor.CancelEdit(); Settings = Settings with { Focus = minutes*50, Rest = minutes*10, Thresholds = thresholds };
        Reset(); Save(); Editor.Note("Time set. Press play on the bar to begin.");
    }
    public void AddMinutes(int minutes) => SetSession((Timer.Focus+Timer.Rest)/60+minutes,Timer.Thresholds);
    public void SetTime(double seconds)
    {
        sessionTouched = true; Timer.SetTime(seconds); Silence(); pulseUntil = 0;
        Settings = Settings with { Focus = Timer.Focus, Rest = Timer.Rest }; Save(); Render();
    }
    private static bool Visible(Window? window) => window is not null && window.IsVisible && window.WindowState != WindowState.Minimized;
    internal bool WakeScheduled => tick.IsEnabled;
    internal long WakeCallbackCount { get; private set; }
    internal TimeSpan WakeInterval => tick.Interval;
    internal bool AnimationScheduled => animation.IsEnabled;
    public void Render()
    {
        if (Closing) return;
        if (Visible(Bar)) Bar.Surface.Refresh();
        if (Visible(Editor)) Editor.Sync();
        if (Pulsing && Visible(Bar) && SystemParameters.ClientAreaAnimation) animation.Start(); else animation.Stop();
        ScheduleWake();
    }
    internal void ScheduleWake()
    {
        if (Closing) return;
        tick.Stop();
        double seconds = Timer.SecondsUntilNextEvent();
        if (Timer.Running && (Visible(Bar) || Visible(Editor))) {
            double fraction = Timer.Remaining - Math.Floor(Timer.Remaining);
            seconds = Math.Min(seconds, fraction > .001 ? fraction : 1);
        }
        if (Visible(Editor)) seconds = Math.Min(seconds, Editor.SecondsUntilPreviewChange());
        if (double.IsPositiveInfinity(seconds)) return;
        tick.Interval = TimeSpan.FromSeconds(Math.Max(.01, seconds)); tick.Start();
    }
    private void PowerChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Resume) QueueClockRefresh();
    }
    private void TimeChanged(object? sender, EventArgs e) => QueueClockRefresh();
    private void QueueClockRefresh()
    {
        if (!Closing) Application.Current.Dispatcher.BeginInvoke(() => { if (!Closing) Tick(); });
    }
    public void ShowApp()
    {
        Bar.Show();
        if (Bar.WindowState == WindowState.Minimized) Bar.WindowState = WindowState.Normal;
        OpenEditor();
    }
    public void OpenEditor()
    {
        sessionTouched = true;
        ShowEditor();
    }
    private void ShowEditor()
    {
        if (!Editor.IsVisible) {
            Editor.Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width-Editor.Width)/2;
            Editor.Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height-Editor.Height)/2;
            Editor.Show(); Editor.CancelEdit(); System.Windows.Input.Keyboard.ClearFocus();
        }
        if (Editor.WindowState == WindowState.Minimized) Editor.WindowState = WindowState.Normal;
        Editor.Activate(); Render();
    }
    public void ScheduleSave() { if (Closing) return; save.Stop(); save.Start(); }
    public bool Save()
    {
        if (settingsPath is null) return true;
        Settings = Settings with { Width = Bar.Width, Height = Bar.Height, X = Bar.Left, Y = Bar.Top };
        try { Settings.Save(settingsPath); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) {
            Log.Write("settings_save_failed " + ex.Message); Report("Could not save settings: " + ex.Message); return false;
        }
    }
    public void CheckUpdates() { if (updates is not null) _ = updates.CheckAsync(UpdateTrigger.Manual); }
    public void Restart() => _ = RestartAsync(false);
    public void InstallUpdate()
    {
        if (updates?.HasPending != true) { Report("Check for updates to download an update first."); return; }
        _ = RestartAsync(true);
    }
    private async Task RestartAsync(bool applyUpdate)
    {
        if (restarting || Closing) return;
        if (updates?.CanUpdate != true) { Report("Restart updates require an installed production app."); return; }
        if (!Save()) return;
        restarting = true;
        bool prepared = await Task.Run(() => updates.PrepareRestart(applyUpdate,() => !Closing));
        if (Closing) return;
        if (!prepared) { restarting = false; return; }
        // Give the pipe server time to deliver its acknowledgement before exiting.
        RequestQuit();
    }
    public void RequestQuit()
    {
        var exit = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        exit.Tick += (_,_) => { exit.Stop(); Quit(); }; exit.Start();
    }
    public void Quit()
    {
        if (Closing) return;
        Save(); Closing = true; Dispose(); Application.Current.Shutdown();
    }
    private void Report(string text)
    {
        Editor.Note(text);
        if (!Editor.IsVisible) tray?.ShowWarning(BuildChannel.DisplayName,text);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; Closing = true;
        tick.Stop(); animation.Stop(); save.Stop(); server?.Dispose(); updates?.Dispose(); audio.Dispose();
        Microsoft.Win32.SystemEvents.PowerModeChanged -= PowerChanged;
        Microsoft.Win32.SystemEvents.TimeChanged -= TimeChanged;
        tray?.Dispose(); Editor.Close(); Bar.Close();
    }
}
