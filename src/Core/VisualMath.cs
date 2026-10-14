using System;

namespace PomodoroBar.Core;

public readonly record struct Rgb(byte R, byte G, byte B);
public readonly record struct BarLayout(double Scale, double Left, double Top, double TimerWidth, bool Vertical = false)
{
    public double LogicalWidth => Vertical ? Math.Max(100, TimerWidth + 16) : TimerWidth + 116;
    public double LogicalHeight => Vertical ? 160 : 52;
    public static BarLayout Fit(double width, double height, double textWidth, bool vertical = false)
    {
        double logicalWidth = vertical ? Math.Max(100,textWidth + 16) : textWidth + 116;
        double logicalHeight = vertical ? 160 : 52;
        double scale = Math.Min(width / logicalWidth, height / logicalHeight);
        return new(scale, (width-logicalWidth*scale)/2, (height-logicalHeight*scale)/2,textWidth,vertical);
    }
    public int Hit(double x, double y)
    {
        x = (x-Left)/Scale; y = (y-Top)/Scale;
        if (x < 0 || x > LogicalWidth || y < 0 || y > LogicalHeight) return -1;
        if (Vertical) return y < 52 ? 1 : Math.Min(4,2+(int)((y-52)/36));
        if (x < TimerWidth+12) return 1;
        return Math.Min(4,2+(int)((x-TimerWidth-12)/32));
    }
}

public static class VisualMath
{
    private static readonly double[] Stops = [1, .8, .4, .2, .05, 0];
    private static readonly Rgb[] Colors = [new(244,246,252), new(102,219,183), new(255,224,130), new(255,174,102), new(255,91,110), new(255,91,110)];
    public static Rgb CountdownColor(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        for (int i = 0; i < Stops.Length - 1; i++)
            if (fraction >= Stops[i + 1]) {
                double t = (Stops[i] - fraction) / (Stops[i] - Stops[i + 1]);
                byte L(byte a, byte b) => (byte)Math.Round(a + (b - a) * t);
                return new(L(Colors[i].R, Colors[i+1].R), L(Colors[i].G, Colors[i+1].G), L(Colors[i].B, Colors[i+1].B));
            }
        return Colors[^1];
    }
}
