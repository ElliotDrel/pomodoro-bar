using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PomodoroBar;

internal static class AppIcon
{
    // The executable icon and WPF HWND icons are separate resources. Explicitly
    // assign the embedded icon so the taskbar never falls back to WPF's default.
    public static ImageSource Source { get; } = Load();

    private static ImageSource Load()
    {
        var icon = BitmapFrame.Create(new Uri("pack://application:,,,/PomodoroBar;component/PomodoroBar.ico", UriKind.Absolute));
        icon.Freeze();
        return icon;
    }
}
