namespace DofusOrganizer.Services.Interfaces;

public interface IWindowFocusService
{
    void FocusWindow(nint hwnd);
    nint GetForegroundWindow();
    bool IsWindowValid(nint hwnd);
    bool IsWindowMinimized(nint hwnd);
}
