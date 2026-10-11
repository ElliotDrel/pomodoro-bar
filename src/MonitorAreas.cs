using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

namespace PomodoroBar;

internal static class MonitorAreas
{
    internal static IReadOnlyList<Rect> WorkingAreas()
    {
        var areas = new List<Rect>();
        Callback callback = (IntPtr monitor,IntPtr dc,ref NativeRect rectangle,IntPtr context) => {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor,ref info)) areas.Add(new Rect(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top));
            return true;
        };
        if (!EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,callback,IntPtr.Zero) || areas.Count == 0) throw new InvalidOperationException("Could not enumerate monitor work areas.");
        return areas;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor,Work; public uint Flags; }
    private delegate bool Callback(IntPtr monitor,IntPtr dc,ref NativeRect rectangle,IntPtr context);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,Callback callback,IntPtr context);
    [DllImport("user32.dll",EntryPoint="GetMonitorInfoW")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
}
