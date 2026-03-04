using System.Diagnostics;
using System.Runtime.InteropServices;
using DofusOrganizer.Services.Interfaces;
using DofusOrganizer.Views;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace DofusOrganizer.Services;

/// <summary>
/// Gère les hotkeys globaux via RegisterHotKey / WM_HOTKEY.
/// Utilise le subclassing de la WndProc de la MainWindow pour intercepter WM_HOTKEY.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    // P/Invoke manuels — CsWin32 ne génère pas SetWindowLongPtr correctement sur x64
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtrManual(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern nint CallWindowProcManual(nint lpPrevWndFunc, nint hWnd, uint msg, nuint wParam, nint lParam);

    private delegate nint WndProcDelegate(nint hwnd, uint msg, nuint wParam, nint lParam);

    private const int GWLP_WNDPROC = -4;
    private const uint WM_HOTKEY_MSG = 0x0312;

    public const int ID_NEXT_WINDOW = 1;
    public const int ID_PREV_WINDOW = 2;
    public const int ID_WINDOW_BASE = 100;

    private nint _hwnd;
    private nint _origWndProcPtr;

    // CRITIQUE : le delegate doit être un champ pour éviter que le GC le libère pendant l'exécution
    private WndProcDelegate? _newWndProc;

    private readonly HashSet<int> _registeredIds = [];

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    public void Initialize(MainWindow window)
    {
        _hwnd = window.WindowHandle;

        _newWndProc = WndProc;
        var newProcPtr = Marshal.GetFunctionPointerForDelegate(_newWndProc);
        _origWndProcPtr = SetWindowLongPtrManual(_hwnd, GWLP_WNDPROC, newProcPtr);
    }

    private nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (msg == WM_HOTKEY_MSG)
        {
            Debug.WriteLine($"[Hotkey] WM_HOTKEY reçu — id={wParam}");
            HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs { HotkeyId = (int)wParam });
            return 0;
        }
        return CallWindowProcManual(_origWndProcPtr, hwnd, msg, wParam, lParam);
    }

    public bool RegisterGlobalHotkey(int id, uint modifiers, uint virtualKey)
    {
        var hwnd = new HWND(_hwnd);
        if (_registeredIds.Contains(id))
            PInvoke.UnregisterHotKey(hwnd, id);

        bool ok = PInvoke.RegisterHotKey(hwnd, id, (HOT_KEY_MODIFIERS)modifiers, virtualKey);
        if (ok)
        {
            _registeredIds.Add(id);
            Debug.WriteLine($"[Hotkey] RegisterHotKey OK — id={id}  mod=0x{modifiers:X}  vk=0x{virtualKey:X}");
        }
        else
        {
            var err = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[Hotkey] RegisterHotKey ÉCHEC — id={id}  mod=0x{modifiers:X}  vk=0x{virtualKey:X}  err={err}");
        }
        return ok;
    }

    public void UnregisterGlobalHotkey(int id)
    {
        if (_registeredIds.Remove(id))
            PInvoke.UnregisterHotKey(new HWND(_hwnd), id);
    }

    public void RegisterWindowHotkey(int id, uint modifiers, uint virtualKey, nint targetHwnd)
    {
        RegisterGlobalHotkey(id, modifiers, virtualKey);
    }

    public void UnregisterAllHotkeys()
    {
        var hwnd = new HWND(_hwnd);
        foreach (var id in _registeredIds)
            PInvoke.UnregisterHotKey(hwnd, id);
        _registeredIds.Clear();
    }

    public void Dispose() => UnregisterAllHotkeys();
}
