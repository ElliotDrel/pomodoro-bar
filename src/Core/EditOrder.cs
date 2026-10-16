using System;
using System.Collections.Generic;

namespace PomodoroBar.Core;

public enum TimeField { Start, Focus, Break, FocusEnd, BreakEnd }

public readonly record struct EditTimeline(double Start,double Focus,double Rest)
{
    public double Read(TimeField field) => field switch {
        TimeField.Start => Start, TimeField.Focus => Focus, TimeField.Break => Rest,
        TimeField.FocusEnd => Start+Focus, _ => Start+Focus+Rest };
}

public sealed class EditOrder
{
    public const double IdleSeconds = 8;
    private List<(TimeField Field,double Value)> order = [], pending = [];
    private double expires;
    // Only commit the order after the controller has validated and applied the timeline.
    public void Accept(double now) { order=pending; expires=now+IdleSeconds; }
    public void Clear() { order=[]; pending=[]; expires=0; }
    public double SecondsLeft(double now) => order.Count == 0 ? 0 : Math.Max(0,expires-now);
    public string Description => string.Join(" → ",order.ConvertAll(edit => edit.Field switch {
        TimeField.Start => "start", TimeField.Focus => "focus duration", TimeField.Break => "break duration",
        TimeField.FocusEnd => "focus end", _ => "break end" }));

    public EditTimeline Apply(TimeField field,double value,EditTimeline current,double now)
    {
        if (now >= expires) Clear();
        var next = new List<(TimeField Field,double Value)>(order);
        int index=next.FindIndex(edit => edit.Field == field);
        if (index < 0) next.Add((field,value)); else next[index]=(field,value);
        var known=new Dictionary<TimeField,double>();

        // Each relationship is simply left + duration = right.
        bool Complete(TimeField left,TimeField duration,TimeField right)
        {
            bool hasLeft=known.TryGetValue(left,out double a), hasDuration=known.TryGetValue(duration,out double b), hasRight=known.TryGetValue(right,out double c);
            if (hasLeft && hasDuration && !hasRight) { known[right]=a+b; return true; }
            if (hasLeft && hasRight && !hasDuration) { known[duration]=c-a; return true; }
            if (hasDuration && hasRight && !hasLeft) { known[left]=c-b; return true; }
            return false;
        }
        bool Add(TimeField f,double target)
        {
            if (known.TryGetValue(f,out double existing)) return Math.Abs(existing-target) < .01;
            known[f]=target;
            while (Complete(TimeField.Start,TimeField.Focus,TimeField.FocusEnd)
                | Complete(TimeField.FocusEnd,TimeField.Break,TimeField.BreakEnd)) { }
            return true;
        }
        foreach (var (f,stored) in next) {
            // Durations follow an active countdown; clock endpoints remain absolute.
            double target=f == field ? value : f is TimeField.Focus or TimeField.Break ? current.Read(f) : stored;
            if (!Add(f,target)) throw new ArgumentException("That conflicts with earlier edits. Wait 8 seconds to start a new edit order.");
        }
        // Preserve unedited values where possible: prefer keeping the start in place.
        Add(TimeField.Start,current.Start);
        if (field == TimeField.BreakEnd) { Add(TimeField.Focus,current.Focus); Add(TimeField.Break,current.Rest); }
        else { Add(TimeField.Break,current.Rest); Add(TimeField.Focus,current.Focus); }
        var result=new EditTimeline(known[TimeField.Start],known[TimeField.Focus],known[TimeField.Break]);
        TimerModel.ValidateDuration(result.Focus); TimerModel.ValidateDuration(result.Rest);
        pending=next;
        return result;
    }
}
