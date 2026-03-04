using System.Diagnostics;
using System.Runtime.InteropServices;
using DofusOrganizer.Services.Interfaces;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DofusOrganizer.Services;

public sealed class WindowFocusService : IWindowFocusService
{
    public void FocusWindow(nint hwnd)
    {
        var target = new HWND(hwnd);

        if (!PInvoke.IsWindow(target))
        {
            Debug.WriteLine($"[Focus] FocusWindow 0x{hwnd:X} — IsWindow=false, abandon");
            return;
        }

        if (PInvoke.IsIconic(target))
        {
            Debug.WriteLine($"[Focus] FocusWindow 0x{hwnd:X} — minimisée, SW_RESTORE");
            PInvoke.ShowWindow(target, SHOW_WINDOW_CMD.SW_RESTORE);
        }

        var foreground = PInvoke.GetForegroundWindow();
        uint foregroundThread;
        uint ourThread = PInvoke.GetCurrentThreadId();

        unsafe
        {
            foregroundThread = PInvoke.GetWindowThreadProcessId(foreground, null);
        }

        Debug.WriteLine($"[Focus] FocusWindow 0x{hwnd:X} — foregroundThread={foregroundThread}  ourThread={ourThread}");

        bool attached = false;
        if (foregroundThread != ourThread && foregroundThread != 0)
        {
            bool ok = PInvoke.AttachThreadInput(foregroundThread, ourThread, true);
            Debug.WriteLine($"[Focus] AttachThreadInput={ok}");
            attached = ok;
        }

        try
        {
            PInvoke.BringWindowToTop(target);
            bool sfwOk = PInvoke.SetForegroundWindow(target);
            PInvoke.SetFocus(target);
            PInvoke.SetActiveWindow(target);
            Debug.WriteLine($"[Focus] BringWindowToTop + SetForegroundWindow={sfwOk}  err={Marshal.GetLastWin32Error()}");
        }
        finally
        {
            if (attached)
                PInvoke.AttachThreadInput(foregroundThread, ourThread, false);
        }
    }

    public nint GetForegroundWindow()
    {
        return PInvoke.GetForegroundWindow();
    }

    public bool IsWindowValid(nint hwnd)
    {
        return PInvoke.IsWindow(new HWND(hwnd));
    }

    public bool IsWindowMinimized(nint hwnd)
    {
        return PInvoke.IsIconic(new HWND(hwnd));
    }
}
