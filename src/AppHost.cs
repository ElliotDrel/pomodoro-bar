using System;
using System.Diagnostics;
using System.IO;
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
    private readonly IAudio audio;
    private readonly DispatcherTimer tick, save;
    private readonly System.Windows.Forms.NotifyIcon? tray;
    private readonly UpdateService? updates;
    private readonly InstanceServer? server;
    private bool disposed;
    private bool sessionTouched;
    public bool CanAutoRestart => !sessionTouched && !Timer.Running && !Timer.Waiting && !Editor.Editing;

    public AppHost(Settings settings, string? settingsPath, IAudio audio, Func<double>? clock = null, bool services = true, bool smoke = false)
    {
        Settings = settings; this.settingsPath = settingsPath; this.audio = audio;
        // TickCount64 advances across sleep; deadlines recover elapsed time on resume.
        this.clock = clock ?? (() => Environment.TickCount64 / 1000d);
        Timer = new TimerModel(settings.Focus,settings.Rest,settings.Thresholds,this.clock);
        save = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        save.Tick += (_,_) => { save.Stop(); Save(); };
        Bar = new BarWindow(this); Editor = new EditorWindow(this);
        tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        tick.Tick += (_,_) => Tick();
        Bar.Show(); ShowEditor(); tick.Start();
        if (services) {
            if (!smoke) {
                updates = new UpdateService();
                updates.Status += text => Application.Current.Dispatcher.BeginInvoke(() => { if (!Closing) Editor.UpdateStatus(text); });
                updates.ApplyRequested += () => Application.Current.Dispatcher.BeginInvoke(() => { if (CanAutoRestart) Restart(); });
            }
            tray = new System.Windows.Forms.NotifyIcon {
                Text = BuildChannel.DisplayName,
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
                Visible = true
            };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            void Item(string text, Action action) => menu.Items.Add(text,null,(_,_) => action());
            Item("Edit timer",OpenEditor); Item("Show floating bar",() => { Bar.Show(); Render(); });
            Item("Play / pause",() => { Silence(); Toggle(); });
            Item("Reset session",Reset);
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            Item("Silence alarm",Silence);
            if (!smoke) {
                Item("Check for updates",() => _ = updates!.CheckAsync(UpdateTrigger.Manual));
                Item("Restart / install update",() => Restart());
            }
            Item("Quit",Quit);
            tray.ContextMenuStrip = menu; tray.DoubleClick += (_,_) => OpenEditor();
            if (!smoke) server = new InstanceServer(command => Application.Current.Dispatcher.InvokeAsync(() => {
                if (command == "restart") { Log.Write("restart_command_received"); return Restart(); }
                if (command == "quit") { RequestQuit(); return true; }
                ShowApp(); return true;
            }).Task);
            if (!smoke) { _ = updates!.CheckAsync(UpdateTrigger.Launch); Log.Write("started"); }
        }
    }
    public void Tick()
    {
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
        Reset(); Save(); Editor.Note("New session is paused · press play to begin.");
    }
    public void AddMinutes(int minutes) => SetSession((Timer.Focus+Timer.Rest)/60+minutes,Timer.Thresholds);
    public void SetTime(double seconds)
    {
        sessionTouched = true; Timer.SetTime(seconds); Silence(); pulseUntil = 0;
        Settings = Settings with { Focus = Timer.Focus, Rest = Timer.Rest }; Save(); Render();
    }
    public void Render() { Bar.Surface.InvalidateVisual(); Editor.Sync(); }
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
    public bool Restart()
    {
        if (updates?.CanUpdate != true) { Report("Restart updates require an installed production app."); return false; }
        if (!Save() || !updates.PrepareRestart()) return false;
        // Give the pipe server time to deliver its acknowledgement before exiting.
        RequestQuit(); return true;
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
        if (!Editor.IsVisible) tray?.ShowBalloonTip(5000,BuildChannel.DisplayName,text,System.Windows.Forms.ToolTipIcon.Warning);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; Closing = true;
        tick.Stop(); save.Stop(); server?.Dispose(); updates?.Dispose(); audio.Dispose();
        tray?.Dispose(); Editor.Close(); Bar.Close();
    }
}
