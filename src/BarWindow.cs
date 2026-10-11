using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using PomodoroBar.Core;

namespace PomodoroBar;

internal sealed class BarWindow : Window
{
    internal readonly BarSurface Surface;
    private readonly AppHost host;
    public bool ResizeEnabled { get; private set; }
    public BarWindow(AppHost host)
    {
        this.host = host;
        Title = BuildChannel.DisplayName;
        Icon = AppIcon.Source;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        Topmost = true; ShowInTaskbar = true; Background = BarSurface.Brush("#171c28");
        MinWidth = 130; MinHeight = 20; MaxWidth = 1000; MaxHeight = 120;
        Surface = new BarSurface(host); Content = Surface;
        Width = host.Settings.Width; Height = host.Settings.Height;
        Left = host.Settings.X; Top = host.Settings.Y;
        SourceInitialized += (_, _) => RestoreReachablePosition();
        SizeChanged += (_, _) => { Surface.InvalidateVisual(); host.ScheduleSave(); };
        LocationChanged += (_, _) => host.ScheduleSave();
        Closing += (_, e) => { if (!host.Closing) { e.Cancel = true; Dispatcher.BeginInvoke(host.Quit); } };
        var menu = new ContextMenu();
        void Item(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => { host.Silence(); action(); }; menu.Items.Add(item); }
        Item("Edit timer", host.OpenEditor); Item("Reset session", host.Reset);
        menu.Items.Add(new Separator()); Item("Quit", host.Quit); ContextMenu = menu;
        PreviewMouseDown += (_, _) => host.Silence();
        KeyDown += (_, e) => { if (e.Key == Key.Space) { host.Silence(); host.Toggle(); e.Handled = true; } };
    }

    private void RestoreReachablePosition()
    {
        // WPF uses DIPs. Convert monitor work areas from physical pixels at this HWND's DPI.
        var source = PresentationSource.FromVisual(this);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var current = new Rect(Left, Top, Width, Height);
        foreach (var screen in System.Windows.Forms.Screen.AllScreens) {
            var bounds = screen.WorkingArea;
            var r = new Rect(fromDevice.Transform(new Point(bounds.Left, bounds.Top)), fromDevice.Transform(new Point(bounds.Right, bounds.Bottom)));
            var visible = Rect.Intersect(r, current);
            if (!visible.IsEmpty && visible.Width >= 40 && visible.Height >= 20) return;
        }
        Left = SystemParameters.WorkArea.Left + 80; Top = SystemParameters.WorkArea.Top + 80;
    }

    public void SetResize(bool enabled)
    {
        ResizeEnabled = enabled;
        ResizeMode = enabled ? ResizeMode.CanResize : ResizeMode.NoResize;
        WindowChrome.SetWindowChrome(this, enabled ? new WindowChrome {
            CaptionHeight = 0, ResizeBorderThickness = new Thickness(5), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0)
        } : null);
        Surface.InvalidateVisual(); host.ScheduleSave();
    }
    public void ResetSize() { Width = 280; Height = 52; host.ScheduleSave(); }
}

