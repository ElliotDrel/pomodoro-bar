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
        Topmost = true; ShowInTaskbar = true; Background = UiTheme.Background;
        MinWidth = 130; MinHeight = 20; MaxWidth = 1000; MaxHeight = 120;
        Surface = new BarSurface(host); Content = Surface;
        Width = host.Settings.Width; Height = host.Settings.Height;
        Left = host.Settings.X; Top = host.Settings.Y;
        SourceInitialized += (_, _) => RestoreReachablePosition();
        SizeChanged += (_, _) => { Surface.InvalidateVisual(); host.ScheduleSave(); };
        LocationChanged += (_, _) => host.ScheduleSave();
        Closing += (_, e) => { if (!host.Closing) { e.Cancel = true; host.Silence(); Hide(); host.Render(); } };
        var menu = UiTheme.Menu();
        void Item(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => { host.Silence(); action(); }; menu.Items.Add(item); }
        Item("Edit timer", host.OpenEditor); Item("Reset session", host.Reset); Item("Reset bar size",ResetSize);
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
        foreach (var bounds in MonitorAreas.WorkingAreas()) {
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
    private int hovered = -1;
    private bool dragArmed;
    private Point down;
    internal BarLayout Layout => layout;
    internal static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static readonly Typeface Typeface = new(UiTheme.Digits, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Pen BorderPen = UiTheme.Pen(UiTheme.Line,1), ResizePen = UiTheme.Pen(UiTheme.Focus,1),
        RingPen = UiTheme.Pen(UiTheme.Line,3), FocusPen = UiTheme.Pen(UiTheme.Focus,3), RestPen = UiTheme.Pen(UiTheme.Rest,3),
        HoverPen = UiTheme.Pen(UiTheme.Ink,1), PlayPen = UiTheme.Pen(UiTheme.Background,2,true),
        GlyphPen = UiTheme.Pen(UiTheme.Ink,2,true), PencilPen = UiTheme.Pen(UiTheme.Ink,5);
    private FormattedText? cachedText, cachedBang;
    private int cachedSecond = -1;
    private double cachedDpi, cachedWidth = -1, cachedHeight = -1, cachedTextWidth = -1, cachedProgress = -1;
    private Rgb? cachedColor;
    private StreamGeometry? cachedArc, playTriangle, skipTriangle;
    private MatrixTransform? layoutTransform;
    private readonly SolidColorBrush pulseBrush = new();
    private (double Remaining, double Progress, bool Focus, bool Running, bool Waiting, bool Pulse, bool Resize)? displayed;
    internal int RenderCount { get; private set; }
    internal void Refresh()
    {
        var t = host.Timer;
        var state = (t.Remaining,t.Progress,t.IsFocus,t.Running,t.Waiting,host.Pulsing,host.Bar.ResizeEnabled);
        if (displayed == state) return;
        displayed = state; InvalidateVisual();
    }
    public BarSurface(AppHost host)
    {
        this.host = host;
        ToolTip = "Drag to move · pencil to edit · right-click for more";
        MouseLeftButtonDown += Press;
        MouseMove += Move;
        MouseLeftButtonUp += Release;
        LostMouseCapture += (_, _) => { pressed = -1; dragArmed = false; };
        MouseLeave += (_,_) => { hovered = -1; InvalidateVisual(); };
        AutomationProperties.SetName(this, "Pomodoro timer. Space plays or pauses. Right-click opens controls.");
    }
    protected override void OnRender(DrawingContext dc)
    {
        RenderCount++;
        var timer = host.Timer;
        double pulse = host.Pulsing ? (SystemParameters.ClientAreaAnimation ? .5 + .5 * Math.Sin(Environment.TickCount64 * Math.PI / 500d) : .6) * .55 : 0;
        byte Blend(byte a, byte b) => (byte)Math.Round(a+(b-a)*pulse);
        dc.DrawRectangle(UiTheme.Background,null,new Rect(RenderSize));
        pulseBrush.Color = Color.FromRgb(Blend(41,255),Blend(54,188),Blend(64,112));
        dc.DrawRoundedRectangle(pulse == 0 ? UiTheme.Surface : pulseBrush,
            host.Bar.ResizeEnabled ? ResizePen : BorderPen,new Rect(.5,.5,Math.Max(0,ActualWidth-1),Math.Max(0,ActualHeight-1)),8,8);
        var rgb = VisualMath.CountdownColor(timer.Remaining / timer.Duration);
        int second = (int)Math.Ceiling(timer.Remaining);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (cachedText is null || cachedSecond != second || cachedDpi != dpi) {
            cachedText = new FormattedText(TimerModel.FormatTime(timer.Remaining), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, 28,UiTheme.Ink,dpi);
            cachedSecond = second; cachedDpi = dpi; cachedColor = null; cachedBang = null;
        }
        if (cachedColor != rgb) {
            var brush = new SolidColorBrush(Color.FromRgb(rgb.R,rgb.G,rgb.B)); brush.Freeze();
            cachedText.SetForegroundBrush(brush); cachedColor = rgb;
        }
        var text = cachedText;
        if (cachedWidth != ActualWidth || cachedHeight != ActualHeight || cachedTextWidth != text.Width) {
            layout = BarLayout.Fit(ActualWidth,ActualHeight,text.Width+6);
            layoutTransform = new MatrixTransform(new Matrix(layout.Scale,0,0,layout.Scale,layout.Left,layout.Top)); layoutTransform.Freeze();
            cachedWidth = ActualWidth; cachedHeight = ActualHeight; cachedTextWidth = text.Width;
            double offset = 40+layout.TimerWidth;
            playTriangle = Triangle(new Point(offset+12,18),new Point(offset+12,34),new Point(offset+23,26));
            skipTriangle = Triangle(new Point(offset+42,19),new Point(offset+42,33),new Point(offset+52,26));
        }
        dc.PushTransform(layoutTransform!);
        var phase = timer.IsFocus ? UiTheme.Focus : UiTheme.Rest;
        var pen = timer.IsFocus ? FocusPen : RestPen;
        dc.DrawEllipse(null, RingPen, new Point(22,26), 12,12);
        if (timer.Progress >= .9999) dc.DrawEllipse(null, pen, new Point(22,26),12,12);
        else if (timer.Progress > .0001) {
            if (cachedArc is null || cachedProgress != timer.Progress) {
                double angle = timer.Progress * Math.PI * 2;
                cachedArc = new StreamGeometry();
                using (var ctx = cachedArc.Open()) {
                    ctx.BeginFigure(new Point(22,14), false, false);
                    ctx.ArcTo(new Point(22 + 12 * Math.Sin(angle), 26 - 12 * Math.Cos(angle)), new Size(12,12), 0, angle > Math.PI, SweepDirection.Clockwise, true, false);
                }
                cachedArc.Freeze(); cachedProgress = timer.Progress;
            }
            dc.DrawGeometry(null, pen, cachedArc);
        }
        if (timer.Waiting) {
            var bang = cachedBang ??= new FormattedText("!", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, 19, phase,dpi);
            bang.SetForegroundBrush(phase);
            dc.DrawText(bang, new Point(22 - bang.Width/2, 14));
        } else dc.DrawEllipse(phase,null,new Point(22,26),2,2);
        dc.DrawText(text, new Point(40, (52-text.Height)/2));
        double x = 40 + layout.TimerWidth;
        for (int i = 0; i < 3; i++) {
            var fill = i == 0 ? phase : hovered == i+2 ? UiTheme.Line : UiTheme.Surface;
            dc.DrawRoundedRectangle(fill,null,new Rect(x+i*32+3,10,27,32),6,6);
        }
        if (hovered == 0) dc.DrawEllipse(null,HoverPen,new Point(22,26),16,16);
        var playGlyph = PlayPen;
        var glyph = GlyphPen;
        if (timer.Running) {
            dc.DrawLine(playGlyph, new Point(x+12,19), new Point(x+12,33)); dc.DrawLine(playGlyph,new Point(x+20,19),new Point(x+20,33));
        } else dc.DrawGeometry(playGlyph.Brush,null,playTriangle);
        x += 32;
        dc.DrawGeometry(glyph.Brush,null,skipTriangle);
        dc.DrawLine(glyph,new Point(x+23,19),new Point(x+23,33));
        x += 32;
        dc.DrawLine(glyph,new Point(x+11,32),new Point(x+23,20));
        dc.DrawLine(PencilPen,new Point(x+12,31),new Point(x+22,21));
        dc.DrawLine(glyph,new Point(x+9,35),new Point(x+14,34));
        dc.Pop();
    }
    private static StreamGeometry Triangle(Point a, Point b, Point c)
    {
        var g = new StreamGeometry(); using (var ctx = g.Open()) { ctx.BeginFigure(a,true,true); ctx.LineTo(b,true,false); ctx.LineTo(c,true,false); }
        g.Freeze(); return g;
    }
    private void Press(object sender, MouseButtonEventArgs e)
    {
        host.Silence(); down = e.GetPosition(this); pressed = layout.Hit(down.X,down.Y); dragArmed = pressed is -1 or 1; CaptureMouse(); e.Handled = true;
    }
    private void Move(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        int hit = layout.Hit(p.X,p.Y);
        if (hovered != hit) { hovered = hit; InvalidateVisual(); }
        Cursor = hit is 0 or 2 or 3 or 4 ? Cursors.Hand : Cursors.SizeAll;
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
