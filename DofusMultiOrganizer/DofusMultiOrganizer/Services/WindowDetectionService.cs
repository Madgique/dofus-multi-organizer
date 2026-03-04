using System.Diagnostics;
using DofusOrganizer.Models;
using DofusOrganizer.Services.Interfaces;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace DofusOrganizer.Services;

public sealed class WindowDetectionService : IWindowDetectionService
{
    private const string TargetWindowClass = "UnityWndClass";

    public IReadOnlyList<DofusWindowInfo> DetectDofusWindows()
    {
        var windows = new List<DofusWindowInfo>();
        Debug.WriteLine("[Detection] ── EnumWindows start ──────────────────────");

        PInvoke.EnumWindows((hwnd, _) =>
        {
            // 1. Visibilité
            if (!PInvoke.IsWindowVisible(hwnd))
                return true;

            // 2. Classe de fenêtre
            string className;
            unsafe
            {
                var classBuffer = new char[256];
                int classLen;
                fixed (char* ptr = classBuffer)
                    classLen = PInvoke.GetClassName(hwnd, ptr, 256);
                className = new string(classBuffer, 0, classLen);
            }

            if (className != TargetWindowClass)
                return true;

            Debug.WriteLine($"[Detection] UnityWndClass trouvée — hwnd=0x{hwnd.Value:X}");

            // 3. État minimisé (on garde la fenêtre mais on la marque)
            bool isMinimized = PInvoke.IsIconic(hwnd);
            if (isMinimized)
                Debug.WriteLine($"[Detection]   → minimisée (incluse avec marqueur)");

            // 4. Titre
            string title;
            unsafe
            {
                var titleBuffer = new char[512];
                int titleLen;
                fixed (char* ptr = titleBuffer)
                    titleLen = PInvoke.GetWindowText(hwnd, ptr, 512);
                title = new string(titleBuffer, 0, titleLen);
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                Debug.WriteLine($"[Detection]   → ignorée (titre vide)");
                return true;
            }

            Debug.WriteLine($"[Detection]   titre = \"{title}\"");
            var (characterName, dofusClass) = ParseWindowTitle(title);
            Debug.WriteLine($"[Detection]   → personnage=\"{characterName}\"  classe=\"{dofusClass}\"  minimisée={isMinimized}");
            windows.Add(new DofusWindowInfo(hwnd, characterName, dofusClass, title, isMinimized));

            return true;
        }, (LPARAM)0);

        Debug.WriteLine($"[Detection] ── {windows.Count} fenêtre(s) trouvée(s) ──────────────────────");
        return windows;
    }

    /// <summary>
    /// Parse "CharacterName - DofusClass [- Version - Config]".
    /// Exemple : "Madgique-F - Feca - 3.5.3.1 - Release" → ("Madgique-F", "Feca")
    /// </summary>
    private static (string CharacterName, string DofusClass) ParseWindowTitle(string title)
    {
        var firstSep = title.IndexOf(" - ", StringComparison.Ordinal);
        if (firstSep <= 0)
            return (title, string.Empty);

        var characterName = title[..firstSep].Trim();
        var rest = title[(firstSep + 3)..];

        var secondSep = rest.IndexOf(" - ", StringComparison.Ordinal);
        var dofusClass = secondSep > 0 ? rest[..secondSep].Trim() : rest.Trim();

        return (characterName, dofusClass);
    }
}
