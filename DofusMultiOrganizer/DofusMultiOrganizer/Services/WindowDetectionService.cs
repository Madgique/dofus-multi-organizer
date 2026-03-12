using System.Diagnostics;
using DofusOrganizer.Models;
using DofusOrganizer.Services.Interfaces;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace DofusOrganizer.Services;

public sealed class WindowDetectionService : IWindowDetectionService
{
    private const string UnityWindowClass = "UnityWndClass";

    // Dofus Rétro : client x64 (navigateur Chromium intégré) + client x86 (Shockwave Flash)
    private static readonly HashSet<string> RetroWindowClasses =
    [
        "Chrome_WidgetWin_1",
        "ShockwaveFlash"
    ];

    public IReadOnlyList<DofusWindowInfo> DetectDofusWindows(DofusMode mode)
    {
        var windows = new List<DofusWindowInfo>();

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

            bool classMatches = mode == DofusMode.Unity
                ? className == UnityWindowClass
                : RetroWindowClasses.Contains(className);

            if (!classMatches)
                return true;

            // Pour Rétro : vérifier que le processus est bien Dofus (filtre Chrome, Edge, Discord…)
            if (mode == DofusMode.Retro && !IsDofusProcess(hwnd))
                return true;

            // 3. État minimisé
            bool isMinimized = PInvoke.IsIconic(hwnd);

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
                return true;

            var (characterName, dofusClass) = mode == DofusMode.Unity
                ? ParseUnityWindowTitle(title)
                : ParseRetroWindowTitle(title);

            windows.Add(new DofusWindowInfo(hwnd, characterName, dofusClass, title, isMinimized));

            return true;
        }, (LPARAM)0);

        return windows;
    }

    /// <summary>
    /// Parse "CharacterName - DofusClass [- Version - Config]".
    /// Exemple : "Madgique-F - Feca - 3.5.3.1 - Release" → ("Madgique-F", "Feca")
    /// </summary>
    private static (string CharacterName, string DofusClass) ParseUnityWindowTitle(string title)
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

    /// <summary>
    /// Retourne true si la fenêtre appartient à un processus dont le nom commence par "dofus".
    /// Filtre les fausses détections : Chrome, Edge, Discord… qui partagent Chrome_WidgetWin_1.
    /// </summary>
    private static bool IsDofusProcess(HWND hwnd)
    {
        try
        {
            uint pid;
            unsafe { PInvoke.GetWindowThreadProcessId(hwnd, &pid); }
            var processName = Process.GetProcessById((int)pid).ProcessName;
            return processName.StartsWith("dofus", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parse titre Rétro.
    /// Exemple : "Madgique - Dofus 1.35.5" → ("Madgique", "")
    /// Exemple x86 (ShockwaveFlash) : "Madgique" → ("Madgique", "")
    /// </summary>
    private static (string CharacterName, string DofusClass) ParseRetroWindowTitle(string title)
    {
        var sep = title.IndexOf(" - Dofus", StringComparison.OrdinalIgnoreCase);
        if (sep > 0)
            return (title[..sep].Trim(), string.Empty);

        return (title.Trim(), string.Empty);
    }
}
