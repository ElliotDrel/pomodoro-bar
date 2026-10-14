using System;
using System.Globalization;

namespace PomodoroBar.Core;

public static class ClockTime
{
    public static DateTimeOffset Parse(string text,DateTimeOffset reference,DateTimeOffset? today = null)
    {
        text = text.Trim();
        bool tomorrow = text.StartsWith("tomorrow ",StringComparison.OrdinalIgnoreCase);
        if (tomorrow) text = text[9..].Trim();
        if (text.EndsWith(" tomorrow",StringComparison.OrdinalIgnoreCase)) { tomorrow = true; text = text[..^9].Trim(); }
        string[] formats = ["h:mm tt","hh:mm tt","H:mm","HH:mm","h:mm:ss tt","HH:mm:ss"];
        DateTimeOffset value;
        if (DateTime.TryParseExact(text,formats,CultureInfo.CurrentCulture,DateTimeStyles.None,out var time)) {
            var day = tomorrow ? (today ?? reference).Date.AddDays(1) : reference.Date;
            value = new DateTimeOffset(day.Add(time.TimeOfDay),reference.Offset);
        } else if (!tomorrow && DateTimeOffset.TryParse(text,CultureInfo.CurrentCulture,DateTimeStyles.None,out var dated)
                   && (text.Contains('/') || text.Contains('-'))) value = dated;
        else throw new ArgumentException("Enter a clock time such as 5:25 PM, or tomorrow 5:25 PM.");
        return TimerModel.EstimatedEnd(value,0);
    }

    public static string Format(DateTimeOffset value,DateTimeOffset now)
    {
        value = TimerModel.EstimatedEnd(value,0);
        return value.Date == now.Date ? value.ToString("t")
            : value.Date == now.Date.AddDays(1) ? value.ToString("t") + " tomorrow"
            : value.ToString("yyyy-MM-dd h:mm tt");
    }
}