internal sealed class BarSurface : FrameworkElement
{
    private readonly AppHost host;
    private BarLayout layout;
    private int pressed = -1;
    private bool dragArmed;
    private Point down;
    internal BarLayout Layout => layout;
    internal static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static readonly Typeface Typeface = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    public BarSurface(AppHost host)
    {
        this.host = host;
        ToolTip = "Drag to move · pencil to edit · right-click for more";
        MouseLeftButtonDown += Press;
        MouseMove += Move;
        MouseLeftButtonUp += Release;
        LostMouseCapture += (_, _) => { pressed = -1; dragArmed = false; };
        AutomationProperties.SetName(this, "Pomodoro timer. Space plays or pauses. Right-click opens controls.");
    }
    protected override void OnRender(DrawingContext dc)
    {
        var timer = host.Timer;
        double pulse = host.Pulsing ? (.5 + .5 * Math.Sin(Environment.TickCount64 * Math.PI / 500d)) * .55 : 0;
        byte Blend(byte a, byte b) => (byte)Math.Round(a+(b-a)*pulse);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(Blend(23,255),Blend(28,188),Blend(40,112))), null, new Rect(RenderSize));
        var rgb = VisualMath.CountdownColor(timer.Remaining / timer.Duration);
        var text = new FormattedText(TimerModel.FormatTime(timer.Remaining), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, 28,
            new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        layout = BarLayout.Fit(ActualWidth, ActualHeight, text.Width + 6);
        dc.PushTransform(new TranslateTransform(layout.Left, layout.Top));
        dc.PushTransform(new ScaleTransform(layout.Scale, layout.Scale));
        var phase = Brush(timer.IsFocus ? "#9bafff" : "#66dbb7");
        var pen = new Pen(phase, 3);
        dc.DrawEllipse(null, new Pen(Brush("#343e54"), 3), new Point(22,26), 12,12);
        if (timer.Progress >= .9999) dc.DrawEllipse(null, pen, new Point(22,26),12,12);
        else if (timer.Progress > .0001) {
            double angle = timer.Progress * Math.PI * 2;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open()) {
                ctx.BeginFigure(new Point(22,14), false, false);
                ctx.ArcTo(new Point(22 + 12 * Math.Sin(angle), 26 - 12 * Math.Cos(angle)), new Size(12,12), 0, angle > Math.PI, SweepDirection.Clockwise, true, false);
            }
            dc.DrawGeometry(null, pen, geometry);
        }
        if (timer.Waiting) {
            var bang = new FormattedText("!", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, 19, phase, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(bang, new Point(22 - bang.Width/2, 14));
        } else dc.DrawEllipse(phase,null,new Point(22,26),2,2);
        dc.DrawText(text, new Point(40, (52-text.Height)/2));
        double x = 40 + layout.TimerWidth;
        var glyph = new Pen(Brush("#eef1fa"), 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (timer.Running) {
            dc.DrawLine(glyph, new Point(x+12,19), new Point(x+12,33)); dc.DrawLine(glyph,new Point(x+20,19),new Point(x+20,33));
        } else Triangle(dc, new Point(x+12,18), new Point(x+12,34), new Point(x+23,26), glyph.Brush);
        x += 32;
        Triangle(dc, new Point(x+10,19), new Point(x+10,33), new Point(x+20,26), glyph.Brush);
        dc.DrawLine(glyph,new Point(x+23,19),new Point(x+23,33));
        x += 32;
        dc.DrawLine(glyph,new Point(x+11,32),new Point(x+23,20));
        dc.DrawLine(new Pen(glyph.Brush,5),new Point(x+12,31),new Point(x+22,21));
        dc.DrawLine(glyph,new Point(x+9,35),new Point(x+14,34));
        if (host.Bar.ResizeEnabled) dc.DrawRectangle(null, new Pen(Brush("#9bafff"),1),new Rect(.5,.5,layout.LogicalWidth-1,51));
        dc.Pop(); dc.Pop();
    }
    private static void Triangle(DrawingContext dc, Point a, Point b, Point c, Brush brush)
    {
        var g = new StreamGeometry(); using (var ctx = g.Open()) { ctx.BeginFigure(a,true,true); ctx.LineTo(b,true,false); ctx.LineTo(c,true,false); }
        dc.DrawGeometry(brush,null,g);
    }
    private void Press(object sender, MouseButtonEventArgs e)
    {
        host.Silence(); down = e.GetPosition(this); pressed = layout.Hit(down.X,down.Y); dragArmed = pressed is -1 or 1; CaptureMouse(); e.Handled = true;
    }
    private void Move(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        Cursor = layout.Hit(p.X,p.Y) is 0 or 2 or 3 or 4 ? Cursors.Hand : Cursors.SizeAll;
        if (dragArmed && e.LeftButton == MouseButtonState.Pressed && (p-down).Length > 4) {
            pressed = -1; ReleaseMouseCapture();
            try { host.Bar.DragMove(); } catch (InvalidOperationException) { }
        }
    }
    private void Release(object sender, MouseButtonEventArgs e)
    {
        int action = pressed; pressed = -1; dragArmed = false; ReleaseMouseCapture();
        if (action == 0) host.Acknowledge();
        else if (action == 2) host.Toggle();
        else if (action == 3) host.Skip();
        else if (action == 4) host.OpenEditor();
        host.Render(); e.Handled = true;
    }
}
