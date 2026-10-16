using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PomodoroBar;

// Real WPF windows, on a private desktop that is never switched onto the screen.
// A fresh STA is required: SetThreadDesktop rejects threads that already own HWNDs/hooks.
internal static class VerificationDesktop
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,uint flags,uint access,IntPtr security);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool SetThreadDesktop(IntPtr desktop);
    [DllImport("user32.dll",SetLastError=true)] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint thread);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder value,uint length,out uint needed);
    [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [ThreadStatic] private static string? privateName;

    private static string Name(IntPtr desktop)
    {
        var name=new StringBuilder(256);
        if (!GetUserObjectInformation(desktop,2,name,512,out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return name.ToString();
    }
    internal static bool IsIsolated
    {
        get {
            if (privateName is null || Name(GetThreadDesktop(GetCurrentThreadId())) != privateName) return false;
            IntPtr input=OpenInputDesktop(0,false,1);
            if (input == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try { return Name(input) != privateName; } finally { CloseDesktop(input); }
        }
    }
    internal static bool ContainsWindow(IntPtr window)
    {
        uint thread=GetWindowThreadProcessId(window,out uint process);
        return privateName is not null && process == Environment.ProcessId
            && Name(GetThreadDesktop(thread)) == privateName;
    }

    internal static int Run(Func<int> verify)
    {
        string name="PomodoroBar.Verification."+Guid.NewGuid().ToString("N");
        // Read/write objects and create windows/menus; deliberately no SWITCHDESKTOP right.
        IntPtr desktop=CreateDesktop(name,IntPtr.Zero,IntPtr.Zero,0,0x87,IntPtr.Zero);
        if (desktop == IntPtr.Zero) {
            Console.Error.WriteLine("Background verification could not create its private desktop: "+new Win32Exception(Marshal.GetLastWin32Error()).Message);
            return 1; // Never fall back to showing test windows on the user's desktop.
        }
        int result=1; Exception? failure=null;
        var worker=new Thread(() => {
            try {
                if (!SetThreadDesktop(desktop)) throw new Win32Exception(Marshal.GetLastWin32Error());
                privateName=name;
                if (!IsIsolated) throw new InvalidOperationException("Verification desktop must not be the input desktop.");
                result=verify();
                if (!IsIsolated) throw new InvalidOperationException("Verification changed the input desktop.");
            } catch (Exception ex) { failure=ex; }
        });
        worker.SetApartmentState(ApartmentState.STA);
        try {
            worker.Start();
            worker.Join();
            if (failure is not null) { Console.Error.WriteLine(failure); return 1; }
            Console.WriteLine("Background verification: private non-input desktop.");
            return result;
        } finally { CloseDesktop(desktop); }
    }
}
