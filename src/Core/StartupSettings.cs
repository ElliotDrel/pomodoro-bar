using System;
using System.IO;

namespace PomodoroBar.Core;

public sealed record StartupSettings(Settings Value, bool CanSave, string? Warning)
{
    public static StartupSettings Load(string path,string? legacy = null,double dpiScale = 1)
    {
        if (File.Exists(path)) {
            try { return new(Settings.Load(path),true,null); }
            catch (Exception ex) when (Recoverable(ex)) {
                try {
                    File.Copy(path,path+".backup-"+Guid.NewGuid().ToString("N"));
                    return new(new Settings(),true,"Saved settings were invalid. Defaults loaded; the original file is preserved as a backup.");
                } catch (Exception backup) when (Recoverable(backup)) {
                    return new(new Settings(),false,"Settings could not be read or backed up. Defaults loaded without changing your settings file.");
                }
            }
        }
        if (legacy is not null) {
            try { var value=Settings.LoadLegacy(legacy,dpiScale); value.Save(path); return new(value,true,null); }
            catch (Exception ex) when (Recoverable(ex)) { return new(new Settings(),false,"Could not import old settings. Defaults loaded; your original file is unchanged. "+ex.Message); }
        }
        return new(new Settings(),true,null);
    }
    private static bool Recoverable(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException;
}
