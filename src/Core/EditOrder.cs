using System;
using System.Collections.Generic;

namespace PomodoroBar.Core;

public enum TimeField { Start, Focus, Break, FocusEnd, BreakEnd }

// Keep earlier edits ahead of later edits, then fill free variables from the current timeline.
public sealed class EditOrder
{
    public const double IdleSeconds = 8;
    private readonly List<TimeField> order = [];
    private double expires;
    private List<TimeField> pending = [];
    private readonly Dictionary<TimeField,double> values = new();
    private Dictionary<TimeField,double> pendingValues = new();
    public void Accept(double now) { order.Clear(); order.AddRange(pending); values.Clear(); foreach (var pair in pendingValues) values.Add(pair.Key,pair.Value); expires=now+IdleSeconds; }
    public void Clear() { order.Clear(); values.Clear(); expires = 0; }
    public double SecondsLeft(double now) => order.Count == 0 ? 0 : Math.Max(0,expires-now);
    public string Description => string.Join(" → ",order.ConvertAll(f => f switch {
        TimeField.Start => "start", TimeField.Focus => "focus duration", TimeField.Break => "break duration",
        TimeField.FocusEnd => "focus end", _ => "break end" }));

    public double[] Apply(TimeField field,double value,double[] current,double now)
    {
        if (now >= expires) Clear();
        var next = new List<TimeField>(order);
        if (!next.Contains(field)) next.Add(field);
        var rows = new List<double[]>();
        double[] Coefficients(TimeField f) => f switch {
            TimeField.Start => [1,0,0], TimeField.Focus => [0,1,0], TimeField.Break => [0,0,1],
            TimeField.FocusEnd => [1,1,0], _ => [1,1,1] };
        double Read(TimeField f) { var c = Coefficients(f); return c[0]*current[0]+c[1]*current[1]+c[2]*current[2]; }
        bool Add(TimeField f,double target)
        {
            var c = Coefficients(f); double[] row = [c[0],c[1],c[2],target];
            foreach (var existing in rows) {
                int pivot = Array.FindIndex(existing,0,3,x => Math.Abs(x) > .000001);
                double factor = row[pivot];
                for (int i=0;i<4;i++) row[i] -= factor*existing[i];
            }
            int p = Array.FindIndex(row,0,3,x => Math.Abs(x) > .000001);
            if (p < 0) return Math.Abs(row[3]) < .01;
            double divisor = row[p]; for (int i=0;i<4;i++) row[i] /= divisor;
            foreach (var existing in rows) {
                double factor = existing[p];
                for (int i=0;i<4;i++) existing[i] -= factor*row[i];
            }
            rows.Add(row); return true;
        }
        foreach (var f in next)
            // Durations follow an active countdown; clock endpoints remain absolute.
            if (!Add(f,f == field ? value : f is TimeField.Focus or TimeField.Break ? Read(f) : values[f]))
                throw new ArgumentException("That conflicts with earlier edits. Wait 8 seconds to start a new edit order.");
        var defaults = field == TimeField.BreakEnd ? new[] { TimeField.Start,TimeField.Focus,TimeField.Break }
            : new[] { TimeField.Start,TimeField.Break,TimeField.Focus };
        foreach (var f in defaults) Add(f,Read(f));
        double[] result = new double[3];
        for (int i=rows.Count-1;i>=0;i--) {
            var row=rows[i]; int p=Array.FindIndex(row,0,3,x => Math.Abs(x) > .000001);
            result[p]=row[3]; for (int j=p+1;j<3;j++) result[p]-=row[j]*result[j];
        }
        TimerModel.ValidateDuration(result[1]); TimerModel.ValidateDuration(result[2]);
        pending = next;
        pendingValues = new(values) { [field] = value };
        return result;
    }
}
