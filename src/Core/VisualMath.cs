using System;

namespace PomodoroBar.Core;

public readonly record struct Rgb(byte R, byte G, byte B);
public readonly record struct BarLayout(double Scale, double Left, double Top, double TimerWidth)
{
    public double LogicalWidth => 32 + TimerWidth + 3 * 32 + 20;
    public static BarLayout Fit(double width, double height, double textWidth)
    {
        double logicalWidth = 32 + textWidth + 3 * 32 + 20;
        double scale = Math.Min(width / logicalWidth, height / 52);
        return new(scale, (width - logicalWidth * scale) / 2, (height - 52 * scale) / 2, textWidth);
    }
    public int Hit(double x, double y)
    {
        x = (x - Left) / Scale; y = (y - Top) / Scale;
        if (y < 0 || y > 52 || x < 0 || x > LogicalWidth) return -1;
        if (x < 40) return 0;
        if (x < 40 + TimerWidth) return 1;
        return Math.Min(4, 2 + (int)((x - 40 - TimerWidth) / 32));
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
