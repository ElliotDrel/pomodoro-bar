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
    internal readonly TextBlock StartCountdown;
    internal readonly TimerPanel TimerPanel;
    internal readonly TextBox BreakCard, StartTimeCard, FocusEndCard, BreakEndCard;
    internal readonly Button PlayButton, SkipButton, OrientationButton;
    private TextBox? activeField;
    private bool validDraft = true;
    private Grid milestoneGrid = null!;
    private readonly TextBlock inputError;
    private readonly System.Collections.Generic.Dictionary<TextBox,TextBlock> dayLabels = new();
    private bool? displayedPlaying;
    internal readonly CheckBox ShowBar;
    internal readonly CheckBox ResizeBar;
    private readonly TextBlock heading, session, status, note, update, updateAction;
    private readonly CycleStrip cycle;
    internal readonly Button AlarmButton;
    internal readonly Button UpdateButton;
    private bool updateReady;
    private bool editing, dirty, syncing;
    private readonly EditOrder editOrder = new();
    private readonly TextBlock editStatus;

    internal int SyncCount { get; private set; }
    internal bool Editing => editing;
    internal bool ErrorVisible => note.Foreground == UiTheme.Error;
    public EditorWindow(AppHost host)
    {
        this.host = host;
        Title = BuildChannel.DisplayName + " · Edit timer";
        Icon = AppIcon.Source;
        ShowInTaskbar = true;
        Width = 680; Height = Math.Min(790,SystemParameters.WorkArea.Height-40); MinWidth = 560; MinHeight = 390;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = UiTheme.Background; Foreground = UiTheme.Ink;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        UseLayoutRounding = true;
        var panel = new StackPanel { Margin = new Thickness(24,20,24,18) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var title = UiTheme.Text(BuildChannel.DisplayName,25,true); title.FontFamily = UiTheme.Display;
        title.Margin = new Thickness(0,0,0,18); panel.Children.Add(title);
        var card = new StackPanel();
        status = UiTheme.Text("Paused",12,ink:UiTheme.Muted); card.Children.Add(status);
        var durationRow = new Grid { Margin = new Thickness(0,8,0,0) };
        durationRow.ColumnDefinitions.Add(new ColumnDefinition());
        durationRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        durationRow.ColumnDefinitions.Add(new ColumnDefinition());
        var focusPanel = new StackPanel();
        heading = UiTheme.Text("Focus time",14,false,UiTheme.Focus); focusPanel.Children.Add(heading);
        TimeCard = TimeInput(54,"Focus duration. Enter minutes, MM:SS, or HH:MM:SS.");
        focusPanel.Children.Add(new Viewbox { Child = TimeCard, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left });
        durationRow.Children.Add(focusPanel);
        var timerControls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,12,12,0) };
        PlayButton = UiTheme.Button(TimerGlyph(false,false),host.Toggle,true); PlayButton.Padding = new Thickness(10,7,10,7);
        AutomationProperties.SetName(PlayButton,"Play or pause timer"); PlayButton.ToolTip = "Play / pause";
        SkipButton = UiTheme.Button(TimerGlyph(false,true),host.Skip); SkipButton.Padding = new Thickness(10,7,10,7);
        AutomationProperties.SetName(SkipButton,"Skip section"); SkipButton.ToolTip = "Skip section";
        timerControls.Children.Add(PlayButton); timerControls.Children.Add(SkipButton);
        var controlsPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        StartCountdown = UiTheme.Text("",24,true,UiTheme.Scheduled); StartCountdown.TextAlignment = TextAlignment.Center;
        StartCountdown.MaxWidth = 160; StartCountdown.Visibility = Visibility.Collapsed;
        controlsPanel.Children.Add(StartCountdown); controlsPanel.Children.Add(timerControls);
        Grid.SetColumn(controlsPanel,1); durationRow.Children.Add(controlsPanel);
        var restPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var restHeading = UiTheme.Text("Break time",14,false,UiTheme.Rest); restHeading.TextAlignment = TextAlignment.Right; restPanel.Children.Add(restHeading);
        BreakCard = TimeInput(48,"Break duration. Enter minutes, MM:SS, or HH:MM:SS."); BreakCard.TextAlignment = TextAlignment.Right;
        restPanel.Children.Add(new Viewbox { Child = BreakCard, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Right });
        Grid.SetColumn(restPanel,2); durationRow.Children.Add(restPanel); card.Children.Add(durationRow);
        cycle = new CycleStrip { Height = 24, Margin = new Thickness(0,16,0,8) }; card.Children.Add(cycle);
        milestoneGrid = new Grid(); milestoneGrid.ColumnDefinitions.Add(new ColumnDefinition()); milestoneGrid.ColumnDefinitions.Add(new ColumnDefinition());
        StartTimeCard = TimeInput(17,"Start time. Enter a clock time or tomorrow followed by a time.");
        FocusEndCard = TimeInput(17,"Focus end time. Editing adjusts focus duration.");
        BreakEndCard = TimeInput(17,"Break end time. Editing adjusts break duration.");
        StackPanel Endpoint(string label,TextBox field,HorizontalAlignment alignment) {
            var group = new StackPanel { HorizontalAlignment = alignment };
            group.Children.Add(UiTheme.Text(label,11,ink:UiTheme.Muted)); group.Children.Add(field);
            var day = UiTheme.Text("",11,ink:UiTheme.Muted); dayLabels[field] = day; group.Children.Add(day); return group;
        }
        milestoneGrid.Children.Add(Endpoint("Start",StartTimeCard,HorizontalAlignment.Left));
        var focusEndPanel = Endpoint("Focus ends",FocusEndCard,HorizontalAlignment.Right); focusEndPanel.Margin = new Thickness(0,0,10,0); milestoneGrid.Children.Add(focusEndPanel);
        var breakEndPanel = Endpoint("Break ends",BreakEndCard,HorizontalAlignment.Right); Grid.SetColumn(breakEndPanel,1); milestoneGrid.Children.Add(breakEndPanel);
        card.Children.Add(milestoneGrid);
        editStatus = UiTheme.Text("Edit any time. Your first edit stays fixed for 8 seconds after your latest change.",12,ink:UiTheme.Muted);
        editStatus.TextWrapping = TextWrapping.Wrap; editStatus.Margin = new Thickness(0,8,0,0); card.Children.Add(editStatus);
        inputError = UiTheme.Text("",12,ink:UiTheme.Error); inputError.Visibility = Visibility.Collapsed;
        inputError.Margin = new Thickness(0,8,0,0); card.Children.Add(inputError);
        TimerPanel = new TimerPanel { Child = card, Padding = new Thickness(18,16,18,16), Background = UiTheme.Surface, CornerRadius = new CornerRadius(14) };
        panel.Children.Add(TimerPanel);
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
        OrientationButton = UiTheme.Button("Use vertical bar",() => host.Bar.SetVertical(!host.Bar.Vertical)); panel.Children.Add(OrientationButton);
        var updateRow = new DockPanel { Margin = new Thickness(0,20,0,0) };
        updateAction = UiTheme.Text("Check for updates",12,true); updateAction.TextAlignment = TextAlignment.Center; updateAction.MaxWidth = 142;
        UpdateButton = UiTheme.Button(updateAction,UpdateClicked);
        UpdateButton.Width = 174; UpdateButton.MinHeight = 42; UpdateButton.IsEnabled = false;
        UpdateButton.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(UpdateButton,Dock.Left); updateRow.Children.Add(UpdateButton);
        update = UiTheme.Text($"Version {BuildChannel.Version}",11,ink:UiTheme.Muted); update.Margin = new Thickness(14,0,0,0);
        update.VerticalAlignment = VerticalAlignment.Center; updateRow.Children.Add(update); panel.Children.Add(updateRow);
        note = UiTheme.Text("Set your time, then press play on the bar.",12,ink:UiTheme.Muted); note.Margin = new Thickness(0,14,0,8); panel.Children.Add(note);
        Bind(TimeCard,() => host.EditTime(editOrder,TimeField.Focus,TimerModel.RoundDuration(TimerModel.ParseTime(TimeCard.Text))));
        Bind(BreakCard,() => host.EditTime(editOrder,TimeField.Break,TimerModel.RoundDuration(TimerModel.ParseTime(BreakCard.Text))));
        Bind(StartTimeCard,() => {
            var start = ClockTime.Parse(StartTimeCard.Text,host.PreviewStart,host.Now);
            if (start < host.Now && start != TimerModel.EstimatedEnd(host.Now,0)) {
                EndEditSession(); host.SetStart(start);
                syncing = true; StartTimeCard.Text = TimerModel.EstimatedEnd(host.Now,0).ToString("t"); syncing = false;
                Error("Start time cannot be in the past. Reset to now.");
            } else host.EditTime(editOrder,TimeField.Start,host.EditClockSeconds(start < host.Now ? host.Now : start));
        });
        Bind(FocusEndCard,() => host.EditTime(editOrder,TimeField.FocusEnd,host.EditClockSeconds(ClockTime.Parse(FocusEndCard.Text,host.FocusEnd,host.Now))));
        Bind(BreakEndCard,() => host.EditTime(editOrder,TimeField.BreakEnd,host.EditClockSeconds(ClockTime.Parse(BreakEndCard.Text,host.BreakEnd,host.Now))));
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
    private TextBox TimeInput(double size,string name)
    {
        var field = new TextBox {
            FontFamily = size > 30 ? UiTheme.Digits : FontFamily, FontSize = size,
            Foreground = UiTheme.Ink, Background = Brushes.Transparent, BorderBrush = UiTheme.Line,
            BorderThickness = new Thickness(0,0,0,1), Padding = new Thickness(0),
            MaxLength = 40, MinWidth = size > 30 ? 100 : 86, Width = size > 30 ? double.NaN : 96, CaretBrush = UiTheme.Focus,
            SelectionBrush = UiTheme.Line, ToolTip = name
        };
        AutomationProperties.SetName(field,name); return field;
    }
    private static FrameworkElement TimerGlyph(bool pause,bool skip)
    {
        var data = pause ? "M 5,2 L 5,16 M 12,2 L 12,16" : skip ? "M 2,2 L 12,9 L 2,16 Z M 16,2 L 16,16" : "M 3,2 L 14,9 L 3,16 Z";
        return new System.Windows.Shapes.Path { Data = Geometry.Parse(data), Width = 18, Height = 18,
            Stroke = skip ? UiTheme.Ink : UiTheme.Background, StrokeThickness = 2,
            Fill = pause ? null : skip ? UiTheme.Ink : UiTheme.Background };
    }
    private void Bind(TextBox field,Action apply)
    {
        field.GotKeyboardFocus += (_,_) => { activeField = field; editing = true; dirty = false; validDraft = true; field.BorderBrush = UiTheme.Focus; };
        field.TextChanged += (_,_) => {
            if (syncing || activeField != field) return;
            dirty = true;
            try { validDraft = true; Note("Changes update as you type; durations round to the nearest minute."); apply(); }
            catch (ArgumentException ex) { validDraft = false; Error(ex.Message); }
            UpdateEditStatus(); host.ScheduleWake();
        };
        field.LostKeyboardFocus += (_,_) => { field.BorderBrush = UiTheme.Line; CancelEdit(); };
        field.PreviewKeyDown += (_,e) => {
            if (e.Key == Key.Enter) { if (FinishEdit()) Keyboard.ClearFocus(); e.Handled = true; }
            else if (e.Key == Key.Escape) { CancelEdit(); Keyboard.ClearFocus(); e.Handled = true; }
        };
    }
    internal void CancelEdit() { editing = false; dirty = false; activeField = null; validDraft = true; Sync(); host.ScheduleWake(); }
    internal void EndEditSession() { editOrder.Clear(); CancelEdit(); }
    private void UpdateEditStatus()
    {
        if (editStatus is null) return;
        double left = editOrder.SecondsLeft(host.EditSeconds);
        editStatus.Text = left > 0 ? $"Edit order: {editOrder.Description}. Unlocks in {Math.Ceiling(left):0}s."
            : "Edit any time. Your first edit stays fixed for 8 seconds after your latest change.";
    }
    internal bool FinishEdit() { if (dirty && !validDraft) return false; CancelEdit(); return true; }
    internal void Error(string text) { note.Text = text; note.Foreground = UiTheme.Error; inputError.Text = text; inputError.Visibility = Visibility.Visible; }
    internal void Sync()
    {
        SyncCount++;
        UpdateEditStatus();
        var t = host.Timer;
        void Set(TextBox field,string value) {
            if (activeField == field || field.Text == value) return;
            syncing = true; field.Text = value; syncing = false;
        }
        Set(TimeCard,TimerModel.FormatTime(t.IsFocus ? t.Remaining : t.Focus));
        Set(BreakCard,TimerModel.FormatTime(t.IsFocus ? t.Rest : t.Remaining));
        void SetClock(TextBox field,DateTimeOffset value) {
            var rounded = TimerModel.EstimatedEnd(value,0);
            Set(field,rounded.ToString("t"));
            dayLabels[field].Text = rounded.Date == host.Now.Date ? "" : rounded.Date == host.Now.Date.AddDays(1) ? "tomorrow" : rounded.ToString("MMM d");
        }
        SetClock(StartTimeCard,host.PreviewStart); SetClock(FocusEndCard,host.FocusEnd); SetClock(BreakEndCard,host.BreakEnd);
        TimeCard.Foreground = t.IsFocus ? host.CountdownBrush : UiTheme.Ink;
        BreakCard.Foreground = t.IsFocus ? UiTheme.Rest : host.CountdownBrush;
        TimerPanel.Set(t.Scheduled ? t.StartProgress : t.Progress,host.CountdownBrush);
        session.Text = $"{(t.Focus+t.Rest)/60:0.##} min total";
        double fraction = t.Focus/(t.Focus+t.Rest);
        cycle.Set(fraction,t.Scheduled ? 0 : t.Progress,!t.IsFocus && t.Waiting);
        double labelFraction = Math.Clamp(fraction,.35,.85);
        milestoneGrid.ColumnDefinitions[0].Width = new GridLength(labelFraction,GridUnitType.Star);
        milestoneGrid.ColumnDefinitions[1].Width = new GridLength(1-labelFraction,GridUnitType.Star);
        status.Text = t.Scheduled ? "Scheduled" : t.Waiting ? "Time is up" : t.Running ? (t.IsFocus ? "Focusing" : "On break") : "Paused";
        StartCountdown.Text = "Starts in " + TimerModel.FormatTime(t.StartRemaining);
        StartCountdown.Visibility = t.Scheduled ? Visibility.Visible : Visibility.Collapsed;
        status.Foreground = t.Scheduled ? UiTheme.Scheduled : t.Waiting ? UiTheme.Warning : UiTheme.Muted;
        bool playing = t.Running || t.Scheduled;
        if (displayedPlaying != playing) { PlayButton.Content = TimerGlyph(playing,false); displayedPlaying = playing; }
        PlayButton.ToolTip = t.Scheduled ? "Cancel scheduled start" : "Play / pause";
        AlarmButton.Visibility = t.Waiting ? Visibility.Visible : Visibility.Collapsed;
        ShowBar.IsChecked = host.Bar.IsVisible; ResizeBar.IsChecked = host.Bar.ResizeEnabled;
        OrientationButton.Content = host.Bar.Vertical ? "Use horizontal bar" : "Use vertical bar";

    }
    internal double SecondsUntilPreviewChange()
    {
        var t = host.Timer;
        double lockWake = editOrder.SecondsLeft(host.EditSeconds) > 0 ? Math.Min(1,editOrder.SecondsLeft(host.EditSeconds)) : double.PositiveInfinity;
        if (t.Running || t.Waiting || t.Scheduled) return lockWake;
        double Boundary(double offset) {
            double seconds = (host.Now.AddSeconds(offset).Ticks % TimeSpan.TicksPerMinute)/(double)TimeSpan.TicksPerSecond;
            return seconds < 30 ? 30-seconds : 90-seconds;
        }
        return Math.Min(lockWake,Math.Min(Boundary(0),Math.Min(Boundary(t.Remaining),Boundary(t.Remaining + (t.IsFocus ? t.Rest : 0)))));
    }
    internal void Note(string text) { note.Text = text; note.Foreground = UiTheme.Muted; inputError.Visibility = Visibility.Collapsed; }
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
    private void CloseEditor() { CancelEdit(); host.CloseWindow(this); }
}

