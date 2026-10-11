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
    internal readonly CheckBox ShowBar;
    internal readonly CheckBox ResizeBar;
    private readonly TextBlock heading, session, status, note, update, focusLength, breakLength;
    private readonly CycleStrip cycle;
    internal readonly Button AlarmButton;
    internal readonly Button CheckUpdateButton, InstallUpdateButton;
    private bool editing, dirty, syncing;
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
        ResizeBar.Click += (_,_) => { if (ResizeBar.IsChecked == true) host.Bar.Show(); host.Bar.SetResize(ResizeBar.IsChecked == true); host.Render(); }; panel.Children.Add(ResizeBar);
        var updatePanel = new StackPanel { Margin = new Thickness(0,10,0,0) };
        update = UiTheme.Text($"Version {BuildChannel.Version}",12,ink:UiTheme.Muted); updatePanel.Children.Add(update);
        var updateActions = new UniformGrid { Columns = 2, Margin = new Thickness(0,8,0,0) };
        CheckUpdateButton = UiTheme.Button("Check for updates",host.CheckUpdates); CheckUpdateButton.IsEnabled = false;
        InstallUpdateButton = UiTheme.Button("Update and restart",host.InstallUpdate,true); InstallUpdateButton.IsEnabled = false;
        updateActions.Children.Add(CheckUpdateButton); updateActions.Children.Add(InstallUpdateButton); updatePanel.Children.Add(updateActions);
        var updates = UiTheme.Disclosure("App updates",updatePanel);
        panel.Children.Add(updates);
        note = UiTheme.Text("Set your time, then press play on the bar.",12,ink:UiTheme.Muted); note.Margin = new Thickness(0,14,0,8); panel.Children.Add(note);
        var actions = new Grid(); actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition());
        var quit = UiTheme.Button("Quit app",host.Quit); quit.Background = Brushes.Transparent; quit.BorderBrush = Brushes.Transparent; quit.HorizontalAlignment = HorizontalAlignment.Left;
        var done = UiTheme.Button("Done",CloseEditor,true); done.MinWidth = 112; done.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(done,1);
        actions.Children.Add(quit); actions.Children.Add(done); panel.Children.Add(actions);
        TimeCard.GotKeyboardFocus += (_,_) => { editing = true; dirty = false; };
        TimeCard.TextChanged += (_,_) => { if (editing && !syncing) dirty = true; };
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
    internal void CancelEdit() { editing = false; dirty = false; Sync(); }
    internal bool FinishEdit()
    {
        if (!editing) return true;
        if (dirty) {
            try { host.SetTime(TimerModel.ParseTime(TimeCard.Text)); }
            catch (ArgumentException ex) { Note(ex.Message); return false; }
        }
        editing = false; dirty = false; Sync(); return true;
    }
    internal void Sync()
    {
        var t = host.Timer;
        heading.Text = t.IsFocus ? "Focus time" : "Break time";
        heading.Foreground = t.IsFocus ? UiTheme.Focus : UiTheme.Rest;
        if (!editing) { syncing = true; TimeCard.Text = TimerModel.FormatTime(t.Remaining); syncing = false; }
        var color = VisualMath.CountdownColor(t.Remaining/t.Duration);
        TimeCard.Foreground = new SolidColorBrush(Color.FromRgb(color.R,color.G,color.B));
        session.Text = $"{(t.Focus+t.Rest)/60:0.##} min total";
        focusLength.Text = TimerModel.FormatTime(t.Focus) + " focus";
        breakLength.Text = TimerModel.FormatTime(t.Rest) + " break";
        cycle.Set(t.Focus/(t.Focus+t.Rest),t.Progress);
        status.Text = t.Waiting ? "Time is up" : t.Running ? "Running" : "Paused";
        status.Foreground = t.Waiting ? UiTheme.Color("#FFAE66") : UiTheme.Muted;
        AlarmButton.Visibility = t.Waiting ? Visibility.Visible : Visibility.Collapsed;
        ShowBar.IsChecked = host.Bar.IsVisible; ResizeBar.IsChecked = host.Bar.ResizeEnabled;
    }
    internal void Note(string text) => note.Text = text;
    internal void UpdateDetails(UpdateService service)
    {
        static string Stamp(DateTimeOffset? time) => time?.LocalDateTime.ToString("MMM d 'at' h:mm tt") ?? "Never";
        update.Text = $"Version {BuildChannel.Version}\n{service.State.Text}\nLast checked: {Stamp(service.LastCheckedAt)}\nLast updated: {Stamp(service.LastUpdatedAt)}";
        CheckUpdateButton.IsEnabled = service.CanUpdate && !service.IsBusy;
        InstallUpdateButton.IsEnabled = service.CanUpdate && service.HasPending;
    }
    private void CloseEditor() { if (FinishEdit()) Hide(); }
}

internal sealed class CycleStrip : FrameworkElement
{
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
            dc.DrawLine(new Pen(UiTheme.Line,1),new Point(x,20),new Point(x,i%3==0 ? 27 : 24));
        }
        double position = Math.Clamp(width*progress,1,Math.Max(1,width-1));
        dc.DrawLine(new Pen(UiTheme.Ink,2),new Point(position,5),new Point(position,20));
        dc.DrawEllipse(UiTheme.Ink,null,new Point(position,5),2,2);
    }
}
