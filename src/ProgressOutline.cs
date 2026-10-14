using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PomodoroBar;

// Shared perimeter geometry keeps bar and editor progress/color identical.
internal sealed class ProgressOutline
{
    private PathGeometry? outline;
    private Size size;
    private Geometry? progressGeometry;
    private double cachedProgress = -1;
    private Brush? brush;
    private Pen? progressPen, trackPen;
    internal const double Thickness = 6;
    internal void Draw(DrawingContext dc,Size renderSize,double progress,Brush color)
    {
        if (renderSize.Width < 6 || renderSize.Height < 6) return;
        if (outline is null || size != renderSize) {
            var bounds = new Rect(3,3,renderSize.Width-6,renderSize.Height-6);
            outline = new RectangleGeometry(bounds,7,7).GetFlattenedPathGeometry(.2,ToleranceType.Absolute);
            outline.Freeze(); size = renderSize; cachedProgress = -1;
        }
        if (brush != color) {
            brush = color; progressPen = UiTheme.Pen(color,Thickness,true);
            trackPen = UiTheme.Pen(color,4,true);
        }
        if (cachedProgress != progress) { progressGeometry = PartialOutline(outline,progress); cachedProgress = progress; }
        dc.DrawGeometry(null,trackPen,outline);
        if (progressGeometry is not null) dc.DrawGeometry(null,progressPen,progressGeometry);
    }
    internal static Geometry? PartialOutline(PathGeometry outline,double fraction)
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
}

internal sealed class TimerPanel : Border
{
    private readonly ProgressOutline outline = new();
    private Brush color = UiTheme.Ink;
    private double progress;
    internal Brush ProgressColor => color;
    internal void Set(double value,Brush brush)
    {
        if (value == progress && brush == color) return;
        progress = value; color = brush; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); outline.Draw(dc,RenderSize,progress,color);
    }
}
