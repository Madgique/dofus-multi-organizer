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
            return;

        if (PInvoke.IsIconic(target))
            PInvoke.ShowWindow(target, SHOW_WINDOW_CMD.SW_RESTORE);

        var foreground = PInvoke.GetForegroundWindow();
        uint foregroundThread;
        uint ourThread = PInvoke.GetCurrentThreadId();

        unsafe
        {
            foregroundThread = PInvoke.GetWindowThreadProcessId(foreground, null);
        }

        bool attached = false;
        if (foregroundThread != ourThread && foregroundThread != 0)
        {
            attached = PInvoke.AttachThreadInput(foregroundThread, ourThread, true);
        }

        try
        {
            PInvoke.BringWindowToTop(target);
            PInvoke.SetForegroundWindow(target);
            PInvoke.SetFocus(target);
            PInvoke.SetActiveWindow(target);
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
