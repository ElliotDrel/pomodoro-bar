using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PomodoroBar.Core;

namespace PomodoroBar;

internal sealed class EditorWindow : Window
{
    private readonly AppHost host;
    internal readonly TextBox TimeCard;
    internal readonly CheckBox ShowBar;
    internal readonly CheckBox ResizeBar;
    private readonly TextBlock heading, session, status, note, update;
    internal readonly Button AlarmButton;
    private bool editing, dirty, syncing;
    internal bool Editing => editing;
    public EditorWindow(AppHost host)
    {
        this.host = host;
        Title = BuildChannel.DisplayName + " · Edit timer";
        Icon = AppIcon.Source;
        ShowInTaskbar = false;
        Width = 480; Height = 625; MinWidth = 400; MinHeight = 390;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BarSurface.Brush("#222938"); Foreground = BarSurface.Brush("#eef1fa");
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(Label("Study session",24,true));
        var card = new StackPanel { Margin = new Thickness(0,10,0,10) };
        heading = Label("FOCUS · TIME REMAINING",12,true); heading.HorizontalAlignment = HorizontalAlignment.Center;
        card.Children.Add(heading);
        TimeCard = new TextBox {
            FontFamily = new FontFamily("Consolas"), FontSize = 44, FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center, Background = Brushes.Transparent, Foreground = Foreground,
            BorderThickness = new Thickness(0), CaretBrush = BarSurface.Brush("#9bafff"), SelectionBrush = BarSurface.Brush("#394a7b"),
            MaxLength = 12, Padding = new Thickness(4), ToolTip = "Minutes, MM:SS, or HH:MM:SS"
        };
        card.Children.Add(TimeCard);
        var hint = Label("Click to edit · Enter to save · Esc to cancel",12); hint.HorizontalAlignment = HorizontalAlignment.Center; card.Children.Add(hint);
        panel.Children.Add(new Border { Child = card, Background = BarSurface.Brush("#171c28"), BorderBrush = BarSurface.Brush("#343e54"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Margin = new Thickness(0,14,0,8) });
        session = Label("",14); panel.Children.Add(session);
        status = Label("",12); status.Margin = new Thickness(0,4,0,14); panel.Children.Add(status);
        AlarmButton = Button("Acknowledge · start next section",host.Acknowledge);
        AlarmButton.Visibility = Visibility.Collapsed; panel.Children.Add(AlarmButton);
        var presets = new UniformGrid { Columns = 2 };
        presets.Children.Add(Button("30 minutes", () => host.SetSession(30,[40,20])));
        presets.Children.Add(Button("60 minutes", () => host.SetSession(60,[60,20,10]))); panel.Children.Add(presets);
        var adds = new UniformGrid { Columns = 3, Margin = new Thickness(0,6,0,14) };
        foreach (int n in new[] {1,5,10}) adds.Children.Add(Button($"+{n} minute{(n == 1 ? "" : "s")}", () => host.AddMinutes(n))); panel.Children.Add(adds);
        ShowBar = new CheckBox { Content = "Show floating bar", IsChecked = true, Margin = new Thickness(3,8,0,8) };
        ShowBar.Click += (_,_) => { if (ShowBar.IsChecked == true) host.Bar.Show(); else host.Bar.Hide(); };
        panel.Children.Add(ShowBar);
        ResizeBar = new CheckBox { Content = "Resize floating bar · drag its edges", Margin = new Thickness(3,8,0,8) };
        ResizeBar.Click += (_,_) => { if (ResizeBar.IsChecked == true) host.Bar.Show(); host.Bar.SetResize(ResizeBar.IsChecked == true); host.Render(); }; panel.Children.Add(ResizeBar);
        panel.Children.Add(Button("Reset bar size", host.Bar.ResetSize));
        note = Label("Choose a duration, then press play on the bar.",12); note.Margin = new Thickness(0,12,0,8); panel.Children.Add(note);
        update = Label($"Version {BuildChannel.Version} · {BuildChannel.Name}",12); panel.Children.Add(update);
        var actions = new UniformGrid { Columns = 2, Margin = new Thickness(0,12,0,0) };
        actions.Children.Add(Button("Quit",host.Quit)); actions.Children.Add(Button("Done",CloseEditor)); panel.Children.Add(actions);
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
    private static TextBlock Label(string text,double size,bool bold=false) => new() { Text=text, FontSize=size, FontWeight=bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping=TextWrapping.Wrap };
    private static Button Button(string text,Action action)
    {
        var b = new Button { Content=text, Margin=new Thickness(3),Padding=new Thickness(9), Background=BarSurface.Brush("#343e54"),Foreground=BarSurface.Brush("#eef1fa"), BorderBrush=BarSurface.Brush("#46516a") };
        b.Click += (_,_) => action(); return b;
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
        heading.Text = (t.IsFocus ? "FOCUS" : "BREAK") + " · TIME REMAINING";
        if (!editing) { syncing = true; TimeCard.Text = TimerModel.FormatTime(t.Remaining); syncing = false; }
        var color = VisualMath.CountdownColor(t.Remaining/t.Duration);
        TimeCard.Foreground = new SolidColorBrush(Color.FromRgb(color.R,color.G,color.B));
        session.Text = $"{(t.Focus+t.Rest)/60:0.##} minutes · {TimerModel.FormatTime(t.Focus)} focus + {TimerModel.FormatTime(t.Rest)} break";
        status.Text = t.Waiting ? "Time is up · click the bar to silence the alarm." : t.Running ? "Counting down" : "Paused · press play on the bar";
        if (t.Waiting && !host.Bar.IsVisible) status.Text = "Time is up · acknowledge below or show the floating bar.";
        AlarmButton.Visibility = t.Waiting ? Visibility.Visible : Visibility.Collapsed;
        ShowBar.IsChecked = host.Bar.IsVisible; ResizeBar.IsChecked = host.Bar.ResizeEnabled;
    }
    internal void Note(string text) => note.Text = text;
    internal void UpdateStatus(string text) => update.Text = $"Version {BuildChannel.Version} · {text}";
    private void CloseEditor() { if (FinishEdit()) Hide(); }
}
