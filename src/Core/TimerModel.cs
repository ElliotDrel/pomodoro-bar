using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PomodoroBar.Core;

public sealed class TimerModel
{
    private readonly Func<double> clock;
    private double deadline;
    private readonly HashSet<double> fired = [];
    public double Focus { get; private set; }
    public double Rest { get; private set; }
    public double[] Thresholds { get; }
    public bool IsFocus { get; private set; } = true;
    public bool Running { get; private set; }
    public bool Waiting { get; private set; }
    public double Remaining { get; private set; }
    public double Duration => IsFocus ? Focus : Rest;
    public double Progress => Math.Clamp(((IsFocus ? 0 : Focus) + Duration - Remaining) / (Focus + Rest), 0, 1);
    public int FiredCount => fired.Count;

    // Schedule only the next unfired milestone. Deadlines remain authoritative after sleep.
    public double SecondsUntilNextEvent()
    {
        if (!Running) return double.PositiveInfinity;
        double remaining = Math.Max(0, deadline - clock());
        double delay = remaining;
        if (IsFocus)
            foreach (double p in Thresholds)
                if (!fired.Contains(p)) delay = Math.Min(delay, Math.Max(0, remaining - Focus * p / 100));
        return delay;
    }

    public TimerModel(double focus, double rest, double[] thresholds, Func<double> clock)
    {
        ValidateDuration(focus); ValidateDuration(rest);
        if (thresholds.Length is < 1 or > 16 || thresholds.Any(p => !double.IsFinite(p) || p <= 0 || p >= 100))
            throw new ArgumentException("Invalid reminder percentages.");
        Focus = focus; Rest = rest; Remaining = focus;
        Thresholds = thresholds.Distinct().OrderDescending().ToArray();
        this.clock = clock;
    }

    // One result per tick: completing takes precedence over every missed reminder.
    public string? Tick()
    {
        if (!Running) return null;
        Remaining = Math.Max(0, deadline - clock());
        bool reminder = false;
        if (IsFocus)
            foreach (double p in Thresholds)
                if (Remaining <= Focus * p / 100 && fired.Add(p)) reminder = true;
        if (Remaining == 0) { Running = false; Waiting = true; return "complete"; }
        return reminder ? ReminderSound(Thresholds.Length, fired.Count) : null;
    }

    public string? Toggle()
    {
        if (Waiting) { Advance(true); return null; }
        if (Running) { var sound = Tick(); Running = false; return sound; }
        else { deadline = clock() + Remaining; Running = true; }
        return null;
    }

    public void Advance(bool? run = null)
    {
        bool nextRun = run ?? (Running || Waiting);
        IsFocus = !IsFocus; Remaining = Duration; Waiting = false; fired.Clear();
        Running = nextRun; deadline = clock() + Remaining;
    }

    public void SetTime(double seconds)
    {
        ValidateDuration(seconds);
        if (IsFocus) Focus = seconds; else Rest = seconds;
        Remaining = seconds; Waiting = false; fired.Clear(); deadline = clock() + seconds;
    }

    public static void ValidateDuration(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 1 || seconds > 86400)
            throw new ArgumentException("Enter a time from one second to 24 hours.");
    }

    public static string ReminderSound(int total, int fired) => total - fired <= 0 ? "last" : total - fired == 1 ? "second-last" : "earlier";

    public static double ParseTime(string text)
    {
        string[] parts = text.Trim().Split(':');
        if (parts.Length is < 1 or > 3 || parts.Any(p => p.Length == 0 || p.Any(c => c < '0' || c > '9')))
            throw new ArgumentException("Use minutes, MM:SS, or HH:MM:SS.");
        if (parts.Any(p => !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            throw new ArgumentException("Time is too large.");
        int[] numbers = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        if (numbers.Skip(1).Any(n => n >= 60)) throw new ArgumentException("Seconds and the minutes in HH:MM:SS must be below 60.");
        double seconds = parts.Length == 1 ? numbers[0] * 60d : numbers.Aggregate(0d, (n, part) => n * 60 + part);
        ValidateDuration(seconds); return seconds;
    }

    public static string FormatTime(double seconds)
    {
        int whole = (int)Math.Ceiling(Math.Max(0, seconds));
        return whole >= 3600 ? $"{whole / 3600}:{whole / 60 % 60:00}:{whole % 60:00}" : $"{whole / 60:00}:{whole % 60:00}";
    }

    public static DateTimeOffset EstimatedEnd(DateTimeOffset now, double seconds)
    {
        var end = now.AddSeconds(seconds);
        long remainder = end.Ticks % TimeSpan.TicksPerMinute;
        long rounded = end.Ticks - remainder + (remainder >= TimeSpan.TicksPerMinute / 2 ? TimeSpan.TicksPerMinute : 0);
        return new DateTimeOffset(rounded, end.Offset);
    }
}
