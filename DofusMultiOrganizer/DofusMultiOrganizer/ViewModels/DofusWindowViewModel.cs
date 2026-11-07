using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using DofusOrganizer.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Win32;
using Windows.Win32.Foundation;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using Color = Windows.UI.Color;

namespace DofusOrganizer.ViewModels;

/// <summary>
/// ViewModel représentant une fenêtre Dofus dans la liste des settings.
/// </summary>
public sealed partial class DofusWindowViewModel : ObservableObject
{
    private static readonly ConcurrentDictionary<string, ImageSource?> _iconCache = new();

    private readonly Action _onHotkeyChanged;

    public DofusWindowInfo WindowInfo { get; }

    [ObservableProperty]
    private int _orderIndex;

    [ObservableProperty]
    private string _hotkey;

    [ObservableProperty]
    private bool _isWindowAlive;

    [ObservableProperty]
    private bool _isMinimized;

    [ObservableProperty]
    private ImageSource? _windowIcon;

    [ObservableProperty]
    private WindowNavigationRole _navigationRole;

    // Couleurs des indicateurs de navigation — doivent rester alignées avec la légende du XAML.
    private static readonly SolidColorBrush TransparentBrush = new(Color.FromArgb(0, 0, 0, 0));
    private static readonly SolidColorBrush CurrentBrush = new(Color.FromArgb(255, 59, 130, 246));  // bleu
    private static readonly SolidColorBrush NextBrush = new(Color.FromArgb(255, 34, 197, 94));     // vert
    private static readonly SolidColorBrush PreviousBrush = new(Color.FromArgb(255, 245, 158, 11)); // orange
    private static readonly LinearGradientBrush NextAndPreviousBrush = CreateNextAndPreviousBrush();

    /// <summary>
    /// Bordure de la ligne : bleue = fenêtre actuelle, verte = cible de « suivant »,
    /// orange = cible de « précédent ». Dégradé si les deux cibles sont la même fenêtre.
    /// </summary>
    public Brush NavigationBorderBrush => NavigationRole switch
    {
        WindowNavigationRole.Current => CurrentBrush,
        WindowNavigationRole.Next => NextBrush,
        WindowNavigationRole.Previous => PreviousBrush,
        WindowNavigationRole.NextAndPrevious => NextAndPreviousBrush,
        _ => TransparentBrush
    };

    partial void OnNavigationRoleChanged(WindowNavigationRole value)
    {
        OnPropertyChanged(nameof(NavigationBorderBrush));
    }

    private static LinearGradientBrush CreateNextAndPreviousBrush()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 34, 197, 94), Offset = 0 });
        brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 245, 158, 11), Offset = 1 });
        return brush;
    }

    public string CharacterName => WindowInfo.CharacterName;
    public string DofusClass => WindowInfo.DofusClass;

    // Propriétés calculées pour la vue (pas de converter nécessaire)
    public Visibility MinimizedTagVisibility => IsMinimized ? Visibility.Visible : Visibility.Collapsed;
    public double RowOpacity => IsMinimized ? 0.5 : 1.0;

    partial void OnIsMinimizedChanged(bool value)
    {
        OnPropertyChanged(nameof(MinimizedTagVisibility));
        OnPropertyChanged(nameof(RowOpacity));
    }

    public DofusWindowViewModel(
        DofusWindowInfo windowInfo,
        int orderIndex,
        string hotkey,
        Action onHotkeyChanged)
    {
        WindowInfo = windowInfo;
        _orderIndex = orderIndex;
        _hotkey = hotkey;
        _onHotkeyChanged = onHotkeyChanged;
        _isWindowAlive = true;
        _isMinimized = windowInfo.IsMinimized;
        _ = LoadIconAsync();
    }

    partial void OnHotkeyChanged(string value)
    {
        _onHotkeyChanged?.Invoke();
    }

    private async Task LoadIconAsync()
    {
        try
        {
            // Étape 1 : WM_GETICON — icône dynamique définie par Dofus en jeu
            // (même source que l'icône affichée dans la barre des tâches quand Dofus la modifie)
            var ms = await Task.Run(TryGetWindowIconStream);
            if (ms != null)
            {
                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(ms.AsRandomAccessStream());
                WindowIcon = bitmapImage;
                return;
            }

            // Étape 2 : fallback → icône de l'exe (StorageFile thumbnail)
            uint pid;
            unsafe { PInvoke.GetWindowThreadProcessId(new HWND(WindowInfo.Hwnd), &pid); }

            var exePath = Process.GetProcessById((int)pid).MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            if (_iconCache.TryGetValue(exePath, out var cached))
            {
                WindowIcon = cached;
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(exePath);
            using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 32);

            var bitmapFallback = new BitmapImage();
            await bitmapFallback.SetSourceAsync(thumbnail);

            _iconCache[exePath] = bitmapFallback;
            WindowIcon = bitmapFallback;
        }
        catch { }
    }

    /// <summary>
    /// Récupère l'icône via WM_GETICON (thread-pool).
    /// Retourne un MemoryStream PNG, ou null si le handle est 0 (pas encore défini en jeu).
    /// Note : Icon.FromHandle ne prend pas possession du HICON — Dofus reste propriétaire.
    /// </summary>
    private MemoryStream? TryGetWindowIconStream()
    {
        try
        {
            const uint WM_GETICON = 0x007F;
            var hwnd = new HWND(WindowInfo.Hwnd);

            // ICON_BIG=1 en priorité, ICON_SMALL=0 en fallback
            var hicon = PInvoke.SendMessage(hwnd, WM_GETICON, (nuint)1, (nint)0).Value;
            if (hicon == 0)
                hicon = PInvoke.SendMessage(hwnd, WM_GETICON, (nuint)0, (nint)0).Value;

            if (hicon == 0) return null;

            // Icon.FromHandle wraps without ownership — Dispose ne détruit pas le HICON Dofus
            var icon = Icon.FromHandle(hicon);
            using var gdiBitmap = icon.ToBitmap();
            var ms = new MemoryStream();
            gdiBitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            return ms;
        }
        catch
        {
            return null;
        }
    }
}
