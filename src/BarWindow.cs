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
    public bool Vertical { get; private set; }
    public BarWindow(AppHost host)
    {
        this.host = host;
        Title = BuildChannel.DisplayName;
        Icon = AppIcon.Source;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        Topmost = true; ShowInTaskbar = true; Background = UiTheme.Background;
        Vertical = host.Settings.Vertical; ApplyLimits();
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
    private void ApplyLimits()
    {
        MinWidth = 0; MinHeight = 0; MaxWidth = double.PositiveInfinity; MaxHeight = double.PositiveInfinity;
        MaxWidth = Vertical ? 300 : 1000; MaxHeight = Vertical ? 1000 : 120;
        MinWidth = Vertical ? 52 : 130; MinHeight = Vertical ? 130 : 20;
    }
    public void SetVertical(bool vertical)
    {
        if (Vertical == vertical) return;
        Vertical = vertical; ApplyLimits(); ResetSize(); RestoreReachablePosition(); host.Render();
    }
    public void ResetSize() { Width = Vertical ? 110 : 280; Height = Vertical ? 180 : 52; host.ScheduleSave(); }
}

internal sealed class BarSurface : FrameworkElement
{
    private readonly AppHost host;
    private BarLayout layout;
    private int pressed = -1, hovered = -1;
    private bool dragArmed;
    private Point down;
    internal BarLayout Layout => layout;
    internal static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static readonly Typeface Typeface = new(UiTheme.Digits,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
    private static readonly Pen BorderPen = UiTheme.Pen(UiTheme.Line,3), FocusPen = UiTheme.Pen(UiTheme.Focus,3,true),
        RestPen = UiTheme.Pen(UiTheme.Rest,3,true), ScheduledPen = UiTheme.Pen(UiTheme.Scheduled,3,true),
        GlyphPen = UiTheme.Pen(UiTheme.Ink,2,true), PlayPen = UiTheme.Pen(UiTheme.Background,2,true);
    private FormattedText? text;
    private int cachedSecond = -1;
    private double cachedDpi;
    private Brush? cachedBrush;
    private PathGeometry? outline;
    private Size outlineSize;
    private Geometry? progressGeometry;
    private double cachedProgress = -1;
    private readonly SolidColorBrush pulseBrush = new();
    private (int Second,double Progress,bool Focus,bool Running,bool Waiting,bool Pulse,bool Resize,bool Vertical,bool Scheduled)? displayed;
    internal int RenderCount { get; private set; }
    internal void Refresh()
    {
        var t = host.Timer;
        var state = ((int)Math.Ceiling(t.Scheduled ? t.StartRemaining : t.Remaining),t.Scheduled ? t.StartProgress : t.Progress,t.IsFocus,t.Running,t.Waiting,host.Pulsing,host.Bar.ResizeEnabled,host.Bar.Vertical,t.Scheduled);
        if (displayed == state) return;
        displayed = state; InvalidateVisual();
    }
    public BarSurface(AppHost host)
    {
        this.host = host;
        ToolTip = "Drag countdown to move · pencil to edit · right-click for more";
        MouseLeftButtonDown += Press; MouseMove += Move; MouseLeftButtonUp += Release;
        LostMouseCapture += (_,_) => { pressed = -1; dragArmed = false; };
        MouseLeave += (_,_) => { hovered = -1; InvalidateVisual(); };
        AutomationProperties.SetName(this,"Pomodoro timer. Space plays or pauses. Right-click opens controls.");
    }
    protected override void OnRender(DrawingContext dc)
    {
        RenderCount++;
        var t = host.Timer;
        double remaining = t.Scheduled ? t.StartRemaining : t.Remaining;
        double progress = t.Scheduled ? t.StartProgress : t.Progress;
        Brush phase = t.Scheduled ? UiTheme.Scheduled : t.IsFocus ? UiTheme.Focus : UiTheme.Rest;
        Pen phasePen = t.Scheduled ? ScheduledPen : t.IsFocus ? FocusPen : RestPen;
        double pulse = host.Pulsing ? (SystemParameters.ClientAreaAnimation ? .5+.5*Math.Sin(Environment.TickCount64*Math.PI/500d) : .6)*.55 : 0;
        byte Blend(byte a,byte b) => (byte)Math.Round(a+(b-a)*pulse);
        pulseBrush.Color = Color.FromRgb(Blend(41,255),Blend(54,188),Blend(64,112));
        dc.DrawRectangle(UiTheme.Background,null,new Rect(RenderSize));
        var bounds = new Rect(2,2,Math.Max(0,ActualWidth-4),Math.Max(0,ActualHeight-4));
        dc.DrawRoundedRectangle(pulse == 0 ? UiTheme.Surface : pulseBrush,BorderPen,bounds,7,7);
        if (outline is null || outlineSize != RenderSize) {
            outline = new RectangleGeometry(bounds,7,7).GetFlattenedPathGeometry(.2,ToleranceType.Absolute);
            outline.Freeze(); outlineSize = RenderSize; cachedProgress = -1;
        }
        if (cachedProgress != progress) { progressGeometry = PartialOutline(outline,progress); cachedProgress = progress; }
        if (progressGeometry is not null) dc.DrawGeometry(null,phasePen,progressGeometry);
        int second = (int)Math.Ceiling(remaining);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (text is null || cachedSecond != second || cachedDpi != dpi) {
            text = new FormattedText(TimerModel.FormatTime(remaining),CultureInfo.InvariantCulture,FlowDirection.LeftToRight,Typeface,28,UiTheme.Ink,dpi);
            cachedSecond = second; cachedDpi = dpi; cachedBrush = null;
        }
        Brush countdown;
        if (t.Scheduled) countdown = UiTheme.Scheduled;
        else {
            var rgb = VisualMath.CountdownColor(remaining/t.Duration);
            if (cachedBrush is SolidColorBrush previous && previous.Color == Color.FromRgb(rgb.R,rgb.G,rgb.B)) countdown = previous;
            else { var brush = new SolidColorBrush(Color.FromRgb(rgb.R,rgb.G,rgb.B)); brush.Freeze(); countdown = brush; }
        }
        if (cachedBrush != countdown) { text.SetForegroundBrush(countdown); cachedBrush = countdown; }
        layout = BarLayout.Fit(ActualWidth,ActualHeight,text.Width+6,host.Bar.Vertical);
        dc.PushTransform(new MatrixTransform(new Matrix(layout.Scale,0,0,layout.Scale,layout.Left,layout.Top)));
        dc.DrawText(text,new Point(layout.Vertical ? (layout.LogicalWidth-text.Width)/2 : 8,(52-text.Height)/2));
        for (int i=0;i<3;i++) {
            double x = layout.Vertical ? (layout.LogicalWidth-28)/2 : layout.TimerWidth+12+i*32+2;
            double y = layout.Vertical ? 54+i*36 : 10;
            dc.DrawRoundedRectangle(i == 0 ? phase : hovered == i+2 ? UiTheme.Line : UiTheme.Surface,null,new Rect(x,y,28,32),6,6);
            var pen = i == 0 ? PlayPen : GlyphPen;
            if (i == 0 && (t.Running || t.Scheduled)) {
                dc.DrawLine(pen,new Point(x+10,y+9),new Point(x+10,y+23)); dc.DrawLine(pen,new Point(x+18,y+9),new Point(x+18,y+23));
            } else if (i < 2) {
                var g = new StreamGeometry();
                using (var ctx = g.Open()) { ctx.BeginFigure(new Point(x+9,y+8),true,true); ctx.LineTo(new Point(x+21,y+16),true,false); ctx.LineTo(new Point(x+9,y+24),true,false); }
                dc.DrawGeometry(pen.Brush,null,g);
                if (i == 1) dc.DrawLine(pen,new Point(x+23,y+8),new Point(x+23,y+24));
            } else {
                dc.DrawLine(pen,new Point(x+8,y+23),new Point(x+21,y+10));
                dc.DrawLine(pen,new Point(x+10,y+24),new Point(x+23,y+11));
                dc.DrawLine(pen,new Point(x+6,y+26),new Point(x+11,y+25));
            }
        }
        dc.Pop();
    }
    private static Geometry? PartialOutline(PathGeometry outline,double fraction)
    {
        if (fraction <= 0) return null;
        if (fraction >= 1) return outline;
        var points = new System.Collections.Generic.List<Point>();
        foreach (var figure in outline.Figures) {
            points.Add(figure.StartPoint);
            foreach (var segment in figure.Segments) {
                if (segment is PolyLineSegment poly) points.AddRange(poly.Points);
                else if (segment is LineSegment line) points.Add(line.Point);
            }
            if (figure.IsClosed) points.Add(figure.StartPoint);
        }
        double length = 0;
        for (int i=1;i<points.Count;i++) length += (points[i]-points[i-1]).Length;
        double left = length*fraction;
        var result = new StreamGeometry();
        using (var ctx = result.Open()) {
            ctx.BeginFigure(points[0],false,false);
            for (int i=1;i<points.Count && left > 0;i++) {
                double distance = (points[i]-points[i-1]).Length;
                if (distance == 0) continue;
                ctx.LineTo(left >= distance ? points[i] : points[i-1]+(points[i]-points[i-1])*(left/distance),true,false);
                left -= distance;
            }
        }
        result.Freeze(); return result;
    }
    private void Press(object sender,MouseButtonEventArgs e)
    {
        host.Silence(); down = e.GetPosition(this); pressed = layout.Hit(down.X,down.Y); dragArmed = pressed is -1 or 1; CaptureMouse(); e.Handled = true;
    }
    private void Move(object sender,MouseEventArgs e)
    {
        var p = e.GetPosition(this); int hit = layout.Hit(p.X,p.Y);
        if (hovered != hit) { hovered = hit; InvalidateVisual(); }
        Cursor = hit is 2 or 3 or 4 ? Cursors.Hand : Cursors.SizeAll;
        if (dragArmed && e.LeftButton == MouseButtonState.Pressed && (p-down).Length > 4) {
            pressed = -1; ReleaseMouseCapture(); try { host.Bar.DragMove(); } catch (InvalidOperationException) { }
        }
    }
    private void Release(object sender,MouseButtonEventArgs e)
    {
        int action = pressed; pressed = -1; dragArmed = false; ReleaseMouseCapture();
        if (action == 2) host.Toggle(); else if (action == 3) host.Skip(); else if (action == 4) host.OpenEditor();
        host.Render(); e.Handled = true;
    }
}
