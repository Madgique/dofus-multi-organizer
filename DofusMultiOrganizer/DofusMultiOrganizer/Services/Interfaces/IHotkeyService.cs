using DofusOrganizer.Views;

namespace DofusOrganizer.Services.Interfaces;

public interface IHotkeyService : IDisposable
{
    void Initialize(MainWindow window);
    bool RegisterGlobalHotkey(int id, uint modifiers, uint virtualKey);
    void UnregisterGlobalHotkey(int id);
    void RegisterWindowHotkey(int id, uint modifiers, uint virtualKey, nint targetHwnd);
    void UnregisterAllHotkeys();
    event EventHandler<HotkeyTriggeredEventArgs> HotkeyTriggered;
}

public sealed class HotkeyTriggeredEventArgs : EventArgs
{
    public int HotkeyId { get; init; }
}
