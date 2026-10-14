using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PomodoroBar.Core;

public sealed record Settings
{
    public double Focus { get; init; } = 1500;
    public double Rest { get; init; } = 300;
    public double[] Thresholds { get; init; } = [40, 20];
    public double Width { get; init; } = 280;
    public double Height { get; init; } = 52;
    public double X { get; init; } = 80;
    public double Y { get; init; } = 80;
    public bool Vertical { get; init; }

    public void Validate()
    {
        if (Thresholds is null) throw new ArgumentException("Missing reminder percentages.");
        _ = new TimerModel(Focus, Rest, Thresholds, () => 0);
        if (!double.IsFinite(Width) || !double.IsFinite(Height) || Width < (Vertical ? 52 : 130) || Width > (Vertical ? 300 : 1000) || Height < (Vertical ? 130 : 20) || Height > (Vertical ? 1000 : 120) || !double.IsFinite(X) || !double.IsFinite(Y))
            throw new ArgumentException("Invalid window geometry.");
    }

    public static Settings Load(string path)
    {
        if (!File.Exists(path)) return new();
        var value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? throw new ArgumentException("Empty settings.");
        value.Validate(); return value;
    }

    public static Settings LoadLegacy(string path, double dpiScale = 1)
    {
        if (!double.IsFinite(dpiScale) || dpiScale <= 0) throw new ArgumentException("Invalid DPI scale.");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var r = json.RootElement;
        var value = new Settings {
            Focus = r.GetProperty("focus").GetDouble() * 60,
            Rest = r.GetProperty("rest").GetDouble() * 60,
            Thresholds = r.GetProperty("thresholds").EnumerateArray().Select(p => p.GetDouble()).ToArray(),
            Width = Math.Clamp(r.GetProperty("width").GetDouble()/dpiScale,130,1000), Height = Math.Clamp(r.GetProperty("height").GetDouble()/dpiScale,20,120),
            X = r.GetProperty("x").GetDouble()/dpiScale, Y = r.GetProperty("y").GetDouble()/dpiScale
        };
        value.Validate(); return value;
    }

    public void Save(string path)
    {
        Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }
}
