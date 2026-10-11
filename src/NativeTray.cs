using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PomodoroBar;

// Use the existing WPF dispatcher and Windows shell; no second UI framework.
internal sealed class NativeTray : IDisposable
{
    private const uint Callback = 0x8001;
    private readonly HwndSource window;
    private readonly Action openEditor;
    private readonly List<(string? Label, Action? Action, Func<bool>? Enabled)> items = [];
    private readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private NotifyData data;
    private bool disposed;
    internal bool Registered { get; private set; }

    public NativeTray(Action openEditor)
    {
        this.openEditor = openEditor;
        window = new HwndSource(new HwndSourceParameters("Pomodoro notification icon") { WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x80, Width = 1, Height = 1 });
        window.AddHook(Message);
        IntPtr icon;
        try { icon = AppIcon.CreateNativeIcon(); } catch { window.RemoveHook(Message); window.Dispose(); throw; }
        data = new NotifyData { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = window.Handle, Id = 1, Flags = 0x87, Message = Callback,
            Icon = icon, Tip = BuildChannel.DisplayName, Info = "", Title = "" };
        if (!Register()) Log.Write("tray_registration_deferred");
    }
    private bool Register()
    {
        if (Registered) ShellNotifyIcon(2,ref data);
        Registered = ShellNotifyIcon(0,ref data);
        if (Registered) { var version = data; version.Timeout = 4; ShellNotifyIcon(4,ref version); }
        return Registered;
    }
    internal IntPtr Handle => window.Handle;
    public void Item(string label, Action action, Func<bool>? enabled = null) => items.Add((label,action,enabled));
    public void Separator() => items.Add((null,null,null));
    public void ShowWarning(string title,string text)
    {
        if (disposed) return;
        var notification = data; notification.Flags = 0x10; notification.Info = text.Length > 255 ? text[..255] : text;
        notification.Title = title.Length > 63 ? title[..63] : title; notification.InfoFlags = 2; notification.Timeout = 5000;
        ShellNotifyIcon(1,ref notification);
    }
    private IntPtr Message(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if (disposed) return IntPtr.Zero;
        if ((uint)message == taskbarCreated) { Register(); return IntPtr.Zero; }
        if ((uint)message != Callback) return IntPtr.Zero;
        handled = true;
        long notification = lParam.ToInt64() & 0xffff;
        if (notification is 0x203 or 0x401) openEditor();
        else if (notification is 0x205 or 0x7b) OpenMenu();
        return IntPtr.Zero;
    }
    private void OpenMenu()
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        uint command;
        try {
            for (int i = 0; i < items.Count; i++) {
                var item = items[i];
                AppendMenu(menu,item.Label is null ? 0x800u : item.Enabled?.Invoke() == false ? 3u : 0u,(UIntPtr)(uint)(i+1),item.Label);
            }
            GetCursorPos(out var point); SetForegroundWindow(window.Handle);
            command = TrackPopupMenu(menu,0x180,point.X,point.Y,0,window.Handle,IntPtr.Zero);
            PostMessage(window.Handle,0,IntPtr.Zero,IntPtr.Zero);
        } finally { DestroyMenu(menu); }
        if (command == 0) ShellNotifyIcon(3,ref data);
        if (command > 0 && command <= items.Count) items[(int)command-1].Action?.Invoke();
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        if (data.Window != IntPtr.Zero) ShellNotifyIcon(2,ref data);
        Registered = false;
        if (data.Icon != IntPtr.Zero) DestroyIcon(data.Icon);
        window.RemoveHook(Message); window.Dispose();
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct NotifyData {
        public uint Size; public IntPtr Window; public uint Id,Flags,Message; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Tip;
        public uint State,StateMask;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string Title;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X,Y; }
    [DllImport("shell32.dll",EntryPoint="Shell_NotifyIconW",CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool ShellNotifyIcon(uint action,ref NotifyData data);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll",EntryPoint="RegisterWindowMessageW",CharSet=CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll",EntryPoint="AppendMenuW",CharSet=CharSet.Unicode)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool AppendMenu(IntPtr menu,uint flags,UIntPtr id,string? text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(IntPtr menu,uint flags,int x,int y,int reserved,IntPtr hwnd,IntPtr rectangle);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
}
