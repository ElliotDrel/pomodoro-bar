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
    internal DateTimeOffset? ScheduledStart { get; private set; }
    internal DateTimeOffset? SectionStart { get; private set; }
    internal DateTimeOffset? SessionStart { get; private set; }
    private DateTimeOffset? focusCompletedAt;
    internal DateTimeOffset PreviewStart => ScheduledStart ?? (Timer.Running || Timer.Waiting || !Timer.IsFocus ? SessionStart ?? Now : Now);
    internal DateTimeOffset FocusEnd => Timer.IsFocus
        ? TimerModel.EstimatedEnd(Timer.Scheduled ? PreviewStart : Now, Timer.Waiting ? Timer.SecondsToDeadline : Timer.Remaining)
        : TimerModel.EstimatedEnd(focusCompletedAt ?? SectionStart ?? Now, 0);
    internal DateTimeOffset BreakEnd => Timer.IsFocus ? FocusEnd.AddSeconds(Timer.Rest) : TimerModel.EstimatedEnd(Now,Timer.Waiting ? Timer.SecondsToDeadline : Timer.Remaining);
    private readonly IAudio audio;
    private readonly DispatcherTimer tick, save, animation;

    private readonly UpdateService? updates;
    private readonly InstanceServer? server;
    private bool disposed;
    private bool sessionTouched;
    private bool restarting;
    public bool CanAutoRestart => !sessionTouched && !Timer.Running && !Timer.Waiting && !Timer.Scheduled && !Editor.Editing;

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
        bool scheduled = Timer.Scheduled;
        string? sound = Timer.Tick();
        if (scheduled && !Timer.Scheduled) { SectionStart = SessionStart = ScheduledStart; ScheduledStart = null; }
        if (sound == "complete" && Timer.IsFocus) focusCompletedAt = FocusEnd;
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
    public void Toggle() {
        if (Timer.Waiting) { Acknowledge(); return; }
        sessionTouched = true; Silence(); pulseUntil = 0;
        if (!Timer.Running && !Timer.Scheduled) {
            SectionStart = Now.AddSeconds(-(Timer.Duration - Timer.Remaining));
            if (Timer.IsFocus && SessionStart is null) SessionStart = SectionStart;
        }
        PlayEvent(Timer.Toggle()); ScheduledStart = null; Render();
    }
    public void Acknowledge() { Editor.EndEditSession(); sessionTouched = true; Silence(); if (Timer.Waiting) { Timer.Advance(true); SectionStart = Now; if (Timer.IsFocus) SessionStart = Now; } pulseUntil = 0; Render(); }
    public void Skip() { Editor.EndEditSession(); sessionTouched = true; Silence(); if (Timer.IsFocus) focusCompletedAt = Now; Timer.Advance(); ScheduledStart = null; SectionStart = Now; if (Timer.IsFocus) SessionStart = Now; pulseUntil = 0; Render(); }
    public void Reset()
    {
        Editor.EndEditSession(); sessionTouched = true; Silence(); ScheduledStart = null; SectionStart = SessionStart = focusCompletedAt = null; Timer = new TimerModel(Settings.Focus,Settings.Rest,Settings.Thresholds,clock); pulseUntil = 0; Render();
    }
    public void SetSession(double minutes,double[] thresholds)
    {
        if (minutes > 1440) { Editor.Note("Maximum session length is 24 hours."); return; }
        double total = Math.Max(120,Math.Floor(minutes+.5)*60);
        double focus = Math.Min(total-60,TimerModel.RoundDuration(total*5/6));
        Settings = Settings with { Focus = focus, Rest = total-focus, Thresholds = thresholds };
        Reset(); Save(); Editor.Note("Time set. Press play on the bar to begin.");
    }
    public void AddMinutes(int minutes)
    {
        double total = Timer.Focus + Timer.Rest + minutes*60;
        if (total > 86400) { Editor.Note("Maximum session length is 24 hours."); return; }
        double focus = Math.Clamp(TimerModel.RoundDuration(total*Timer.Focus/(Timer.Focus+Timer.Rest)),60,total-60);
        Settings = Settings with { Focus = focus, Rest = total-focus };
        Reset(); Save(); Editor.Note("Time added, preserving your focus/break ratio to whole minutes.");
    }
    public void SetTime(double seconds)
    {
        sessionTouched = true; Timer.SetTime(seconds); Silence(); pulseUntil = 0;
        Settings = Settings with { Focus = Timer.Focus, Rest = Timer.Rest }; Save(); Render();
    }
    private void SetDuration(bool focus,double seconds)
    {
        Timer.SetSectionTime(focus,TimerModel.RoundDuration(seconds));
        if (focus == Timer.IsFocus && Timer.Running) SectionStart = Now;
    }
    internal void EditTime(EditOrder order,TimeField field,double value)
    {
        var now = Now;
        // During focus, duration means remaining focus; during break it means remaining break.
        var origin = Timer.IsFocus ? (Timer.Scheduled ? PreviewStart : now) : FocusEnd.AddSeconds(-Timer.Focus);
        double focus = Timer.IsFocus ? Timer.Remaining : Timer.Focus;
        double rest = Timer.IsFocus ? Timer.Rest : (BreakEnd-FocusEnd).TotalSeconds;
        // The break field displays remaining time; the timeline includes elapsed break time.
        if (!Timer.IsFocus && field == TimeField.Break) value += (now-FocusEnd).TotalSeconds;
        var reference = DateTimeOffset.UnixEpoch;
        var current = new EditTimeline((origin-reference).TotalSeconds,focus,rest);
        var result = order.Apply(field,value,current,clock());
        var start = reference.AddSeconds(result.Start).ToOffset(now.Offset);
        var focusEnd = start.AddSeconds(result.Focus);
        var breakEnd = focusEnd.AddSeconds(result.Rest);
        bool movedStart = Math.Abs(result.Start-current.Start) > .01;
        if (Timer.IsFocus && start < now.AddSeconds(-.01) && movedStart)
            throw new ArgumentException("Keeping earlier edits would put the start in the past. Shorten the duration or wait 8 seconds.");
        if ((Timer.IsFocus ? focusEnd : breakEnd) <= now)
            throw new ArgumentException("The active end time must be in the future.");
        TimerModel.ValidateDuration(Timer.IsFocus && start > now ? result.Focus : (Timer.IsFocus ? focusEnd : breakEnd).Subtract(now).TotalSeconds);
        if (Timer.IsFocus) {
            if (movedStart) ConfigureStart(start);
            if (field == TimeField.Focus) SetDuration(true,result.Focus);
            else if (movedStart || Math.Abs(result.Focus-current.Focus) > .01)
                Timer.SetEndpoint(true,(focusEnd-(Timer.Scheduled ? PreviewStart : now)).TotalSeconds);
            if (Math.Abs(result.Rest-current.Rest) > .01) SetDuration(false,result.Rest);
        } else {
            if (Math.Abs(result.Focus-current.Focus) > .01) SetDuration(true,result.Focus);
            focusCompletedAt = focusEnd;
            if (field == TimeField.Break) SetDuration(false,(breakEnd-now).TotalSeconds);
            else Timer.SetEndpoint(false,(breakEnd-now).TotalSeconds);
        }
        order.Accept(clock());
        sessionTouched = true; Silence(); pulseUntil = 0;
        Settings = Settings with { Focus = Timer.Focus, Rest = Timer.Rest }; ScheduleSave(); Render();
    }
    internal double EditClockSeconds(DateTimeOffset value) => (value-DateTimeOffset.UnixEpoch).TotalSeconds;
    internal double EditSeconds => clock();
    internal bool SetStart(DateTimeOffset start)
    {
        sessionTouched = true; Silence();
        bool accepted = ConfigureStart(start); Render(); return accepted;
    }
    private bool ConfigureStart(DateTimeOffset start)
    {
        focusCompletedAt = null;
        var now = Now;
        bool past = start < now && start != TimerModel.EstimatedEnd(now,0);
        if (past || start <= TimerModel.EstimatedEnd(now,0)) {
            Timer = new TimerModel(Timer.Focus,Timer.Rest,Timer.Thresholds,clock);
            ScheduledStart = null; SectionStart = SessionStart = null;
        } else {
            ScheduledStart = start; SectionStart = SessionStart = null; Timer.Schedule((start-now).TotalSeconds);
        }
        return !past;
    }
    internal void CloseWindow(Window window)
    {
        window.Hide(); Render();
        // Run after WPF finishes the Closing event, avoiding reentrant Close calls.
        Application.Current.Dispatcher.BeginInvoke(() => {
            if (!Closing && !Bar.IsVisible && !Editor.IsVisible) Quit();
        });
    }
    private Rgb? displayedCountdownColor;
    private System.Windows.Media.SolidColorBrush? displayedCountdownBrush;
    internal System.Windows.Media.SolidColorBrush CountdownBrush {
        get {
            if (Timer.Scheduled) return UiTheme.Scheduled;
            if (!Timer.IsFocus) return Timer.Waiting ? UiTheme.Complete : UiTheme.Rest;
            var color = VisualMath.CountdownColor(Timer.Remaining/Timer.Duration);
            if (displayedCountdownColor != color) {
                displayedCountdownBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R,color.G,color.B));
                displayedCountdownBrush.Freeze(); displayedCountdownColor = color;
            }
            return displayedCountdownBrush!;
        }
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
        if ((Timer.Running || Timer.Scheduled) && (Visible(Bar) || Visible(Editor))) {
            double display = Timer.Scheduled ? Timer.StartRemaining : Timer.Remaining;
            double fraction = display - Math.Floor(display);
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
        if (!Closing) Application.Current.Dispatcher.BeginInvoke(() => {
            if (Closing) return;
            if (ScheduledStart is DateTimeOffset start) {
                double delay = (start-Now).TotalSeconds;
                Timer.RebaseScheduledStart(delay);
            }
            Tick();
        });
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
        Settings = Settings with { Width = Bar.Width, Height = Bar.Height, X = Bar.Left, Y = Bar.Top, Vertical = Bar.Vertical };
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

    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; Closing = true;
        tick.Stop(); animation.Stop(); save.Stop(); server?.Dispose(); updates?.Dispose(); audio.Dispose();
        Microsoft.Win32.SystemEvents.PowerModeChanged -= PowerChanged;
        Microsoft.Win32.SystemEvents.TimeChanged -= TimeChanged;
        Editor.Close(); Bar.Close();
    }
}