internal sealed class CycleStrip : FrameworkElement
{
    private static readonly Pen TickPen = UiTheme.Pen(UiTheme.Line,1), ProgressPen = UiTheme.Pen(UiTheme.Ink,2);
    private double focus = 5d/6, progress;
    private bool breakComplete;
    internal void Set(double focusFraction,double elapsed,bool completedBreak = false)
    {
        if (focus == focusFraction && progress == elapsed && breakComplete == completedBreak) return;
        focus = focusFraction; progress = elapsed; breakComplete = completedBreak; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        double width = Math.Max(0,ActualWidth), split = width*focus;
        double start = 0;
        foreach (var band in UiTheme.FocusTimelineBands) {
            double end = split*band.End;
            double gap = Math.Min(1,(end-start)/4);
            dc.DrawRectangle(band.Color,null,new Rect(start,8,Math.Max(0,end-start-gap),8));
            start = end;
        }
        dc.DrawRectangle(breakComplete ? UiTheme.Complete : UiTheme.Rest,null,new Rect(split,8,width-split,8));
        dc.DrawLine(TickPen,new Point(1,4),new Point(1,21));
        dc.DrawLine(TickPen,new Point(split,4),new Point(split,21));
        dc.DrawLine(TickPen,new Point(Math.Max(1,width-1),4),new Point(Math.Max(1,width-1),21));
        double position = Math.Clamp(width*progress,1,Math.Max(1,width-1));
        dc.DrawLine(ProgressPen,new Point(position,5),new Point(position,20));
        dc.DrawEllipse(UiTheme.Ink,null,new Point(position,5),2,2);
    }
}
