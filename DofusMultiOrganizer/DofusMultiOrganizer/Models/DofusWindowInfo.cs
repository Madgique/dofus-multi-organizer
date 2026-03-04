namespace DofusOrganizer.Models;

/// <summary>
/// Représente une fenêtre Dofus détectée.
/// </summary>
public record DofusWindowInfo(
    nint Hwnd,
    string CharacterName,
    string DofusClass,
    string FullTitle,
    bool IsMinimized
);
