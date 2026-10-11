using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;
using PomodoroBar.Core;

namespace PomodoroBar;

internal sealed class EditorWindow : Window
{
    private readonly AppHost host;
    internal readonly TextBox TimeCard;
    internal readonly TextBlock EndTime;
    internal readonly CheckBox ShowBar;
    internal readonly CheckBox ResizeBar;
    private readonly TextBlock heading, session, status, note, update, focusLength, breakLength, updateAction;
    private readonly CycleStrip cycle;
    internal readonly Button AlarmButton;
    internal readonly Button UpdateButton;
    private bool updateReady;
    private bool editing, dirty, syncing;
    private double? draftSeconds;
    private double cachedFocus = double.NaN, cachedRest = double.NaN;
    private int displayedSecond = -1;
    private Rgb? displayedColor;
    private DateTimeOffset? displayedEnd;
    private DateTime displayedToday;
    private string? displayedEndPrefix;
    internal int SyncCount { get; private set; }
    internal bool Editing => editing;
    public EditorWindow(AppHost host)
    {
        this.host = host;
        Title = BuildChannel.DisplayName + " · Edit timer";
        Icon = AppIcon.Source;
        ShowInTaskbar = true;
        Width = 520; Height = Math.Min(790,SystemParameters.WorkArea.Height-40); MinWidth = 420; MinHeight = 390;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = UiTheme.Background; Foreground = UiTheme.Ink;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        UseLayoutRounding = true;
        var panel = new StackPanel { Margin = new Thickness(24,20,24,18) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var title = UiTheme.Text(BuildChannel.DisplayName,25,true); title.FontFamily = UiTheme.Display;
        title.Margin = new Thickness(0,0,0,18); panel.Children.Add(title);
        var card = new StackPanel();
        var phaseRow = new DockPanel();
        status = UiTheme.Text("Paused",12,ink:UiTheme.Muted); status.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(status,Dock.Right); phaseRow.Children.Add(status);
        heading = UiTheme.Text("Focus time",16,true,UiTheme.Focus); phaseRow.Children.Add(heading); card.Children.Add(phaseRow);
        TimeCard = new TextBox {
            FontFamily = UiTheme.Digits, FontSize = 60, FontWeight = FontWeights.Normal,
            TextAlignment = TextAlignment.Left, Background = Brushes.Transparent, Foreground = Foreground,
            BorderThickness = new Thickness(0,0,0,2), BorderBrush = Brushes.Transparent,
            CaretBrush = UiTheme.Focus, SelectionBrush = UiTheme.Line, Margin = new Thickness(-3,3,0,0),
            MaxLength = 12, Padding = new Thickness(0), ToolTip = "Enter minutes, MM:SS, or HH:MM:SS"
        };
        AutomationProperties.SetName(TimeCard,"Time remaining. Click to edit duration.");
        TimeCard.GotKeyboardFocus += (_,_) => TimeCard.BorderBrush = UiTheme.Focus;
        TimeCard.LostKeyboardFocus += (_,_) => TimeCard.BorderBrush = Brushes.Transparent;
        card.Children.Add(TimeCard);
        card.Children.Add(UiTheme.Text("Click the time to edit. Enter saves; Esc cancels.",12,ink:UiTheme.Muted));
        EndTime = UiTheme.Text("",13,ink:UiTheme.Muted);
        EndTime.Margin = new Thickness(0,6,0,0); card.Children.Add(EndTime);
        cycle = new CycleStrip { Height = 28, Margin = new Thickness(0,14,0,4) }; card.Children.Add(cycle);
        var lengths = new DockPanel();
        breakLength = UiTheme.Text("",12,ink:UiTheme.Rest); DockPanel.SetDock(breakLength,Dock.Right); lengths.Children.Add(breakLength);
        focusLength = UiTheme.Text("",12,ink:UiTheme.Focus); lengths.Children.Add(focusLength); card.Children.Add(lengths);
        panel.Children.Add(new Border { Child = card, Padding = new Thickness(18,16,18,16), Background = UiTheme.Surface, CornerRadius = new CornerRadius(14) });
        AlarmButton = UiTheme.Button("Silence alarm and start next section",host.Acknowledge,true);
        AlarmButton.Margin = new Thickness(0,8,0,0); AlarmButton.Visibility = Visibility.Collapsed; panel.Children.Add(AlarmButton);
        var sessionRow = new DockPanel { Margin = new Thickness(0,20,0,8) };
        session = UiTheme.Text("",13,ink:UiTheme.Muted); DockPanel.SetDock(session,Dock.Right); sessionRow.Children.Add(session);
        sessionRow.Children.Add(UiTheme.Text("Session length",15,true)); panel.Children.Add(sessionRow);
        var presets = new UniformGrid { Columns = 2 };
        presets.Children.Add(Preset(30,25,5,() => host.SetSession(30,[40,20])));
        presets.Children.Add(Preset(60,50,10,() => host.SetSession(60,[60,20,10]))); panel.Children.Add(presets);
        var adds = new UniformGrid { Columns = 3, Margin = new Thickness(0,3,0,0) };
        foreach (int n in new[] {1,5,10}) adds.Children.Add(UiTheme.Button($"+{n} min", () => host.AddMinutes(n))); panel.Children.Add(adds);
        var barHeading = new DockPanel { Margin = new Thickness(0,18,0,5) };
        var resetSize = UiTheme.Button("Reset size",host.Bar.ResetSize); resetSize.Padding = new Thickness(10,5,10,5); resetSize.MinHeight = 28;
        DockPanel.SetDock(resetSize,Dock.Right); barHeading.Children.Add(resetSize);
        var barTitle = UiTheme.Text("Floating bar",15,true); barTitle.VerticalAlignment = VerticalAlignment.Center; barHeading.Children.Add(barTitle); panel.Children.Add(barHeading);
        ShowBar = UiTheme.Switch("Show floating bar"); ShowBar.IsChecked = true;
        ShowBar.Click += (_,_) => { if (ShowBar.IsChecked == true) host.Bar.Show(); else host.Bar.Hide(); };
        panel.Children.Add(ShowBar);
        ResizeBar = UiTheme.Switch("Resize using the bar’s edges");
        ResizeBar.Click += (_,_) => {
            bool resize = ResizeBar.IsChecked == true;
            // Showing the bar synchronizes controls; capture the requested value first.
            if (resize) host.Bar.Show(); host.Bar.SetResize(resize); host.Render();
        }; panel.Children.Add(ResizeBar);
        var updateRow = new DockPanel { Margin = new Thickness(0,20,0,0) };
        updateAction = UiTheme.Text("Check for updates",12,true); updateAction.TextAlignment = TextAlignment.Center; updateAction.MaxWidth = 142;
        UpdateButton = UiTheme.Button(updateAction,UpdateClicked);
        UpdateButton.Width = 174; UpdateButton.MinHeight = 42; UpdateButton.IsEnabled = false;
        UpdateButton.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(UpdateButton,Dock.Left); updateRow.Children.Add(UpdateButton);
        update = UiTheme.Text($"Version {BuildChannel.Version}",11,ink:UiTheme.Muted); update.Margin = new Thickness(14,0,0,0);
        update.VerticalAlignment = VerticalAlignment.Center; updateRow.Children.Add(update); panel.Children.Add(updateRow);
        note = UiTheme.Text("Set your time, then press play on the bar.",12,ink:UiTheme.Muted); note.Margin = new Thickness(0,14,0,8); panel.Children.Add(note);
        TimeCard.GotKeyboardFocus += (_,_) => { editing = true; dirty = false; };
        TimeCard.TextChanged += (_,_) => {
            if (editing && !syncing) {
                dirty = true;
                try { draftSeconds = TimerModel.ParseTime(TimeCard.Text); } catch (ArgumentException) { draftSeconds = null; }
                SyncEndTime(); host.ScheduleWake();
            }
        };
        TimeCard.LostKeyboardFocus += (_,_) => { if (!FinishEdit()) CancelEdit(); };
        TimeCard.PreviewKeyDown += (_,e) => {
            if (e.Key == Key.Enter) { if (FinishEdit()) Keyboard.ClearFocus(); e.Handled = true; }
            else if (e.Key == Key.Escape) { CancelEdit(); Keyboard.ClearFocus(); e.Handled = true; }
        };
        PreviewKeyDown += (_,e) => { if (e.Key == Key.Escape && !editing) { CloseEditor(); e.Handled = true; } };
        Closing += (_,e) => { if (!host.Closing) { e.Cancel = true; CloseEditor(); } };
        Sync();
    }
    private static Button Preset(int total,int focus,int rest,Action action)
    {
        var content = new StackPanel();
        var title = UiTheme.Text($"{total} minutes",18,true); title.FontFamily = UiTheme.Display; content.Children.Add(title);
        content.Children.Add(UiTheme.Text($"{focus} focus / {rest} break",12,ink:UiTheme.Muted));
        var b = UiTheme.Button(content,action); b.HorizontalContentAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetName(b,$"{total} minutes: {focus} focus, {rest} break"); return b;
    }
    internal void CancelEdit() { editing = false; dirty = false; displayedSecond = -1; Sync(); host.ScheduleWake(); }
    internal bool FinishEdit()
    {
        if (!editing) return true;
        if (dirty) {
            try { host.SetTime(TimerModel.ParseTime(TimeCard.Text)); }
            catch (ArgumentException ex) { Note(ex.Message); return false; }
        }
        editing = false; dirty = false; displayedSecond = -1; Sync(); host.ScheduleWake(); return true;
    }
    internal void Sync()
    {
        SyncCount++;
        var t = host.Timer;
        heading.Text = t.IsFocus ? "Focus time" : "Break time";
        heading.Foreground = t.IsFocus ? UiTheme.Focus : UiTheme.Rest;
        int second = (int)Math.Ceiling(t.Remaining);
        if (!editing && displayedSecond != second) { syncing = true; TimeCard.Text = TimerModel.FormatTime(t.Remaining); syncing = false; displayedSecond = second; }
        var color = VisualMath.CountdownColor(t.Remaining/t.Duration);
        if (displayedColor != color) {
            var brush = new SolidColorBrush(Color.FromRgb(color.R,color.G,color.B)); brush.Freeze();
            TimeCard.Foreground = brush; displayedColor = color;
        }
        if (cachedFocus != t.Focus || cachedRest != t.Rest) {
            session.Text = $"{(t.Focus+t.Rest)/60:0.##} min total";
            focusLength.Text = TimerModel.FormatTime(t.Focus) + " focus";
            breakLength.Text = TimerModel.FormatTime(t.Rest) + " break";
            cachedFocus = t.Focus; cachedRest = t.Rest;
        }
        cycle.Set(t.Focus/(t.Focus+t.Rest),t.Progress);
        status.Text = t.Waiting ? "Time is up" : t.Running ? "Running" : "Paused";
        status.Foreground = t.Waiting ? UiTheme.Warning : UiTheme.Muted;
        AlarmButton.Visibility = t.Waiting ? Visibility.Visible : Visibility.Collapsed;
        ShowBar.IsChecked = host.Bar.IsVisible; ResizeBar.IsChecked = host.Bar.ResizeEnabled;
        SyncEndTime();
    }
    private void SyncEndTime()
    {
        var t = host.Timer;
        double remaining = t.Remaining;
        bool preview = editing && dirty;
        if (preview) {
            if (draftSeconds is not double value) { displayedEnd = null; EndTime.Text = "Enter a valid time to preview the end."; return; }
            remaining = value;
        } else if (t.Waiting) { displayedEnd = null; EndTime.Text = t.IsFocus ? "Focus complete" : "Break complete"; return; }
        var now = host.Now;
        var end = TimerModel.EstimatedEnd(now,remaining).ToLocalTime();
        string prefix = !t.Running ? "If started now · ends at " : preview ? "If saved now · ends at " : "Ends at ";
        var today = now.LocalDateTime.Date;
        if (displayedEnd == end && displayedEndPrefix == prefix && displayedToday == today) return;
        displayedEnd = end; displayedEndPrefix = prefix; displayedToday = today;
        string day = end.Date == today ? "" : end.Date == today.AddDays(1) ? " tomorrow" : end.ToString(" 'on' ddd, MMM d");
        string when = end.ToString("t") + day;
        EndTime.Text = prefix + when;
    }
    internal double SecondsUntilPreviewChange()
    {
        var t = host.Timer;
        bool preview = editing && dirty;
        if ((t.Running && !preview) || (t.Waiting && !preview) || (preview && draftSeconds is null)) return double.PositiveInfinity;
        double seconds = (host.Now.AddSeconds(preview ? draftSeconds!.Value : t.Remaining).Ticks % TimeSpan.TicksPerMinute) / (double)TimeSpan.TicksPerSecond;
        return seconds < 30 ? 30 - seconds : 90 - seconds;
    }
    internal void Note(string text) => note.Text = text;
    private void UpdateClicked()
    {
        bool install = updateReady;
        UpdateButton.IsEnabled = false;
        updateAction.Text = install ? "Restarting…" : "Checking for update now";
        AutomationProperties.SetName(UpdateButton,updateAction.Text);
        if (install) host.InstallUpdate(); else host.CheckUpdates();
    }
    internal void UpdateDetails(UpdateService service)
        => UpdateDetails(service.State,service.CanUpdate,service.HasPending,service.LastCheckedAt,service.LastUpdatedAt);
    internal void UpdateDetails(UpdateStatus state,bool canUpdate,bool hasPending,DateTimeOffset? checkedAt,DateTimeOffset? updatedAt)
    {
        static string Stamp(DateTimeOffset? time) => time?.LocalDateTime.ToString("MMM d 'at' h:mm tt") ?? "Never";
        update.Text = $"Version {BuildChannel.Version}\n{state.Text}\nLast checked: {Stamp(checkedAt)}\nLast updated: {Stamp(updatedAt)}";
        updateReady = canUpdate && hasPending && state.Phase == UpdatePhase.Ready;
        updateAction.Text = updateReady ? "Update and restart" : state.Phase == UpdatePhase.Checking ? "Checking for update now" : state.Phase == UpdatePhase.Downloading ? "Downloading update…" : "Check for updates";
        UpdateButton.IsEnabled = canUpdate && state.Phase is not (UpdatePhase.Checking or UpdatePhase.Downloading);
        UpdateButton.Background = updateReady ? UiTheme.Focus : UiTheme.Surface;
        UpdateButton.BorderBrush = updateReady ? UiTheme.Focus : UiTheme.Line;
        updateAction.Foreground = updateReady ? UiTheme.Background : UiTheme.Ink;
        AutomationProperties.SetName(UpdateButton,updateAction.Text);
    }
    private void CloseEditor() { if (FinishEdit()) Hide(); }
}

internal sealed class CycleStrip : FrameworkElement
{
    private static readonly Pen TickPen = UiTheme.Pen(UiTheme.Line,1), ProgressPen = UiTheme.Pen(UiTheme.Ink,2);
    private double focus = 5d/6, progress;
    internal void Set(double focusFraction,double elapsed)
    {
        if (focus == focusFraction && progress == elapsed) return;
        focus = focusFraction; progress = elapsed; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        double width = Math.Max(0,ActualWidth), split = width*focus;
        dc.DrawRectangle(UiTheme.Focus,null,new Rect(0,14,split,4));
        dc.DrawRectangle(UiTheme.Rest,null,new Rect(split,14,width-split,4));
        for (int i = 0; i <= 12; i++) {
            double x = Math.Clamp(width*i/12,1,Math.Max(1,width-1));
            dc.DrawLine(TickPen,new Point(x,20),new Point(x,i%3==0 ? 27 : 24));
        }
        double position = Math.Clamp(width*progress,1,Math.Max(1,width-1));
        dc.DrawLine(ProgressPen,new Point(position,5),new Point(position,20));
        dc.DrawEllipse(UiTheme.Ink,null,new Point(position,5),2,2);
    }
}
