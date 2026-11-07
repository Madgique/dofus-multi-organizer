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

        // Déjà au premier plan : ne rien faire. Sans ce garde-fou, un cycle sur une seule
        // fenêtre (ou un appui répété) ré-active inutilement la fenêtre courante et peut
        // faire clignoter / perdre le focus.
        if (PInvoke.GetForegroundWindow().Value == target.Value)
            return;

        if (PInvoke.IsIconic(target))
            PInvoke.ShowWindow(target, SHOW_WINDOW_CMD.SW_RESTORE);

        uint ourThread = PInvoke.GetCurrentThreadId();
        uint foregroundThread = GetWindowThreadId(PInvoke.GetForegroundWindow());
        uint targetThread = GetWindowThreadId(target);

        // S'attacher à la fois au thread au premier plan (débloque SetForegroundWindow)
        // et au thread cible (nécessaire pour SetFocus / SetActiveWindow).
        var attachedThreads = new List<uint>(2);
        AttachInputQueue(foregroundThread, ourThread, attachedThreads);
        AttachInputQueue(targetThread, ourThread, attachedThreads);

        try
        {
            PInvoke.BringWindowToTop(target);
            PInvoke.SetForegroundWindow(target);
            PInvoke.SetFocus(target);
            PInvoke.SetActiveWindow(target);
        }
        finally
        {
            // Détacher dans l'ordre inverse pour rendre les files d'entrée indépendantes
            for (int i = attachedThreads.Count - 1; i >= 0; i--)
                PInvoke.AttachThreadInput(attachedThreads[i], ourThread, false);
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

    private static uint GetWindowThreadId(HWND hwnd)
    {
        if (hwnd.Value == 0)
            return 0;

        unsafe
        {
            return PInvoke.GetWindowThreadProcessId(hwnd, null);
        }
    }

    private static void AttachInputQueue(uint threadId, uint ourThread, List<uint> attachedThreads)
    {
        if (threadId == 0 || threadId == ourThread || attachedThreads.Contains(threadId))
            return;

        if (PInvoke.AttachThreadInput(threadId, ourThread, true))
            attachedThreads.Add(threadId);
    }
}
