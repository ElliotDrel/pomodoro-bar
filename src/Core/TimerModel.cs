using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PomodoroBar.Core;

public sealed class TimerModel
{
    private readonly Func<double> clock;
    private double deadline;
    private double? startDeadline;
    private double scheduledDelay;
    public bool Scheduled => startDeadline.HasValue;
    public double StartRemaining => startDeadline is double start ? Math.Max(0, start - clock()) : 0;
    public double StartProgress => Scheduled ? Math.Clamp(1 - StartRemaining / scheduledDelay, 0, 1) : 0;
    public double SecondsToDeadline => deadline - clock();
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
        if (Scheduled) return StartRemaining;
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
        if (startDeadline is double start && clock() >= start) {
            startDeadline = null; Running = true; deadline = start + Remaining;
        }
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
        if (Scheduled) { startDeadline = null; return null; }
        if (Waiting) { Advance(true); return null; }
        if (Running) { var sound = Tick(); Running = false; return sound; }
        else { deadline = clock() + Remaining; Running = true; }
        return null;
    }

    public void Advance(bool? run = null)
    {
        startDeadline = null;
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

    public void SetSectionTime(bool focus, double seconds)
    {
        ValidateDuration(seconds);
        if (focus == IsFocus) SetTime(seconds);
        else if (focus) Focus = seconds;
        else Rest = seconds;
    }

    public void Schedule(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) throw new ArgumentException("Choose a future start time.");
        IsFocus = true; Remaining = Focus; Running = false; Waiting = false; fired.Clear();
        scheduledDelay = seconds; startDeadline = clock() + seconds;
    }

    public void RebaseScheduledStart(double seconds)
    {
        if (!Scheduled || !double.IsFinite(seconds)) return;
        startDeadline = clock() + seconds;
    }

    public void SetEndpoint(bool focus,double seconds)
    {
        ValidateDuration(seconds);
        if (focus != IsFocus) { SetSectionTime(focus,RoundDuration(seconds)); return; }
        double elapsed = Math.Max(0,Duration - Remaining);
        if (focus) Focus = RoundDuration(Math.Min(86400,elapsed+seconds)); else Rest = RoundDuration(Math.Min(86400,elapsed+seconds));
        Remaining = seconds; Waiting = false; fired.Clear(); deadline = clock() + seconds;
    }

    public static double RoundDuration(double seconds)
    {
        ValidateDuration(seconds);
        return Math.Clamp(Math.Floor(seconds / 60 + .5) * 60, 60, 86400);
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
