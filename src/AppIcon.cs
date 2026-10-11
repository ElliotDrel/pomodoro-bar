using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PomodoroBar;

internal static class AppIcon
{
    // The executable icon and WPF HWND icons are separate resources. Explicitly
    // assign the embedded icon so the taskbar never falls back to WPF's default.
    public static ImageSource Source { get; } = Load();

    internal static IntPtr CreateNativeIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/PomodoroBar;component/PomodoroBar.ico",UriKind.Absolute)).Stream;
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt16()!=0 || reader.ReadUInt16()!=1) throw new InvalidDataException("Invalid embedded icon.");
        int count = reader.ReadUInt16(), best = int.MaxValue; uint length=0,offset=0;
        for (int i=0;i<count;i++) {
            int width=reader.ReadByte(); if (width==0) width=256;
            reader.ReadByte();reader.ReadByte();reader.ReadByte();reader.ReadUInt16();reader.ReadUInt16();
            uint bytes=reader.ReadUInt32(),position=reader.ReadUInt32();
            int score=Math.Abs(width-32); if (score<best) { best=score;length=bytes;offset=position; }
        }
        stream.Position=offset;
        byte[] image=reader.ReadBytes(checked((int)length));
        if (image.Length!=length) throw new InvalidDataException("Incomplete embedded icon.");
        IntPtr icon=CreateIconFromResourceEx(image,(uint)image.Length,true,0x30000,32,32,0);
        if (icon==IntPtr.Zero) throw new InvalidOperationException("Could not load the embedded notification icon.");
        return icon;
    }
    [DllImport("user32.dll")] private static extern IntPtr CreateIconFromResourceEx(byte[] data,uint length,[MarshalAs(UnmanagedType.Bool)] bool icon,uint version,int width,int height,uint flags);

    private static ImageSource Load()
    {
        var icon = BitmapFrame.Create(new Uri("pack://application:,,,/PomodoroBar;component/PomodoroBar.ico", UriKind.Absolute));
        icon.Freeze();
        return icon;
    }
}
